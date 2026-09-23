using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Outbox;

namespace Janus.Privacy.Tests.Outbox;

/// <summary>
/// The outbox, in a list, so a test can read back what was put on it.
/// </summary>
internal sealed class OutboxStoreInMemory : IOutboxStore
{
    private readonly List<Delivery> _deliveries = [];
    private readonly Dictionary<(DeliveryId, string), DateTimeOffset> _confirmedAt = [];

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

    /// <summary>
    /// Records a subscriber's confirmation of a delivery at an instant, as the worker
    /// does when the subscriber answers.
    /// </summary>
    /// <param name="delivery">Which delivery.</param>
    /// <param name="subscriber">What the subscriber is called.</param>
    /// <param name="at">When it confirmed.</param>
    public void Confirms(Delivery delivery, string subscriber, DateTimeOffset at)
    {
        delivery.Confirm(subscriber);
        _confirmedAt[(delivery.Id, subscriber)] = at;
    }

    /// <inheritdoc/>
    public ValueTask<DeliveryProgress?> LatestAsync(
        SubjectId subject,
        SubjectEventKind kind,
        CancellationToken cancellationToken)
    {
        Delivery? latest = _deliveries
            .Where(delivery => delivery.Subject == subject && delivery.Kind == kind)
            .OrderByDescending(delivery => delivery.RaisedAt)
            .FirstOrDefault();

        return ValueTask.FromResult(
            latest is null
                ? null
                : new DeliveryProgress(
                    latest,
                    latest.Confirmed.ToDictionary(
                        subscriber => subscriber,
                        subscriber => _confirmedAt[(latest.Id, subscriber)],
                        StringComparer.Ordinal)));
    }
}
