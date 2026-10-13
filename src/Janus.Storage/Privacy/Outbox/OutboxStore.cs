using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Erasures;
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
/// its delivery reach the database together or not at all. A pass claims a row whole by
/// one conditional update, and its renewal, each confirmation and the outcome are written
/// only under that claim.
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
    public async ValueTask<IReadOnlyList<DeliveryId>> DueAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await context.Outbox
            .AsNoTracking()
            .Where(delivery => delivery.Status == Core.ErasureStatus.AwaitingSubscribers
                && delivery.NextAttemptAt <= now
                && (delivery.ClaimedUntil == null || delivery.ClaimedUntil <= now))
            .OrderBy(delivery => delivery.RaisedAt)
            .ThenBy(delivery => delivery.Id)
            .Select(delivery => delivery.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<DeliveryClaim?> ClaimAsync(
        DeliveryId delivery,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset until = RowClaim.Until(now, timeout);

        int claimed = await context.Outbox
            .Where(row => row.Id == delivery
                && row.Status == Core.ErasureStatus.AwaitingSubscribers
                && row.NextAttemptAt <= now
                && (row.ClaimedUntil == null || row.ClaimedUntil <= now))
            .ExecuteUpdateAsync(
                row => row.SetProperty(one => one.ClaimedUntil, until),
                cancellationToken)
            .ConfigureAwait(false);

        return claimed == 1 ? new DeliveryClaim(delivery, until) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<DeliveryClaim?> ClaimUnledgeredAsync(
        DeliveryId delivery,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset until = RowClaim.Until(now, timeout);

        int claimed = await context.Outbox
            .Where(row => row.Id == delivery
                && row.Kind == SubjectEventKind.ErasureRequested
                && row.Status == Core.ErasureStatus.Complete
                && !row.Confirmations.Any(confirmation =>
                    confirmation.Subscriber == ErasureLedgerSubscriber.Called)
                && (row.ClaimedUntil == null || row.ClaimedUntil <= now))
            .ExecuteUpdateAsync(
                row => row.SetProperty(one => one.ClaimedUntil, until),
                cancellationToken)
            .ConfigureAwait(false);

        return claimed == 1 ? new DeliveryClaim(delivery, until) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<DeliveryClaim?> RenewAsync(
        DeliveryClaim claim,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset until = RowClaim.Until(now, timeout);

        int renewed = await context.Outbox
            .Where(row => row.Id == claim.Delivery && row.ClaimedUntil == claim.Until)
            .ExecuteUpdateAsync(
                row => row.SetProperty(one => one.ClaimedUntil, until),
                cancellationToken)
            .ConfigureAwait(false);

        return renewed == 1 ? claim with { Until = until } : null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The update that finds the claim still standing changes nothing and holds the row
    /// until the transaction ends, so no pass takes the row over between it and the
    /// confirmation written with it.
    /// </remarks>
    public async ValueTask<bool> ConfirmAsync(
        DeliveryClaim claim,
        string subscriber,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(subscriber);

        int held = await context.Outbox
            .Where(row => row.Id == claim.Delivery && row.ClaimedUntil == claim.Until)
            .ExecuteUpdateAsync(
                row => row.SetProperty(one => one.ClaimedUntil, claim.Until),
                cancellationToken)
            .ConfigureAwait(false);

        if (held != 1)
        {
            return false;
        }

        if (!await context.OutboxConfirmations
                .AnyAsync(
                    confirmation => confirmation.Delivery == claim.Delivery && confirmation.Subscriber == subscriber,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            await context.OutboxConfirmations
                .AddAsync(
                    new DeliveryConfirmationRecord
                    {
                        Delivery = claim.Delivery,
                        Subscriber = subscriber,
                        ConfirmedAt = at,
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RecordAsync(Delivery delivery, DeliveryClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        Core.ErasureStatus status = delivery.Status;
        int attempts = delivery.Attempts;
        DateTimeOffset next = delivery.NextAttemptAt;

        return await context.Outbox
            .Where(row => row.Id == claim.Delivery && row.ClaimedUntil == claim.Until)
            .ExecuteUpdateAsync(
                row => row
                    .SetProperty(one => one.Status, status)
                    .SetProperty(one => one.Attempts, attempts)
                    .SetProperty(one => one.NextAttemptAt, next)
                    .SetProperty(one => one.ClaimedUntil, (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false) == 1;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ReleaseAsync(DeliveryClaim claim, CancellationToken cancellationToken) =>
        await context.Outbox
            .Where(row => row.Id == claim.Delivery && row.ClaimedUntil == claim.Until)
            .ExecuteUpdateAsync(
                row => row.SetProperty(one => one.ClaimedUntil, (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false) == 1;

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
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<Delivery?> FindForUpdateAsync(
        DeliveryId delivery,
        CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A delivery's row is held only inside the operation's transaction.");
        }

        bool tracked = context.Outbox.Local.Any(row => row.Id == delivery);

        DeliveryRecord? held = (await context.Outbox
                .FromSql($"SELECT * FROM identity.outbox WHERE id = {delivery.Value} FOR UPDATE")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault();

        if (held is null)
        {
            return null;
        }

        // A row the context already tracks was read before the lock, so it is read again:
        // what the decision is made on is the row as it stood when the lock was taken,
        // with every confirmation committed by then.
        if (tracked)
        {
            await context.Entry(held).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }

        await context.Entry(held).Collection(row => row.Confirmations).LoadAsync(cancellationToken).ConfigureAwait(false);

        return Read(held);
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

    /// <inheritdoc/>
    public async ValueTask<DeliveryProgress?> LatestAsync(
        SubjectId subject,
        SubjectEventKind kind,
        CancellationToken cancellationToken)
    {
        DeliveryRecord? held = await context.Outbox
            .AsNoTracking()
            .Include(row => row.Confirmations)
            .Where(row => row.Subject == subject && row.Kind == kind)
            .OrderByDescending(row => row.RaisedAt)
            .ThenByDescending(row => row.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return held is null ? null : Progress(held);
    }

    /// <inheritdoc/>
    public async ValueTask<DeliveryProgress?> ProgressAsync(
        DeliveryId delivery,
        CancellationToken cancellationToken)
    {
        DeliveryRecord? held = await context.Outbox
            .AsNoTracking()
            .Include(row => row.Confirmations)
            .SingleOrDefaultAsync(row => row.Id == delivery, cancellationToken)
            .ConfigureAwait(false);

        return held is null ? null : Progress(held);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<DeliveryProgress>> OutstandingAsync(
        SubjectEventKind kind,
        CancellationToken cancellationToken) =>
    [
        .. (await context.Outbox
                .AsNoTracking()
                .Include(row => row.Confirmations)
                .Where(row => row.Kind == kind && row.Status != Core.ErasureStatus.Complete)
                .OrderBy(row => row.RaisedAt)
                .ThenBy(row => row.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(Progress),
    ];

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<DeliveryId>> UnledgeredAsync(
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        await context.Outbox
            .AsNoTracking()
            .Where(row => row.Kind == SubjectEventKind.ErasureRequested
                && row.Status == Core.ErasureStatus.Complete
                && !row.Confirmations.Any(confirmation =>
                    confirmation.Subscriber == ErasureLedgerSubscriber.Called))
            .OrderBy(row => row.RaisedAt)
            .ThenBy(row => row.Id)
            .Select(row => row.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private static DeliveryProgress Progress(DeliveryRecord held) =>
        new(
            Read(held),
            held.Confirmations.ToDictionary(
                confirmation => confirmation.Subscriber,
                confirmation => confirmation.ConfirmedAt,
                StringComparer.Ordinal));

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
