using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Erasures;
using Janus.Privacy.Outbox;

namespace Janus.Privacy.Tests.Outbox;

/// <summary>
/// The outbox, in a list, so a test can read back what was put on it, with the claim
/// each row carries.
/// </summary>
internal sealed class OutboxStoreInMemory : IOutboxStore
{
    private readonly List<Delivery> _deliveries = [];
    private readonly Dictionary<(DeliveryId, string), DateTimeOffset> _confirmedAt = [];
    private readonly Dictionary<DeliveryId, DateTimeOffset> _claims = [];

    /// <summary>
    /// What is on the outbox. A row a pass recorded under its claim is the row as the
    /// pass left it, and no longer the object a test added.
    /// </summary>
    public IReadOnlyList<Delivery> Deliveries => _deliveries;

    /// <summary>
    /// The unit of work the passes under test run in. Where a test names it, a write
    /// under a claim made while none is open is noted.
    /// </summary>
    public UnitOfWorkInMemory? Work { get; set; }

    /// <summary>
    /// Whether a write under a claim was made while no unit of work was open.
    /// </summary>
    public bool WroteOutsideAUnitOfWork { get; private set; }

    /// <summary>
    /// Every claim a pass took, in order.
    /// </summary>
    public List<DeliveryClaim> Claimed { get; } = [];

    /// <summary>
    /// Every renewal that changed a row, in order, as the claim then stood.
    /// </summary>
    public List<DeliveryClaim> Renewed { get; } = [];

    /// <summary>
    /// Every confirmation written under a claim, in order.
    /// </summary>
    public List<string> Confirmations { get; } = [];

    /// <summary>
    /// How many outcomes were written under a claim.
    /// </summary>
    public int Outcomes { get; private set; }

    /// <summary>
    /// Stands in for another pass that takes the row over, as one would once the claim
    /// on it had timed out.
    /// </summary>
    /// <param name="delivery">The row.</param>
    /// <param name="until">When the claim of the other pass times out.</param>
    public void TakeOver(DeliveryId delivery, DateTimeOffset until) => _claims[delivery] = until;

    /// <summary>
    /// The row as the outbox now holds it.
    /// </summary>
    /// <param name="delivery">What it is held under.</param>
    /// <returns>The row.</returns>
    public Delivery Held(DeliveryId delivery) => _deliveries.Single(held => held.Id == delivery);

    /// <inheritdoc/>
    public ValueTask AddAsync(Delivery delivery, CancellationToken cancellationToken)
    {
        _deliveries.Add(delivery);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<DeliveryId>> DueAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<DeliveryId>>(
        [
            .. _deliveries
                .Where(delivery => Due(delivery, now))
                .OrderBy(delivery => delivery.RaisedAt)
                .Select(delivery => delivery.Id),
        ]);

    /// <inheritdoc/>
    public ValueTask<DeliveryClaim?> ClaimAsync(
        DeliveryId delivery,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_deliveries.Any(held => held.Id == delivery && Due(held, now))
            ? Claim(delivery, now + timeout)
            : null);

    /// <inheritdoc/>
    public ValueTask<DeliveryClaim?> ClaimUnledgeredAsync(
        DeliveryId delivery,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_deliveries.Any(held => held.Id == delivery && Unledgered(held) && Unclaimed(held.Id, now))
            ? Claim(delivery, now + timeout)
            : null);

    /// <inheritdoc/>
    public ValueTask<DeliveryClaim?> RenewAsync(
        DeliveryClaim claim,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!Holds(claim))
        {
            return ValueTask.FromResult<DeliveryClaim?>(null);
        }

        Writing();

        DeliveryClaim renewed = claim with { Until = now + timeout };

        _claims[claim.Delivery] = renewed.Until;
        Renewed.Add(renewed);

        return ValueTask.FromResult<DeliveryClaim?>(renewed);
    }

