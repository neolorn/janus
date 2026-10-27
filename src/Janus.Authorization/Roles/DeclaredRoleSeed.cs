using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Model;
using Janus.Core;

namespace Janus.Authorization.Roles;

/// <summary>
/// Defines, as the application starts, each role the host declared that no start has
/// seeded yet.
/// </summary>
/// <param name="model">Which roles the host declared.</param>
/// <param name="seeded">Which of them a start has seeded.</param>
/// <param name="roles">Where roles are read and written.</param>
/// <param name="audit">Where each definition is written down.</param>
/// <param name="events">Where each definition is announced.</param>
/// <param name="work">The one transaction each role is seeded in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-004, IDN-PRIN-001 and OPS-CFG-007 (D-204). This is the one
/// place a role is defined without <c>role:manage</c>, a step-up or a person's reason,
/// and it is reached from the start step alone: <see cref="IRoles"/> refuses every
/// system principal. A role once seeded is the administrators' from then on, so a role
/// of the name already held is left as it stands, and a seeded name is never seeded
/// again.
/// </remarks>
internal sealed class DeclaredRoleSeed(
    AuthorizationModel model,
    ISeededRoles seeded,
    IRoleStore roles,
    IRoleAudit audit,
    IEvents events,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// The start principal the declared roles are defined under (chapter 10 section
    /// 5.29).
    /// </summary>
    internal static SystemPrincipal Principal { get; } =
        SystemPrincipal.ForDeployment("role-declaration", "AUTHZ-GRANT-004", SystemOperation.RoleSeeding);

    /// <summary>
    /// Seeds every declared role not yet seeded, each in a transaction of its own under
    /// the lock of its name.
    /// </summary>
    /// <param name="cancellationToken">Abandons the start.</param>
    /// <returns>The work of seeding them.</returns>
    /// <exception cref="InvalidOperationException">
    /// A transaction could not begin or commit, which is a fault naming the code.
    /// </exception>
    public async ValueTask SeededAsync(CancellationToken cancellationToken)
    {
        foreach (DefinedRole declared in model.DeclaredRoles)
        {
            (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
                .Switch(_ => { }, error => throw new InvalidOperationException(error.Code.ToString()));

            // CONV-DESIGN-003: two starts at once seed a name one after the other, and
            // the second finds it seeded.
            await seeded.HoldAsync(declared.Name, cancellationToken).ConfigureAwait(false);

            if (!await seeded.SeededAsync(declared.Name, cancellationToken).ConfigureAwait(false))
            {
                // D-166 X3: the role is read under its row's lock, as a definition reads
                // it, so one an administrator made is the one left standing.
                if (await roles.FindForUpdateAsync(declared.Name, cancellationToken).ConfigureAwait(false) is null)
                {
                    DateTimeOffset now = time.GetUtcNow();

                    await roles
                        .CreateAsync(Role.Of(declared.Name, declared.Permissions, declared.HandsOut), cancellationToken)
                        .ConfigureAwait(false);
                    await audit
                        .DefinedAsync(
                            new DefinedRole(
                                declared.Name,
                                [.. declared.Permissions.OrderBy(permission => permission.ToString(), StringComparer.Ordinal)],
                                [.. declared.HandsOut.OrderBy(handed => handed.ToString(), StringComparer.Ordinal)]),
                            Principal,
                            now,
                            cancellationToken)
                        .ConfigureAwait(false);

                    // Chapter 10 section 5b (D-218): a declared role's seeding is a
                    // definition, announced in the transaction that makes it.
                    (await events
                            .PublishAsync(
                                new RoleChanged(now, Key(declared.Name, now), declared.Name.ToString(), RoleChange.Defined),
                                cancellationToken)
                            .ConfigureAwait(false))
                        .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
                }

                await seeded.RecordAsync(declared.Name, cancellationToken).ConfigureAwait(false);
            }

            (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
        }
    }

    private static string Key(RoleName role, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"role-defined:{role}@{at.UtcTicks}");
}
