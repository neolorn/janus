using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Groups;
using Janus.Core;

namespace Janus.Authorization.Tests.Groups;

/// <summary>
/// The changes to groups a deployment wrote down, held in memory in the order they
/// were made.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake that keeps what it is given, so a test reads back the entry a
/// change left.
/// </remarks>
internal sealed class GroupAuditInMemory : IGroupAudit
{
    private readonly List<GroupChange> _changes = [];

    /// <summary>
    /// Every change recorded, oldest first.
    /// </summary>
    public IReadOnlyList<GroupChange> Changes => _changes;

    /// <inheritdoc/>
    public ValueTask CreatedAsync(
        Group group,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        Recorded(AuditActions.GroupCreated, group, member: null, reason, actor, at);

    /// <inheritdoc/>
    public ValueTask RemovedAsync(
        Group group,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        Recorded(AuditActions.GroupRemoved, group, member: null, reason, actor, at);

    /// <inheritdoc/>
    public ValueTask MemberAddedAsync(
        Group group,
        GrantSubject member,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        Recorded(AuditActions.GroupMemberAdded, group, member, reason, actor, at);

    /// <inheritdoc/>
    public ValueTask MemberRemovedAsync(
        Group group,
        GrantSubject member,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        Recorded(AuditActions.GroupMemberRemoved, group, member, reason, actor, at);

    private ValueTask Recorded(
        AuditAction action,
        Group group,
        GrantSubject? member,
        string reason,
        SubjectId actor,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(group);

        _changes.Add(new GroupChange(action, group.Id, group.Organization, member, reason, actor, at));

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// One change as it was written down.
    /// </summary>
    /// <param name="Action">What was done to the group.</param>
    /// <param name="Group">Which group.</param>
    /// <param name="Organization">The organization it belongs to.</param>
    /// <param name="Member">The member that joined or left, where one did.</param>
    /// <param name="Reason">Why.</param>
    /// <param name="Actor">Who.</param>
    /// <param name="At">When.</param>
    internal sealed record GroupChange(
        AuditAction Action,
        GroupId Group,
        OrganizationId Organization,
        GrantSubject? Member,
        string Reason,
        SubjectId Actor,
        DateTimeOffset At);
}
