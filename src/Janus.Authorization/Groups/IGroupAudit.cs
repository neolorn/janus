using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Groups;

/// <summary>
/// Where a change to a group is written down: who, which group, which member, when
/// and why.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GROUP-001, OPS-CFG-007 and IDN-AUD-001. A change of members gives
/// or takes away what the group holds, so it is recorded as a grant is.
/// </remarks>
internal interface IGroupAudit
{
    /// <summary>
    /// Records a group created.
    /// </summary>
    /// <param name="group">Which group.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who created it.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask CreatedAsync(
        Group group,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a group removed.
    /// </summary>
    /// <param name="group">Which group.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who removed it.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RemovedAsync(
        Group group,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a member added to a group.
    /// </summary>
    /// <param name="group">The group that gained it.</param>
    /// <param name="member">The account or group that joined.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who added it.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask MemberAddedAsync(
        Group group,
        GrantSubject member,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a member taken out of a group.
    /// </summary>
    /// <param name="group">The group that lost it.</param>
    /// <param name="member">The account or group that left.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who took it out.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask MemberRemovedAsync(
        Group group,
        GrantSubject member,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
