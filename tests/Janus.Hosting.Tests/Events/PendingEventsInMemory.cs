using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Events;

namespace Janus.Hosting.Tests.Events;

/// <summary>
/// The emitted events a test publishes, held in memory.
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class PendingEventsInMemory : IPendingEvents
{
    private readonly List<PendingEvent> _held = [];

    /// <summary>
    /// Every event written, as the last pass left it.
    /// </summary>
    public IReadOnlyList<PendingEvent> Held => _held;

    /// <inheritdoc/>
    public ValueTask AddAsync(PendingEvent pending, CancellationToken cancellationToken)
    {
        _held.Add(pending);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<PendingEvent>> DueAsync(
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<PendingEvent>>(
        [
            .. _held
                .Where(pending => pending.PublishedAt is null
                    && pending.FailedAt is null
                    && pending.NextAttemptAt <= now)
                .OrderBy(pending => pending.Id.Value)
                .Take(count),
        ]);

    /// <inheritdoc/>
    public ValueTask RecordAsync(PendingEvent pending, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(_held.RemoveAll(pending => pending.PublishedAt is not null));
}
