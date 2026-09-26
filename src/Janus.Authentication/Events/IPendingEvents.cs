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
/// neither the change nor the event, and what committed is offered from the row.
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
    /// The events neither marked nor failed whose next pass is due, oldest first.
    /// </summary>
    /// <param name="now">The instant the pass runs at.</param>
    /// <param name="count">How many at most.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The events.</returns>
    ValueTask<IReadOnlyList<PendingEvent>> DueAsync(
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes what a pass made of one event onto the transaction in progress.
    /// </summary>
    /// <param name="pending">The event, as the pass left it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask RecordAsync(PendingEvent pending, CancellationToken cancellationToken);

    /// <summary>
    /// Clears the events every consumer has taken, which nothing offers or reads again.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were cleared.</returns>
    ValueTask<int> SweepAsync(CancellationToken cancellationToken);
}
