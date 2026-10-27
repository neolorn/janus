using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Authentication.Tests.Policies;
using Janus.Core;

namespace Janus.Authentication.Tests.Invitations;

/// <summary>
/// The allow grants a person gave an account, held in memory per organization: an ended
/// membership revokes those of its organization, and for the administrative organization
/// those of every organization the account no longer belongs to, each judged by the
/// hand-out rule (IDN-MEM-001, AUTHZ-GRANT-005, D-213).
/// </summary>
/// <param name="handOut">The hand-out rule the end is judged by.</param>
/// <param name="memberships">Which organizations each account belongs to.</param>
/// <remarks>
/// CONV-TEST-004: a fake that holds what a test gave and revokes it as the stores do.
/// </remarks>
internal sealed class MembershipRevocationsInMemory(
    IRoleHandOut handOut,
    MembershipLookupInMemory memberships) : IMembershipRevocations
{
    private readonly List<Held> _held = [];

    /// <summary>
    /// Gets the grants revoked, in the order the ends revoked them.
    /// </summary>
    public List<Revocation> Revoked { get; } = [];

    /// <summary>
    /// Gives an account a role across an organization, as a person's grant does.
    /// </summary>
    /// <param name="member">The account.</param>
    /// <param name="organization">Where.</param>
    /// <param name="role">The role, as the deployment defines it.</param>
    public void Give(SubjectId member, OrganizationId organization, DefinedRole role) =>
        _held.Add(new Held(member, organization, role));

    /// <summary>
    /// The roles an account still holds in an organization.
    /// </summary>
    /// <param name="member">The account.</param>
    /// <param name="organization">Where.</param>
    /// <returns>The roles, as given.</returns>
    public IReadOnlyList<RoleName> HeldBy(SubjectId member, OrganizationId organization) =>
        [.. _held.Where(held => held.Member == member && held.Organization == organization).Select(held => held.Role.Name)];

    /// <inheritdoc/>
    public async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        SubjectId member,
        OrganizationId organization,
        bool administrative,
        CancellationToken cancellationToken)
    {
        foreach (IGrouping<OrganizationId, Held> reached in (await ReachedAsync(member, organization, administrative, cancellationToken)
                .ConfigureAwait(false))
            .GroupBy(held => held.Organization))
        {
            if (await handOut
                    .RefusedAsync(
                        context,
                        Permissions.MembershipManage,
                        reached.Key,
                        [.. reached.Select(held => held.Role)],
                        [],
                        cancellationToken)
                    .ConfigureAwait(false)
                is Error refused)
            {
                return refused;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<DomainEvent>> RevokeAsync(
        SubjectId member,
        OrganizationId organization,
        bool administrative,
        SubjectId actor,
        string reason,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        List<DomainEvent> announced = [];

        foreach (Held held in await ReachedAsync(member, organization, administrative, cancellationToken)
            .ConfigureAwait(false))
        {
            _ = _held.Remove(held);
            Revoked.Add(new Revocation(held.Member, held.Organization, held.Role.Name, actor, reason, at));
            announced.Add(Announced(held, at) with { Actor = actor, Effective = actor });
        }

        return announced;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<DomainEvent>> RevokeAsync(
        SubjectId member,
        OrganizationId organization,
        bool administrative,
        SystemPrincipal principal,
        SubjectId endSetBy,
        string reason,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        List<DomainEvent> announced = [];

        foreach (Held held in await ReachedAsync(member, organization, administrative, cancellationToken)
            .ConfigureAwait(false))
        {
            _ = _held.Remove(held);
            Revoked.Add(new Revocation(held.Member, held.Organization, held.Role.Name, default, reason, at)
            {
                Principal = principal,
                EndSetBy = endSetBy,
            });
            announced.Add(Announced(held, at));
        }

        return announced;
    }

    // Chapter 10 section 5b (D-218): the grant revoked, across its organization, as the
    // stores announce it.
    private static GrantChanged Announced(Held held, DateTimeOffset at)
    {
        var grant = new GrantId(Guid.CreateVersion7(at));

        return new GrantChanged(
            at,
            string.Create(CultureInfo.InvariantCulture, $"grant-revoked:{grant.Value}@{at.UtcTicks}"),
            grant,
            GrantKind.Stored,
            SubjectType.User,
            held.Member.Value,
            held.Role.Name.ToString(),
            Deny: false,
            held.Organization,
            "organization",
            held.Organization.ToString(),
            GrantChange.Revoked)
        {
            Subject = held.Member,
        };
    }

    private async ValueTask<IReadOnlyList<Held>> ReachedAsync(
        SubjectId member,
        OrganizationId organization,
        bool administrative,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<OrganizationId> current = await memberships.OfAsync(member, cancellationToken).ConfigureAwait(false);

        return
        [
            .. _held.Where(held => held.Member == member
                && (held.Organization == organization || (administrative && !current.Contains(held.Organization)))),
        ];
    }

    /// <summary>
    /// A grant an end revoked.
    /// </summary>
    /// <param name="Member">Whose.</param>
    /// <param name="Organization">Where.</param>
    /// <param name="Role">Of which role.</param>
    /// <param name="Actor">Who ended the membership, or the nil subject for the job.</param>
    /// <param name="Reason">The end's reason.</param>
    /// <param name="At">When.</param>
    internal sealed record Revocation(
        SubjectId Member,
        OrganizationId Organization,
        RoleName Role,
        SubjectId Actor,
        string Reason,
        DateTimeOffset At)
    {
        /// <summary>
        /// The principal that carried out an end set in advance, where one did.
        /// </summary>
        public SystemPrincipal? Principal { get; init; }

        /// <summary>
        /// The administrator who set the end the principal carried out, where one did.
        /// </summary>
        public SubjectId? EndSetBy { get; init; }
    }

    private sealed record Held(SubjectId Member, OrganizationId Organization, DefinedRole Role);
}
