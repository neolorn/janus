using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Erasures;

/// <summary>
/// The organization side of the deletion window: which windows have run out, and the
/// erasure at the end of one.
/// </summary>
/// <remarks>
/// Implements IDN-ORG-003, IDN-ORG-005 and CONV-DESIGN-003. Nothing here removes a
/// row: the organization persists, its identifier goes on resolving, and the audit
/// trail that references it stays queryable (IDN-PRIN-003).
/// </remarks>
internal interface IOrganizationStates
{
    /// <summary>
    /// The organizations whose deletion grace window began on or before an instant and
    /// which nothing has cancelled, the erased ones excluded.
    /// </summary>
    /// <param name="before">The instant the window must have begun by.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Them, oldest window first.</returns>
    ValueTask<IReadOnlyList<PendingOrganizationDeletion>> DeletingSinceAsync(
        DateTimeOffset before,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes the erasure at the end of one window: every current membership of the
    /// organization ends, what the organization is called becomes its own identifier,
    /// and the erasure is written onto the row.
    /// </summary>
    /// <param name="organization">Which organization.</param>
    /// <param name="at">The instant the erasure executes.</param>
    /// <param name="window">What <c>organization.deletion.grace</c> allows.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The memberships that ended, or nothing where the organization, read under a lock
    /// on its row, has no window left running that has elapsed: a cancellation or another
    /// pass committed first (CONV-DESIGN-003).
    /// </returns>
    /// <exception cref="InvalidOperationException">No such organization, or no transaction is open.</exception>
    ValueTask<IReadOnlyList<EndedMembership>?> EraseAsync(
        OrganizationId organization,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken);
}
