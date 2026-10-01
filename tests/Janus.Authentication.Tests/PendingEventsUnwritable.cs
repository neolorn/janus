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
    public ValueTask<IReadOnlyList<PendingEvent>> DueAsync(
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<PendingEvent>>([]);

    /// <inheritdoc/>
    public ValueTask RecordAsync(PendingEvent pending, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(0);
}
