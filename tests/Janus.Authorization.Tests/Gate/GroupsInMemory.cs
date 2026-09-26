using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Groups;
using Janus.Core;

namespace Janus.Authorization.Tests.Gate;

/// <summary>
/// The groups of one organization held in memory, edges and all, so that a test may
/// ask how often the closure was read.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake that follows nesting the way the closure does, not a recorder
/// of calls that answers whatever a test told it to.
/// </remarks>
internal sealed class GroupsInMemory : IGroupStore
{
    private readonly Dictionary<GroupId, Group> _groups = [];
    private readonly Dictionary<GroupId, List<GrantSubject>> _members = [];

    /// <summary>
    /// How many times the transitive set of a subject has been read.
    /// </summary>
    public int Reads { get; private set; }

    /// <summary>
    /// How many times one group's row has been read by its identifier.
    /// </summary>
    public int Found { get; private set; }

    /// <inheritdoc/>
    public ValueTask<Group?> FindAsync(GroupId id, CancellationToken cancellationToken)
    {
        Found++;

        return ValueTask.FromResult(_groups.GetValueOrDefault(id));
    }

    /// <inheritdoc/>
    public ValueTask<OrganizationId?> ScopeOfAsync(GroupId id, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_groups.TryGetValue(id, out Group? group) ? group.Organization : (OrganizationId?)null);

    /// <inheritdoc/>
    public ValueTask CreateAsync(Group group, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);

        _groups[group.Id] = group;
        _members[group.Id] = [];

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<Group>> InAsync(OrganizationId organization, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<Group>>(
        [
            .. _groups.Values
                .Where(group => group.Organization == organization)
                .OrderBy(group => group.Name, StringComparer.Ordinal),
        ]);

    /// <inheritdoc/>
    public ValueTask RemoveAsync(GroupId id, CancellationToken cancellationToken)
    {
        _groups.Remove(id);
        _members.Remove(id);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask AddMemberAsync(GroupId group, GrantSubject member, CancellationToken cancellationToken)
    {
        Edges(group).Add(member);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveMemberAsync(GroupId group, GrantSubject member, CancellationToken cancellationToken)
    {
        Edges(group).Remove(member);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<GrantSubject>> MembersAsync(
        GroupId group,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<GrantSubject>>([.. Edges(group)]);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<GroupId>> GroupsOfAsync(
        GrantSubject subject,
        CancellationToken cancellationToken)
    {
        Reads++;

        return ValueTask.FromResult<IReadOnlyList<GroupId>>([.. Holding(subject)]);
    }

    /// <inheritdoc/>
    public ValueTask<bool> ReachesAsync(
        GroupId group,
        GrantSubject member,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Holding(member).Contains(group));

    private List<GrantSubject> Edges(GroupId group)
    {
        if (!_members.TryGetValue(group, out List<GrantSubject>? held))
        {
            held = [];
            _members[group] = held;
        }

        return held;
    }

    // The groups holding the subject, followed upward until nothing new is reached,
    // which is what nesting to any depth means (AUTHZ-GROUP-001).
    private HashSet<GroupId> Holding(GrantSubject subject)
    {
        HashSet<GroupId> reached = [];
        var pending = new Queue<GrantSubject>();
        pending.Enqueue(subject);

        while (pending.Count > 0)
        {
            GrantSubject at = pending.Dequeue();

            foreach (GroupId group in _members
                .Where(entry => entry.Value.Contains(at))
                .Select(entry => entry.Key))
            {
                if (reached.Add(group))
                {
                    pending.Enqueue(GrantSubject.Of(group));
                }
            }
        }

        return reached;
    }
}
