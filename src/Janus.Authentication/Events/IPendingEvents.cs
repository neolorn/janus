using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Events;

/// <summary>
/// Where the emitted events wait for their consumers.
/// </summary>
/// <remarks>
/// Implements LIB-API-001, CONV-DESIGN-002 and CONV-DESIGN-003. An event is added
/// inside the transaction that made it true, so nothing here commits: a rollback leaves
/// neither the change nor the event, and what committed is offered from the row, which
/// a pass claims whole before any consumer is called.
/// </remarks>
internal interface IPendingEvents
{
    /// <summary>
    /// Writes one event onto the transaction in progress.
    /// </summary>
    /// <param name="pending">The event.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask AddAsync(PendingEvent pending, CancellationToken cancellationToken);

    /// <summary>
    /// The events neither marked nor failed whose next pass is due and that no pass
    /// holds, oldest first. The read takes no lock: the claim decides who carries a row.
    /// </summary>
    /// <param name="now">The instant the pass runs at.</param>
    /// <param name="count">How many at most.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>What the events are held under.</returns>
    ValueTask<IReadOnlyList<PendingEventId>> DueAsync(
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken);

    /// <summary>
    /// Claims one event whole for a pass, by one update that succeeds only where the row
    /// is neither marked nor failed, is due, its next attempt's instant come, and is
    /// unclaimed or its claim has timed out. The caller commits it on its own before any
    /// consumer is called.
    /// </summary>
    /// <param name="pending">What it is held under.</param>
    /// <param name="now">The instant of the claim.</param>
    /// <param name="timeout">How long the claim stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The claim, or nothing where another pass holds the row, the row is not due or
    /// the row is gone.
    /// </returns>
    ValueTask<EventClaim?> ClaimAsync(
        PendingEventId pending,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// Renews a claim before a consumer is called, by one update conditional on the claim
    /// still being the pass's own, which moves its end to the timeout from this instant.
    /// </summary>
    /// <param name="claim">The claim the pass holds.</param>
    /// <param name="now">The instant of the renewal.</param>
    /// <param name="timeout">How long the claim stands from it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The claim as renewed, or nothing where it has been taken over.</returns>
    ValueTask<EventClaim?> RenewAsync(
        EventClaim claim,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads one event as its row holds it.
    /// </summary>
    /// <param name="pending">What it is held under.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The event, or nothing where no row holds it.</returns>
    ValueTask<PendingEvent?> FindAsync(PendingEventId pending, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the consumers that have taken one event, as one of them takes it, by one
    /// update conditional on the claim.
    /// </summary>
    /// <param name="pending">The event, with the consumers that have taken it.</param>
    /// <param name="claim">The claim the pass holds.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it was written: not where the claim has been taken over.</returns>
    ValueTask<bool> TakeAsync(PendingEvent pending, EventClaim claim, CancellationToken cancellationToken);

    /// <summary>
    /// Writes what a pass made of one event (its attempts, its schedule, its mark or its
    /// failure) and releases the claim, by one update conditional on the claim.
    /// </summary>
    /// <param name="pending">The event, as the pass left it.</param>
    /// <param name="claim">The claim the pass was made under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it was written: not where the claim has been taken over.</returns>
    ValueTask<bool> RecordAsync(PendingEvent pending, EventClaim claim, CancellationToken cancellationToken);

    /// <summary>
    /// Clears the events every consumer has taken, which nothing offers or reads again.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were cleared.</returns>
    ValueTask<int> SweepAsync(CancellationToken cancellationToken);
}
