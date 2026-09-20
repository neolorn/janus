using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Privacy.Outbox;

namespace Janus.Privacy.Tests.Outbox;

/// <summary>
/// The outbox, in a list, so a test can read back what was put on it.
/// </summary>
internal sealed class OutboxStoreInMemory : IOutboxStore
{
    private readonly List<Delivery> _deliveries = [];

    /// <summary>
    /// What is on the outbox.
    /// </summary>
    public IReadOnlyList<Delivery> Deliveries => _deliveries;

    /// <inheritdoc/>
    public ValueTask AddAsync(Delivery delivery, CancellationToken cancellationToken)
    {
        _deliveries.Add(delivery);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<Delivery>> DueAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<Delivery>>(
        [
            .. _deliveries
                .Where(delivery => delivery.Status is Core.ErasureStatus.AwaitingSubscribers
                    && delivery.NextAttemptAt <= now)
                .OrderBy(delivery => delivery.RaisedAt),
        ]);

    /// <inheritdoc/>
    public ValueTask<Delivery?> FindAsync(
        DeliveryId delivery,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_deliveries.Find(held => held.Id == delivery));

    /// <inheritdoc/>
    public ValueTask RecordAsync(Delivery delivery, CancellationToken cancellationToken)
    {
        if (!_deliveries.Contains(delivery))
        {
            throw new InvalidOperationException("The delivery has no row to record progress on.");
        }

        return ValueTask.CompletedTask;
    }
}
