using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Recovery;
using Janus.Authorization.Gate;
using Janus.Authorization.Grants;
using Janus.Authorization.Groups;
using Janus.Authorization.Roles;
using Janus.Core;

namespace Janus.Storage.Authentication.Recovery;

/// <summary>
/// Whether an approver may hand out every role the account whose recovery they approve
/// holds, over the grant and group stores, judged by the gate's hand-out rule.
/// </summary>
/// <param name="grants">The grants the account holds, and those its groups hold.</param>
/// <param name="groups">The groups naming the account, and those above them.</param>
/// <param name="roles">What each role reached carries.</param>
/// <param name="admission">The gate's judgment of a change that hands out roles.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-RECOV-002, AUTHZ-GRANT-005 and CONV-LAYOUT-001 (D-251). The roles are
/// those of the account's live allow grants, its own and those of every group it belongs
/// to, directly or through nesting; each is judged in the organization its grant is in, on
/// the record the grant is on where it is on one, as a change handing it out there that
/// widens nobody's access. A deny takes access away, so it hands nothing over.
/// </remarks>
internal sealed class ApproverReach(
    IGrantStore grants,
    IGroupStore groups,
    IRoleStore roles,
    IHandOutAdmission admission,
    TimeProvider time) : IApproverReach
{
    /// <inheritdoc/>
    public async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        DateTimeOffset now = time.GetUtcNow();
        List<Grant> held =
        [
            .. (await grants.NamingAsync(subject, cancellationToken).ConfigureAwait(false))
                .Where(grant => !grant.Deny && grant.IsLive(now)),
        ];

        foreach (Group group in await groups.HoldingAsync(subject, cancellationToken).ConfigureAwait(false))
        {
            held.AddRange((await HeldThroughAsync(group, now, cancellationToken).ConfigureAwait(false))
                .Where(grant => !grant.Deny));
        }

        foreach (IGrouping<(OrganizationId Organization, ResourceReference? Resource), Grant> scoped in held
            .GroupBy(grant => (grant.Organization, Scope(grant)))
            .OrderBy(scoped => scoped.Key.Organization.Value))
        {
            List<DefinedRole> handed = [];

            foreach (RoleName name in scoped
                .Select(grant => grant.Role)
                .Distinct()
                .OrderBy(name => name.ToString(), StringComparer.Ordinal))
            {
                Role read = await roles.EffectiveAsync(name, cancellationToken).ConfigureAwait(false) ?? Role.Of(name, []);

                handed.Add(new DefinedRole(read.Name, [.. read.Permissions], [.. read.HandsOut], read.GrantsMustEnd));
            }

            var change = new HandOut(
                Permissions.RecoveryApprove,
                scoped.Key.Organization,
                scoped.Key.Resource,
                handed,
                Widened: []);

            if ((await admission.AdmitAsync(context, change, cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error refused)
            {
                return refused;
            }
        }

        return null;
    }

    // The record a grant is on, or nothing for the whole organization.
    private static ResourceReference? Scope(Grant grant) =>
        grant.ResourceType is ResourceType type && grant.ResourceId is ResourceId id
            ? new ResourceReference(type, id)
            : null;

    // AUTHZ-GROUP-001: a member holds the live grants of the group and of every group
    // holding it.
    private async ValueTask<IReadOnlyList<Grant>> HeldThroughAsync(
        Group group,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<GroupId> above = await groups
            .GroupsOfAsync(GrantSubject.Of(group.Id), cancellationToken)
            .ConfigureAwait(false);

        return await grants
            .HeldByAsync([GrantSubject.Of(group.Id), .. above.Select(GrantSubject.Of)], group.Organization, at, cancellationToken)
            .ConfigureAwait(false);
    }
}
