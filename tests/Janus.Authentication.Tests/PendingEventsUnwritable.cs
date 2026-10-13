using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Events;

namespace Janus.Authentication.Tests;

/// <summary>
/// The table the emitted events wait in, gone away: every write of an event throws, as
/// a database that cannot take the row does.
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class PendingEventsUnwritable : IPendingEvents
{
    /// <inheritdoc/>
    public ValueTask AddAsync(PendingEvent pending, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The pending events table at db.internal:5432 could not be written.");

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<PendingEventId>> DueAsync(
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<PendingEventId>>([]);

    /// <inheritdoc/>
    public ValueTask<EventClaim?> ClaimAsync(
        PendingEventId pending,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<EventClaim?>(null);

    /// <inheritdoc/>
    public ValueTask<EventClaim?> RenewAsync(
        EventClaim claim,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<EventClaim?>(null);

    /// <inheritdoc/>
    public ValueTask<PendingEvent?> FindAsync(PendingEventId pending, CancellationToken cancellationToken) =>
        ValueTask.FromResult<PendingEvent?>(null);

    /// <inheritdoc/>
    public ValueTask<bool> TakeAsync(PendingEvent pending, EventClaim claim, CancellationToken cancellationToken) =>
        ValueTask.FromResult(false);

    /// <inheritdoc/>
    public ValueTask<bool> RecordAsync(PendingEvent pending, EventClaim claim, CancellationToken cancellationToken) =>
        ValueTask.FromResult(false);

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(0);
}
