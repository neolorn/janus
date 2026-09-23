using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Privacy.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.Outbox;

/// <summary>
/// The outbox, over the <c>outbox</c> and <c>outbox_confirmations</c> tables.
/// </summary>
/// <param name="context">The context the operation's reads and writes run on.</param>
/// <param name="time">The clock a confirmation is stamped with.</param>
/// <remarks>
/// Implements IDN-LIFE-003a and CONV-DESIGN-003. A delivery is added onto the
/// transaction in progress and committed by the caller's unit of work, so a fact and
/// its delivery reach the database together or not at all.
/// </remarks>
internal sealed class OutboxStore(StoreContext context, TimeProvider time) : IOutboxStore
{
    /// <inheritdoc/>
    public async ValueTask AddAsync(Delivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        await context.Outbox
            .AddAsync(
                new DeliveryRecord
                {
                    Id = delivery.Id,
                    Subject = delivery.Subject,
                    Kind = delivery.Kind,
                    RaisedAt = delivery.RaisedAt,
                    Restricted = delivery.Restricted,
                    Reason = delivery.Reason,
                    Status = delivery.Status,
                    Attempts = delivery.Attempts,
                    NextAttemptAt = delivery.NextAttemptAt,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Delivery>> DueAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
    [
        .. (await context.Outbox
                .Include(delivery => delivery.Confirmations)
                .Where(delivery => delivery.Status == Core.ErasureStatus.AwaitingSubscribers
                    && delivery.NextAttemptAt <= now)
                .OrderBy(delivery => delivery.RaisedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(Read),
    ];

    /// <inheritdoc/>
    public async ValueTask<Delivery?> FindAsync(
        DeliveryId delivery,
        CancellationToken cancellationToken)
    {
        DeliveryRecord? held = await context.Outbox
            .Include(row => row.Confirmations)
            .SingleOrDefaultAsync(row => row.Id == delivery, cancellationToken)
            .ConfigureAwait(false);

        return held is null ? null : Read(held);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(Delivery delivery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        DeliveryRecord held = await context.Outbox
            .Include(row => row.Confirmations)
            .SingleOrDefaultAsync(row => row.Id == delivery.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The delivery has no row to record progress on.");

        held.Status = delivery.Status;
        held.Attempts = delivery.Attempts;
        held.NextAttemptAt = delivery.NextAttemptAt;

        foreach (string subscriber in delivery.Confirmed)
        {
            if (held.Confirmations.Any(confirmation => string.Equals(
                confirmation.Subscriber,
                subscriber,
                StringComparison.Ordinal)))
            {
                continue;
            }

            held.Confirmations.Add(new DeliveryConfirmationRecord
            {
                Delivery = held.Id,
                Subscriber = subscriber,
                ConfirmedAt = time.GetUtcNow(),
            });
        }
    }

    private static Delivery Read(DeliveryRecord row) =>
        Delivery.Existing(
            row.Id,
            row.Subject,
            row.Kind,
            row.RaisedAt,
            row.Restricted,
            row.Reason,
            row.Status,
            row.Attempts,
            row.NextAttemptAt,
            [.. row.Confirmations.Select(confirmation => confirmation.Subscriber)]);
}