    /// <inheritdoc/>
    public ValueTask<bool> ConfirmAsync(
        DeliveryClaim claim,
        string subscriber,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (!Holds(claim))
        {
            return ValueTask.FromResult(false);
        }

        Writing();
        Confirms(Held(claim.Delivery), subscriber, at);
        Confirmations.Add(subscriber);

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> RecordAsync(Delivery delivery, DeliveryClaim claim, CancellationToken cancellationToken)
    {
        if (!Holds(claim))
        {
            return ValueTask.FromResult(false);
        }

        Writing();

        Delivery held = Held(claim.Delivery);

        // The outcome carries the status, the attempts and the schedule; the
        // confirmations are the ones written as they happened.
        _deliveries[_deliveries.IndexOf(held)] = Delivery.Existing(
            held.Id,
            held.Subject,
            held.Kind,
            held.RaisedAt,
            held.Restricted,
            held.Reason,
            delivery.Status,
            delivery.Attempts,
            delivery.NextAttemptAt,
            held.Confirmed);
        _ = _claims.Remove(claim.Delivery);
        Outcomes++;

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> ReleaseAsync(DeliveryClaim claim, CancellationToken cancellationToken)
    {
        if (!Holds(claim))
        {
            return ValueTask.FromResult(false);
        }

        Writing();

        return ValueTask.FromResult(_claims.Remove(claim.Delivery));
    }

    /// <summary>
    /// Gets or sets what another transaction commits while this one waits for a
    /// delivery's row, so a test may change it under a decision already made.
    /// </summary>
    public Action<Delivery>? Locking { get; set; }

    /// <inheritdoc/>
    public ValueTask<Delivery?> FindForUpdateAsync(
        DeliveryId delivery,
        CancellationToken cancellationToken)
    {
        Delivery? held = _deliveries.Find(one => one.Id == delivery);

        if (held is not null)
        {
            Locking?.Invoke(held);
        }

        return ValueTask.FromResult(held);
    }

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

        return ValueTask.FromResult(latest is null ? null : Progress(latest));
    }

    /// <inheritdoc/>
    public ValueTask<DeliveryProgress?> ProgressAsync(
        DeliveryId delivery,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            _deliveries.Find(held => held.Id == delivery) is Delivery held ? Progress(held) : null);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<DeliveryProgress>> OutstandingAsync(
        SubjectEventKind kind,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<DeliveryProgress>>(
        [
            .. _deliveries
                .Where(delivery => delivery.Kind == kind
                    && delivery.Status is not Core.ErasureStatus.Complete)
                .OrderBy(delivery => delivery.RaisedAt)
                .Select(Progress),
        ]);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<DeliveryId>> UnledgeredAsync(
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<DeliveryId>>(
        [
            .. _deliveries
                .Where(Unledgered)
                .OrderBy(delivery => delivery.RaisedAt)
                .Skip(skip)
                .Take(take)
                .Select(delivery => delivery.Id),
        ]);

    private static bool Unledgered(Delivery delivery) =>
        delivery.Kind is SubjectEventKind.ErasureRequested
        && delivery.Status is Core.ErasureStatus.Complete
        && !delivery.Confirmed.Contains(ErasureLedgerSubscriber.Called);

    private bool Due(Delivery delivery, DateTimeOffset now) =>
        delivery.Status is Core.ErasureStatus.AwaitingSubscribers
        && delivery.NextAttemptAt <= now
        && Unclaimed(delivery.Id, now);

    private bool Unclaimed(DeliveryId delivery, DateTimeOffset now) =>
        !_claims.TryGetValue(delivery, out DateTimeOffset until) || until <= now;

    private bool Holds(DeliveryClaim claim) =>
        _claims.TryGetValue(claim.Delivery, out DateTimeOffset until) && until == claim.Until;

    private DeliveryClaim? Claim(DeliveryId delivery, DateTimeOffset until)
    {
        Writing();

        var claim = new DeliveryClaim(delivery, until);

        _claims[delivery] = until;
        Claimed.Add(claim);

        return claim;
    }

    private void Writing()
    {
        if (Work is { Open: false })
        {
            WroteOutsideAUnitOfWork = true;
        }
    }

    // What a read hands out is a copy, as a row read from the database is: what a pass
    // does to it reaches the outbox only through a write.
    private DeliveryProgress Progress(Delivery delivery) =>
        new(
            Delivery.Existing(
                delivery.Id,
                delivery.Subject,
                delivery.Kind,
                delivery.RaisedAt,
                delivery.Restricted,
                delivery.Reason,
                delivery.Status,
                delivery.Attempts,
                delivery.NextAttemptAt,
                delivery.Confirmed),
            delivery.Confirmed.ToDictionary(
                subscriber => subscriber,
                subscriber => _confirmedAt[(delivery.Id, subscriber)],
                StringComparer.Ordinal));
}
