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
/// <param name="audit">Where every change is written down.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, AUTHZ-GROUP-001 and OPS-CFG-007. The organization a change
/// is judged in is the group's own, read from its row, never from the caller. A change
/// of members confers or takes away what the group holds, so it is stepped up as a
/// grant is; creating a group and removing one nothing names confer nothing.
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
    IGroupAudit audit,
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
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<GroupId>(notBegun);
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
            return Result.Failure(Malformed("id"));
        }

        if (Stated(reason) is not string stated)
        {
            return Result.Failure(Malformed("reason"));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: what names the group is read with the organization's groups held,
        // which a change of members and a grant to a group hold too.
        await groups.HoldAsync(found.Organization, cancellationToken).ConfigureAwait(false);

        if (await groups.FindAsync(group, cancellationToken).ConfigureAwait(false) is not Group held)
        {
            return await RefusedAsync(Malformed("id"), cancellationToken).ConfigureAwait(false);
        }

        // AUTHZ-GRANT-003 AC3: a grant's history names the group it was given to,
        // revoked or not, and a member or a containing group would lose what it holds
        // without a change of members recording it.
        if (await NamedAsync(held, cancellationToken).ConfigureAwait(false))
        {
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
            return Result.Failure(Malformed("id"));
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

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: the nesting is judged with the organization's groups held, so a
        // change of members at the same moment cannot close a cycle or confer through a
        // group that came to administer, and the group and its member still stand.
        await groups.HoldAsync(found.Organization, cancellationToken).ConfigureAwait(false);

        Group? held = await groups.FindAsync(group, cancellationToken).ConfigureAwait(false);

        Error? refused = held is null
            ? Malformed("id")
            : !await JoinableAsync(member, held.Organization, cancellationToken).ConfigureAwait(false)
                ? Unjoinable()
                : await CycleAsync(group, member, cancellationToken).ConfigureAwait(false)
                    ? Error.From(ErrorCodes.GroupCycle)
                    : await ChangeRefusedAsync(context, acting, session, held, cancellationToken).ConfigureAwait(false);

        if (refused is not null)
        {
            return await RefusedAsync(refused, cancellationToken).ConfigureAwait(false);
        }

        // X9: a member already held changes nothing and records nothing, and the unit
        // of work still ends before the operation returns.
        if (!(await groups.MembersAsync(group, cancellationToken).ConfigureAwait(false)).Contains(member))
        {
            await groups.AddMemberAsync(group, member, cancellationToken).ConfigureAwait(false);
            await audit
                .MemberAddedAsync(held!, member, stated, acting, context.BreakGlassReason, time.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);
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
            return Result.Failure(Malformed("id"));
        }

        if (Stated(reason) is not string stated)
        {
            return Result.Failure(Malformed("reason"));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: what the group and the groups above it hold is judged with the
        // organization's groups held, as for an added member.
        await groups.HoldAsync(found.Organization, cancellationToken).ConfigureAwait(false);

        Group? held = await groups.FindAsync(group, cancellationToken).ConfigureAwait(false);

        if ((held is null
                ? Malformed("id")
                : await ChangeRefusedAsync(context, acting, session, held, cancellationToken).ConfigureAwait(false))
            is Error refused)
        {
            return await RefusedAsync(refused, cancellationToken).ConfigureAwait(false);
        }

        if (!(await groups.MembersAsync(group, cancellationToken).ConfigureAwait(false)).Contains(member))
        {
            return Result.Success();
        }

        await groups.RemoveMemberAsync(group, member, cancellationToken).ConfigureAwait(false);
        await audit
            .MemberRemovedAsync(held!, member, stated, acting, context.BreakGlassReason, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    // API-CONV-002: a free-text field is 1 to 1024 characters after trimming.
    private static string? Stated(string text) =>
        text?.Trim() is { Length: > 0 and <= 1024 } stated ? stated : null;

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    private static Error Unjoinable() =>
        Error.From(ErrorCodes.RequestInvalid, "member", JsonSerializer.SerializeToElement("subjectId"));

    // A refusal decided inside the transaction ends it before the operation returns.
    private async ValueTask<Result> RefusedAsync(Error refused, CancellationToken cancellationToken) =>
        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => Result.Failure(refused), Result.Failure);

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
    // an allow or a deny, is made only by a system administrator. The step-up is judged
    // last, as for a grant.
    private async ValueTask<Error?> ChangeRefusedAsync(
        AccessContext context,
        SubjectId acting,
        SessionId session,
        Group group,
        CancellationToken cancellationToken)
    {
        if (await AdministersAsync(group, cancellationToken).ConfigureAwait(false)
            && await scope.RefusedAsync(context, Permissions.SystemAdminister, cancellationToken).ConfigureAwait(false)
                is Error administering)
        {
            return administering;
        }

        return (await stepUp
            .RequireAsync(acting, session, StepUpAction.GrantManage, cancellationToken)
            .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);
    }

    // A role nobody can read is taken to carry system administration, so the check
    // fails closed. D-166 X3: each role is read under its row's lock, in one order, so a
    // definition at the same moment is judged before or after the change.
    private async ValueTask<bool> AdministersAsync(Group group, CancellationToken cancellationToken)
    {
        IReadOnlyList<GroupId> above = await groups
            .GroupsOfAsync(GrantSubject.Of(group.Id), cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Grant> held = await grants
            .HeldByAsync(
                [GrantSubject.Of(group.Id), .. above.Select(GrantSubject.Of)],
                group.Organization,
                time.GetUtcNow(),
                cancellationToken)
            .ConfigureAwait(false);

        foreach (RoleName role in held
            .Select(grant => grant.Role)
            .Distinct()
            .OrderBy(name => name.ToString(), StringComparer.Ordinal))
        {
            if (await roles.FindForUpdateAsync(role, cancellationToken).ConfigureAwait(false) is not Role read
                || read.Allows(Permissions.SystemAdminister))
            {
                return true;
            }
        }

        return false;
    }
}
