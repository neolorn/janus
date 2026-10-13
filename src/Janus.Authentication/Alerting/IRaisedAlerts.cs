using System;
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
/// neither the change nor the alert, and what committed is carried from the row. A row
/// is carried only under a claim, and what a pass made of it is written only while that
/// claim stands, so two passes over the same rows carry each once.
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
    /// The conditions waiting longest that no standing claim holds, read without a lock.
    /// </summary>
    /// <param name="now">The instant the pass runs at.</param>
    /// <param name="count">How many at most.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The conditions, oldest first.</returns>
    ValueTask<IReadOnlyList<RaisedAlert>> OldestAsync(
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken);

    /// <summary>
    /// Claims one condition for a pass, by one update that succeeds only where the row
    /// is unclaimed or its claim has timed out. The caller commits it on its own before
    /// the router is called.
    /// </summary>
    /// <param name="alert">What it is held under.</param>
    /// <param name="now">The instant of the claim.</param>
    /// <param name="timeout">How long the claim stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The instant the claim stands until, which names it, or nothing where another pass
    /// holds the row or the row is gone.
    /// </returns>
    ValueTask<DateTimeOffset?> ClaimAsync(
        RaisedAlertId alert,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes a condition the alert channels have carried, by one statement that
    /// changes nothing where the claim has been taken over.
    /// </summary>
    /// <param name="alert">What it is held under.</param>
    /// <param name="claim">The claim it was carried under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the row was removed under the claim.</returns>
    ValueTask<bool> RemoveAsync(RaisedAlertId alert, DateTimeOffset claim, CancellationToken cancellationToken);

    /// <summary>
    /// Gives up the claim on a condition the router refused, so the next pass takes it,
    /// by one update that changes nothing where the claim has been taken over.
    /// </summary>
    /// <param name="alert">What it is held under.</param>
    /// <param name="claim">The claim the attempt was made under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the claim was given up.</returns>
    ValueTask<bool> ReleaseAsync(RaisedAlertId alert, DateTimeOffset claim, CancellationToken cancellationToken);
}
