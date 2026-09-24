using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Alerting;

/// <summary>
/// Where the raised conditions wait for the alert channels.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-001, CONV-DESIGN-002 and CONV-DESIGN-003. A condition is added
/// inside the transaction that raised it, so nothing here commits: a rollback leaves
/// neither the change nor the alert, and what committed is carried from the row.
/// </remarks>
internal interface IRaisedAlerts
{
    /// <summary>
    /// Writes one raised condition onto the transaction in progress.
    /// </summary>
    /// <param name="alert">The condition.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask AddAsync(RaisedAlert alert, CancellationToken cancellationToken);

    /// <summary>
    /// The conditions waiting longest.
    /// </summary>
    /// <param name="count">How many at most.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The conditions, oldest first.</returns>
    ValueTask<IReadOnlyList<RaisedAlert>> OldestAsync(int count, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a condition the channels have carried.
    /// </summary>
    /// <param name="alert">What it is kept under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of removing it.</returns>
    ValueTask RemoveAsync(RaisedAlertId alert, CancellationToken cancellationToken);
}
