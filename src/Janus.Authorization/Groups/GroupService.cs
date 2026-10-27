using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Authorization.Grants;
using Janus.Authorization.Roles;
using Janus.Core;

namespace Janus.Authorization.Groups;

/// <summary>
/// The groups of an organization, created, removed and given members by an
/// administrator.
/// </summary>
/// <param name="gate">Whether the caller may manage groups where the group belongs.</param>
/// <param name="unscoped">The refusal of a group the deployment holds no row for.</param>
/// <param name="scope">Whether the caller holds system administration.</param>
/// <param name="stepUp">What a change of members asks of the caller's session.</param>
/// <param name="groups">Where groups and their members are read and written.</param>
/// <param name="grants">What a group holds, and whether any grant was given to it.</param>
/// <param name="roles">Where the roles a group holds are read.</param>
/// <param name="emergency">Which account joins no group.</param>
/// <param name="handOut">Whether the caller may hand out the roles a change of members reaches.</param>
/// <param name="audit">Where every change is written down.</param>
/// <param name="events">Where every change of members is announced.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, AUTHZ-GROUP-001, AUTHZ-GRANT-005 and OPS-CFG-007. The
/// organization a change is judged in is the group's own, read from its row, never from
/// the caller. A change of members confers or takes away what the group holds, so it is
/// stepped up and handed out as a grant is; creating a group and removing one nothing
/// names confer nothing.
/// </remarks>
internal sealed class GroupService(
    IAccessGate gate,
    IUnscopedRefusal unscoped,
    AdministrativeScope scope,
    IStepUpGate stepUp,
    IGroupStore groups,
    IGrantStore grants,
    IRoleStore roles,
    IEmergencyAccount emergency,
    IHandOutAdmission handOut,
    IGroupAudit audit,
    IEvents events,
    IUnitOfWork work,
    TimeProvider time) : IGroups
{
    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<DefinedGroup>>> InAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await ManagingRefusedAsync(context, organization, cancellationToken).ConfigureAwait(false)
            is Error denied)
        {
            return Result.Failure<IReadOnlyList<DefinedGroup>>(denied);
        }

        var defined = new List<DefinedGroup>();

        foreach (Group group in await groups.InAsync(organization, cancellationToken).ConfigureAwait(false))
        {
            defined.Add(new DefinedGroup(
                group.Id,
                group.Name,
                await groups.MembersAsync(group.Id, cancellationToken).ConfigureAwait(false)));
        }

        return Result.Success<IReadOnlyList<DefinedGroup>>(defined);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<GroupId>> CreateAsync(
        AccessContext context,
        OrganizationId organization,
        string name,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A change is made by a person, whose identity the record carries.
        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure<GroupId>(Error.From(ErrorCodes.Denied));
        }

        if (await ManagingRefusedAsync(context, organization, cancellationToken).ConfigureAwait(false)
            is Error denied)
        {
            return Result.Failure<GroupId>(denied);
        }

        if (Stated(name) is not string named)
        {
            return Result.Failure<GroupId>(Malformed("name"));
        }

        if (Stated(reason) is not string stated)
        {
            return Result.Failure<GroupId>(Malformed("reason"));
        }

        var group = Group.Create(GroupId.New(time), organization, named);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<GroupId>(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await ManagingRefusedAsync(context, organization, cancellationToken).ConfigureAwait(false)
            is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<GroupId>(since);
        }

        await groups.CreateAsync(group, cancellationToken).ConfigureAwait(false);
        await audit
            .CreatedAsync(group, stated, acting, context.BreakGlassReason, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<GroupId>(notCommitted);
        }

        return Result.Success(group.Id);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RemoveAsync(
        AccessContext context,
        GroupId group,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await GroupRefusedAsync(context, group, cancellationToken).ConfigureAwait(false) is Error denied)
        {
            return Result.Failure(denied);
        }

        if (await groups.FindAsync(group, cancellationToken).ConfigureAwait(false) is not Group found)
        {
            return Result.Failure(await GoneAsync(context, cancellationToken).ConfigureAwait(false));
        }

        if (Stated(reason) is not string stated)
        {
            return Result.Failure(Malformed("reason"));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await GroupRefusedAsync(context, group, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // D-166 X3: what names the group is read with the organization's groups held,
        // which a change of members and a grant to a group hold too.
        await groups.HoldAsync(found.Organization, cancellationToken).ConfigureAwait(false);

        if (await groups.FindAsync(group, cancellationToken).ConfigureAwait(false) is not Group held)
        {
            Error gone = await GoneAsync(context, cancellationToken).ConfigureAwait(false);

            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(gone);
        }

        // AUTHZ-GRANT-003 AC3: a grant's history names the group it was given to,
        // revoked or not, and a member or a containing group would lose what it holds
        // without a change of members recording it.
        if (await NamedAsync(held, cancellationToken).ConfigureAwait(false))
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(Error.From(ErrorCodes.GroupInUse));
        }

        await groups.RemoveAsync(group, cancellationToken).ConfigureAwait(false);
        await audit
            .RemovedAsync(held, stated, acting, context.BreakGlassReason, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> AddMemberAsync(
        AccessContext context,
        SessionId session,
        GroupId group,
        GrantSubject member,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await GroupRefusedAsync(context, group, cancellationToken).ConfigureAwait(false) is Error denied)
        {
            return Result.Failure(denied);
        }

        if (await groups.FindAsync(group, cancellationToken).ConfigureAwait(false) is not Group found)
        {
            return Result.Failure(await GoneAsync(context, cancellationToken).ConfigureAwait(false));
        }

        // X5, D-166: a member group that does not exist or belongs to another
        // organization is a body read and understood that names nothing it can hold.
        if (!await JoinableAsync(member, found.Organization, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(Unjoinable());
        }

        // OPS-BOOT-002: a group's grants would be something further granted to the
        // break-glass session's account.
        if (await emergency.FindAsync(cancellationToken).ConfigureAwait(false) is SubjectId reserved
            && member == GrantSubject.Of(reserved))
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (Stated(reason) is not string stated)
        {
            return Result.Failure(Malformed("reason"));
        }

        // CONV-DESIGN-002, D-207: every refusal the step-up must follow is judged first
        // without a lock, so the step-up, which may ask the carrier's signal, is judged
        // before the unit of work begins; each is judged again under the lock inside.
        Error? unmet = await CycleAsync(group, member, cancellationToken).ConfigureAwait(false)
            ? Error.From(ErrorCodes.GroupCycle)
            : await ChangeRefusedAsync(context, acting, session, found, member, adding: true, cancellationToken)
                .ConfigureAwait(false);

        if (unmet is not null)
        {
            return Result.Failure(unmet);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await GroupRefusedAsync(context, group, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // D-166 X3: the nesting is judged with the organization's groups held, so a
        // change of members at the same moment cannot close a cycle or confer through a
        // group that came to administer, and the group and its member still stand.
        await groups.HoldAsync(found.Organization, cancellationToken).ConfigureAwait(false);

        Group? held = await groups.FindAsync(group, cancellationToken).ConfigureAwait(false);

        Error? refused = held is null
            ? await GoneAsync(context, cancellationToken).ConfigureAwait(false)
            : !await JoinableAsync(member, held.Organization, cancellationToken).ConfigureAwait(false)
                ? Unjoinable()
                : await CycleAsync(group, member, cancellationToken).ConfigureAwait(false)
                    ? Error.From(ErrorCodes.GroupCycle)
                    : await AdministeringRefusedAsync(context, held, locking: true, cancellationToken).ConfigureAwait(false)
                        ?? await HandOutRefusedAsync(context, held, member, adding: true, cancellationToken)
                            .ConfigureAwait(false);

        if (refused is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(refused);
        }

        // CONV-DESIGN-003: a member already held changes nothing and records nothing, so
        // the unit of work is rolled back.
        if ((await groups.MembersAsync(group, cancellationToken).ConfigureAwait(false)).Contains(member))
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Success();
        }

        await groups.AddMemberAsync(group, member, cancellationToken).ConfigureAwait(false);
        await audit
            .MemberAddedAsync(held!, member, stated, acting, context.BreakGlassReason, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        if (await AnnouncedAsync(held!, member, GroupMembershipChange.Added, context, cancellationToken).ConfigureAwait(false)
            is Error unannounced)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unannounced);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RemoveMemberAsync(
        AccessContext context,
        SessionId session,
        GroupId group,
        GrantSubject member,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await GroupRefusedAsync(context, group, cancellationToken).ConfigureAwait(false) is Error denied)
        {
            return Result.Failure(denied);
        }

        if (await groups.FindAsync(group, cancellationToken).ConfigureAwait(false) is not Group found)
        {
            return Result.Failure(await GoneAsync(context, cancellationToken).ConfigureAwait(false));
        }

        if (Stated(reason) is not string stated)
        {
            return Result.Failure(Malformed("reason"));
        }

        // CONV-DESIGN-002, D-207: every refusal the step-up must follow is judged first
        // without a lock, so the step-up, which may ask the carrier's signal, is judged
        // before the unit of work begins; each is judged again under the lock inside.
        if (await ChangeRefusedAsync(context, acting, session, found, member, adding: false, cancellationToken)
                .ConfigureAwait(false) is Error unmet)
        {
            return Result.Failure(unmet);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await GroupRefusedAsync(context, group, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // D-166 X3: what the group and the groups above it hold is judged with the
        // organization's groups held, as for an added member.
        await groups.HoldAsync(found.Organization, cancellationToken).ConfigureAwait(false);

        Group? held = await groups.FindAsync(group, cancellationToken).ConfigureAwait(false);

        if ((held is null
                ? await GoneAsync(context, cancellationToken).ConfigureAwait(false)
                : await AdministeringRefusedAsync(context, held, locking: true, cancellationToken).ConfigureAwait(false)
                    ?? await HandOutRefusedAsync(context, held, member, adding: false, cancellationToken)
                        .ConfigureAwait(false))
            is Error refused)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(refused);
        }

        // CONV-DESIGN-003: a member not held changes nothing and records nothing, so the
        // unit of work is rolled back.
        if (!(await groups.MembersAsync(group, cancellationToken).ConfigureAwait(false)).Contains(member))
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Success();
        }

        await groups.RemoveMemberAsync(group, member, cancellationToken).ConfigureAwait(false);
        await audit
            .MemberRemovedAsync(held!, member, stated, acting, context.BreakGlassReason, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        if (await AnnouncedAsync(held!, member, GroupMembershipChange.Removed, context, cancellationToken).ConfigureAwait(false)
            is Error unannounced)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unannounced);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    // Chapter 10 section 5b (D-218): the change is announced in its transaction, with the
    // person who acted as the context gives them.
    private async ValueTask<Error?> AnnouncedAsync(
        Group group,
        GrantSubject member,
        GroupMembershipChange change,
        AccessContext context,
        CancellationToken cancellationToken) =>
        (await events
            .PublishAsync(
                GroupMembershipChanges.Of(group, member, change, time.GetUtcNow()) with
                {
                    Actor = context.Acting,
                    Effective = context.Effective,
                },
                cancellationToken)
            .ConfigureAwait(false))
        .Match(() => (Error?)null, error => error);

    // API-CONV-002: a free-text field is 1 to 1024 characters after trimming.
    private static string? Stated(string text) =>
        text?.Trim() is { Length: > 0 and <= 1024 } stated ? stated : null;

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    private static Error Unjoinable() =>
        Error.From(ErrorCodes.RequestInvalid, "member", JsonSerializer.SerializeToElement("subjectId"));

    // AUTHZ-SCOPE-001, CONV-DESIGN-002 AC3: a change to a group is judged in the
    // organization its row belongs to, and that alone is read of it before the gate. A
    // group the deployment holds no row for belongs to none, and the gate refuses it as
    // it refuses a caller managing nothing where a group is; one removed since its
    // organization was read is no such group once it is read.
    private async ValueTask<Error?> GroupRefusedAsync(
        AccessContext context,
        GroupId group,
        CancellationToken cancellationToken) =>
        await groups.ScopeOfAsync(group, cancellationToken).ConfigureAwait(false) is OrganizationId organization
            ? await ManagingRefusedAsync(context, organization, cancellationToken).ConfigureAwait(false)
            : await unscoped.RefusedAsync(context, Permissions.GroupManage, cancellationToken).ConfigureAwait(false);

    // 09 section 8: a group that no longer stands belongs to no organization, so it is
    // refused as one no row names, whoever asks (AUTHZ-SCOPE-001).
    private ValueTask<Error> GoneAsync(AccessContext context, CancellationToken cancellationToken) =>
        unscoped.RefusedAsync(context, Permissions.GroupManage, cancellationToken);

    private async ValueTask<Error?> ManagingRefusedAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        (await gate
            .RequireAsync(context, Permissions.GroupManage, organization, cancellationToken)
            .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);

    // A group belongs to one organization, and only a group of the same one joins it,
    // as only such a group is given a grant there.
    private async ValueTask<bool> JoinableAsync(
        GrantSubject member,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        member.Type != SubjectType.Group
            || (await groups.FindAsync(new GroupId(member.Value), cancellationToken).ConfigureAwait(false))
                ?.Organization == organization;

    // AUTHZ-GROUP-001: a group that already reaches this one, or this one itself,
    // would come to hold itself.
    private async ValueTask<bool> CycleAsync(
        GroupId group,
        GrantSubject member,
        CancellationToken cancellationToken) =>
        member.Type == SubjectType.Group
            && (member.Value == group.Value
                || await groups.ReachesAsync(new GroupId(member.Value), GrantSubject.Of(group), cancellationToken)
                    .ConfigureAwait(false));

    private async ValueTask<bool> NamedAsync(Group group, CancellationToken cancellationToken) =>
        (await groups.MembersAsync(group.Id, cancellationToken).ConfigureAwait(false)).Count > 0
        || (await groups.GroupsOfAsync(GrantSubject.Of(group.Id), cancellationToken).ConfigureAwait(false)).Count > 0
        || await grants.NamesAsync(GrantSubject.Of(group.Id), cancellationToken).ConfigureAwait(false);

    // OPS-CFG-007: a member holds what the group holds and what every group holding it
    // holds, so a change of members where any of them carries system administration,
    // an allow or a deny, is made only by a system administrator. The roles it
    // reaches are handed out (AUTHZ-GRANT-005). The step-up is judged last, as for a
    // grant, and before the unit of work begins, since it may ask the carrier's signal
    // (CONV-DESIGN-002, D-207).
    private async ValueTask<Error?> ChangeRefusedAsync(
        AccessContext context,
        SubjectId acting,
        SessionId session,
        Group group,
        GrantSubject member,
        bool adding,
        CancellationToken cancellationToken) =>
        await AdministeringRefusedAsync(context, group, locking: false, cancellationToken).ConfigureAwait(false)
            ?? await HandOutRefusedAsync(context, group, member, adding, cancellationToken).ConfigureAwait(false)
            ?? (await stepUp
                .RequireAsync(acting, session, StepUpAction.GrantManage, cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);

    // OPS-CFG-007: the refusal of a change of members by a caller who is no system
    // administrator, where the group or a group holding it carries system
    // administration; judged first without a lock and again under the roles' locks.
    private async ValueTask<Error?> AdministeringRefusedAsync(
        AccessContext context,
        Group group,
        bool locking,
        CancellationToken cancellationToken) =>
        await AdministersAsync(group, locking, cancellationToken).ConfigureAwait(false)
            ? await scope.RefusedAsync(context, Permissions.SystemAdminister, cancellationToken).ConfigureAwait(false)
            : null;

    // A role nobody can read is taken to carry system administration, so the check
    // fails closed. D-166 X3: inside the unit of work each role is read under its row's
    // lock, in one order, so a definition at the same moment is judged before or after
    // the change.
    private async ValueTask<bool> AdministersAsync(Group group, bool locking, CancellationToken cancellationToken)
    {
        IReadOnlyList<Grant> held = await ReachedAsync(group, cancellationToken).ConfigureAwait(false);

        foreach (RoleName role in held
            .Select(grant => grant.Role)
            .Distinct()
            .OrderBy(name => name.ToString(), StringComparer.Ordinal))
        {
            Role? read = locking
                ? await roles.FindForUpdateAsync(role, cancellationToken).ConfigureAwait(false)
                : await roles.FindAsync(role, cancellationToken).ConfigureAwait(false);

            if (read is null || read.Allows(Permissions.SystemAdminister))
            {
                return true;
            }
        }

        return false;
    }

    // AUTHZ-GRANT-005 (D-212): a change of members hands out every role the group
    // reaches, each at the scope of the grant reaching it. Adding the actor, or a group
    // holding it, widens the actor's own access whatever the group reaches, and so does
    // removing either from a group that reaches a deny. A role nobody can read hands
    // out what it is named.
    private async ValueTask<Error?> HandOutRefusedAsync(
        AccessContext context,
        Group group,
        GrantSubject member,
        bool adding,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Grant> reached = await ReachedAsync(group, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<GrantSubject> widened = adding || reached.Any(grant => grant.Deny) ? [member] : [];

        List<HandOut> changes = [];

        foreach (IGrouping<ResourceReference?, Grant> scoped in reached.GroupBy(Scope))
        {
            List<DefinedRole> handed = [];

            foreach (RoleName name in scoped
                .Select(grant => grant.Role)
                .Distinct()
                .OrderBy(name => name.ToString(), StringComparer.Ordinal))
            {
                Role read = await roles.FindAsync(name, cancellationToken).ConfigureAwait(false) ?? Role.Of(name, []);

                handed.Add(new DefinedRole(read.Name, [.. read.Permissions], [.. read.HandsOut]));
            }

            changes.Add(new HandOut(Permissions.GroupManage, group.Organization, scoped.Key, handed, widened));
        }

        if (changes.Count == 0)
        {
            changes.Add(new HandOut(Permissions.GroupManage, group.Organization, null, [], widened));
        }

        foreach (HandOut change in changes)
        {
            if ((await handOut.AdmitAsync(context, change, cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error refused)
            {
                return refused;
            }
        }

        return null;
    }

    // AUTHZ-GROUP-001: a member holds the live grants of the group and of every group
    // holding it, allows and denies alike.
    private async ValueTask<IReadOnlyList<Grant>> ReachedAsync(Group group, CancellationToken cancellationToken)
    {
        IReadOnlyList<GroupId> above = await groups
            .GroupsOfAsync(GrantSubject.Of(group.Id), cancellationToken)
            .ConfigureAwait(false);

        return await grants
            .HeldByAsync(
                [GrantSubject.Of(group.Id), .. above.Select(GrantSubject.Of)],
                group.Organization,
                time.GetUtcNow(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    // The record a grant is on, or nothing for the whole organization.
    private static ResourceReference? Scope(Grant grant) =>
        grant.ResourceType is ResourceType type && grant.ResourceId is ResourceId id
            ? new ResourceReference(type, id)
            : null;
}
