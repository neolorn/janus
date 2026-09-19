using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Groups;

/// <summary>
/// Where groups and their members are read and written, and where the closure that
/// answers "which groups does this principal belong to" is maintained.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GROUP-001, AUTHZ-GROUP-002 and CONV-DESIGN-003. The closure is
/// written in the same transaction as the membership change it follows from, so no
/// permission query walks the nesting and none carries a recursive common table
/// expression (AUTHZ-INHERIT-002 AC4).
/// </remarks>
internal interface IGroupStore
{
    /// <summary>
    /// Reads one group.
    /// </summary>
    /// <param name="id">Which group.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The group, or nothing where no such row exists.</returns>
    ValueTask<Group?> FindAsync(GroupId id, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a new group.
    /// </summary>
    /// <param name="group">The group to create.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask CreateAsync(Group group, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a member, and carries the change through the closure in the same
    /// transaction.
    /// </summary>
    /// <param name="group">The group gaining the member.</param>
    /// <param name="member">The account or group joining it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AddMemberAsync(GroupId group, GrantSubject member, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a member out, and carries the change through the closure in the same
    /// transaction.
    /// </summary>
    /// <param name="group">The group losing the member.</param>
    /// <param name="member">The account or group leaving it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RemoveMemberAsync(GroupId group, GrantSubject member, CancellationToken cancellationToken);

    /// <summary>
    /// The members a group holds directly.
    /// </summary>
    /// <param name="group">Which group.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Its direct members.</returns>
    ValueTask<IReadOnlyList<GrantSubject>> MembersAsync(GroupId group, CancellationToken cancellationToken);

    /// <summary>
    /// Every group a subject belongs to, at any depth, read from the closure in one
    /// query.
    /// </summary>
    /// <param name="subject">The account or group.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The groups it belongs to.</returns>
    ValueTask<IReadOnlyList<GroupId>> GroupsOfAsync(GrantSubject subject, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a group already reaches a subject, at any depth, which is what makes
    /// adding it a cycle.
    /// </summary>
    /// <param name="group">The group that would gain the member.</param>
    /// <param name="member">The account or group that would join it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the group already reaches it.</returns>
    ValueTask<bool> ReachesAsync(GroupId group, GrantSubject member, CancellationToken cancellationToken);
}
