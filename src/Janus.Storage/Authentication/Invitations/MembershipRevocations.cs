using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Authorization.Gate;
using Janus.Authorization.Grants;
using Janus.Authorization.Groups;
using Janus.Authorization.Roles;
using Janus.Core;
using Janus.Identity.Organizations;

namespace Janus.Storage.Authentication.Invitations;

/// <summary>
/// The grants an ended membership revokes and the groups it takes the account out of,
/// over the grant and group stores, judged by the gate's hand-out rule.
/// </summary>
/// <param name="memberships">Which organizations the account still belongs to.</param>
/// <param name="grants">The grants the account holds, and those its groups hold.</param>
/// <param name="groups">The groups naming the account, and those above them.</param>
/// <param name="roles">What each role reached carries.</param>
/// <param name="admission">The gate's judgment of a change that hands out roles.</param>
/// <param name="grantAudit">Where each revocation is written down.</param>
/// <param name="groupAudit">Where each removal is written down.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements IDN-MEM-001, AUTHZ-GRANT-005 and CONV-LAYOUT-001 (D-213, D-217). A grant a
/// derivation wrote or materialised is the host's data speaking, so it stands, and so does
/// a deny grant to the account. A revocation hands out its grant's role at the grant's
/// scope, and leaving a group hands out every role the group reaches, allow or deny; none
/// widens the actor's own access, since none of the allow grants it reaches remains. An end
/// the job <c>membership-ends</c> carries out is not judged again, since it was judged when
/// set; its grants carry the nil revoker and its records are the job's.
/// </remarks>
internal sealed class MembershipRevocations(
    IMembershipStore memberships,
    IGrantStore grants,
    IGroupStore groups,
    IRoleStore roles,
    IHandOutAdmission admission,
    IGrantAudit grantAudit,
    IGroupAudit groupAudit,
    TimeProvider time) : IMembershipRevocations
{
    /// <inheritdoc/>
    public async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        SubjectId member,
        OrganizationId organization,
        bool administrative,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        DateTimeOffset now = time.GetUtcNow();
        Reach reach = await ReachedAsync(member, organization, administrative, now, cancellationToken)
            .ConfigureAwait(false);
        List<Grant> handed = [.. reach.Grants];

        foreach (Group group in reach.Groups)
        {
            handed.AddRange(await HeldThroughAsync(group, now, cancellationToken).ConfigureAwait(false));
        }

        foreach (IGrouping<(OrganizationId Organization, ResourceReference? Resource), Grant> scoped in handed
            .GroupBy(grant => (grant.Organization, Scope(grant))))
        {
            List<DefinedRole> touched = [];

            foreach (RoleName name in scoped
                .Select(grant => grant.Role)
                .Distinct()
                .OrderBy(name => name.ToString(), StringComparer.Ordinal))
            {
                Role read = await roles.FindAsync(name, cancellationToken).ConfigureAwait(false) ?? Role.Of(name, []);

                touched.Add(new DefinedRole(read.Name, [.. read.Permissions], [.. read.HandsOut]));
            }

            var change = new HandOut(
                Permissions.MembershipManage,
                scoped.Key.Organization,
                scoped.Key.Resource,
                touched,
                Widened: []);

            if ((await admission.AdmitAsync(context, change, cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error refused)
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
        ArgumentNullException.ThrowIfNull(reason);

        return await RevokeAsync(
                member,
                organization,
                administrative,
                actor,
                reason,
                at,
                (held, cancellation) => grantAudit.RevokedAsync(held, breakGlassReason, cancellation),
                (group, cancellation) => groupAudit.MemberRemovedAsync(
                    group,
                    GrantSubject.Of(member),
                    reason,
                    actor,
                    breakGlassReason,
                    at,
                    cancellation),
                cancellationToken)
            .ConfigureAwait(false);
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
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(reason);

        // D-217: the job's revocation carries the nil subject as revoker, as a record's
        // removal does (AUTHZ-INHERIT-002).
        return await RevokeAsync(
                member,
                organization,
                administrative,
                revoker: default,
                reason,
                at,
                (held, cancellation) => grantAudit.RevokedAsync(held, principal, endSetBy, cancellation),
                (group, cancellation) => groupAudit.MemberRemovedAsync(
                    group,
                    GrantSubject.Of(member),
                    reason,
                    principal,
                    endSetBy,
                    at,
                    cancellation),
                cancellationToken)
            .ConfigureAwait(false);
    }

    // The record a grant is on, or nothing for the whole organization.
    private static ResourceReference? Scope(Grant grant) =>
        grant.ResourceType is ResourceType type && grant.ResourceId is ResourceId id
            ? new ResourceReference(type, id)
            : null;

    private async ValueTask<IReadOnlyList<DomainEvent>> RevokeAsync(
        SubjectId member,
        OrganizationId organization,
        bool administrative,
        SubjectId revoker,
        string reason,
        DateTimeOffset at,
        Func<Grant, CancellationToken, ValueTask> revoked,
        Func<Group, CancellationToken, ValueTask> removed,
        CancellationToken cancellationToken)
    {
        Reach reach = await ReachedAsync(member, organization, administrative, at, cancellationToken)
            .ConfigureAwait(false);

        // Chapter 10 section 5b (D-218): each change is handed back to be announced in the
        // end's transaction.
        List<DomainEvent> announced = [];

        // D-166 X3: each grant is read again under its row's lock, in one order, so a
        // revocation at the same moment leaves it to the one that came first.
        foreach (Grant reached in reach.Grants.OrderBy(grant => grant.Id.Value))
        {
            if (await grants.FindForUpdateAsync(reached.Id, cancellationToken).ConfigureAwait(false)
                is not { Kind: GrantKind.Stored } held
                || !held.IsLive(at))
            {
                continue;
            }

            held.Revoke(revoker, at, reason)
                .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

            await grants.RecordAsync(held, cancellationToken).ConfigureAwait(false);
            await revoked(held, cancellationToken).ConfigureAwait(false);
        }

        // D-166 X3: the groups of each organization are held as a change of members holds
        // them, so the account leaves each group it is still a member of once.
        foreach (IGrouping<OrganizationId, Group> held in reach.Groups.GroupBy(group => group.Organization))
        {
            await groups.HoldAsync(held.Key, cancellationToken).ConfigureAwait(false);

            foreach (Group group in held.OrderBy(group => group.Id.Value))
            {
                if (!(await groups.MembersAsync(group.Id, cancellationToken).ConfigureAwait(false))
                    .Contains(GrantSubject.Of(member)))
                {
                    continue;
                }

                await groups.RemoveMemberAsync(group.Id, GrantSubject.Of(member), cancellationToken)
                    .ConfigureAwait(false);
                await removed(group, cancellationToken).ConfigureAwait(false);
                announced.Add(Announced(
                    GroupMembershipChanges.Of(group, GrantSubject.Of(member), GroupMembershipChange.Removed, at),
                    revoker));
            }
        }

        return announced;
    }

    // The person who ended the membership acted; the job, which carries the nil revoker,
    // is no person.
    private static DomainEvent Announced(DomainEvent raised, SubjectId revoker) =>
        revoker == default ? raised : raised with { Actor = revoker, Effective = revoker };

    // IDN-MEM-001 (D-213): the organization itself, and, for the administrative
    // organization, every other the account holds no current membership of.
    private async ValueTask<Reach> ReachedAsync(
        SubjectId member,
        OrganizationId organization,
        bool administrative,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        HashSet<OrganizationId> current =
        [
            .. (await memberships.FindBySubjectAsync(member, cancellationToken).ConfigureAwait(false))
                .Where(membership => membership.IsCurrent)
                .Select(membership => membership.Organization),
        ];

        bool Reached(OrganizationId held) =>
            held == organization || (administrative && !current.Contains(held));

        IReadOnlyList<Grant> named = await grants.NamingAsync(member, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<Group> holding = await groups.HoldingAsync(member, cancellationToken).ConfigureAwait(false);

        // D-217: a deny grant to the account stands, since revoking it would lift what it
        // defeats; an end only takes access away.
        return new Reach(
            [.. named.Where(grant => grant.Kind is GrantKind.Stored && !grant.Deny && grant.IsLive(at) && Reached(grant.Organization))],
            [.. holding.Where(group => Reached(group.Organization))]);
    }

    // AUTHZ-GROUP-001: a member holds the live grants of the group and of every group
    // holding it, allows and denies alike.
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

    // What an end reaches: the grants it revokes and the groups it leaves.
    private sealed record Reach(IReadOnlyList<Grant> Grants, IReadOnlyList<Group> Groups);
}
