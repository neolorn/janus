using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Privacy.Outbox;

/// <summary>
/// One fact about a subject, written in the transaction that made it true, waiting
/// for every registered subscriber, or every purpose handler naming its purpose, to
/// confirm it has done its own half.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-003a, PRIV-RIGHT-005b, PRIV-RIGHT-001a and PRIV-CONS-008. The row
/// carries each subscriber's confirmation, so what is outstanding is readable from the
/// moment the transaction commits, and a delivery that has spent its retry budget is
/// <c>failed</c> rather than quietly done.
/// </remarks>
internal sealed class Delivery
{
    private readonly HashSet<string> _confirmed;

    private Delivery(
        DeliveryId id,
        SubjectId subject,
        SubjectEventKind kind,
        DateTimeOffset raisedAt,
        bool restricted,
        ErasureReason reason,
        string? purpose,
        DeliveryChange? change,
        HashSet<string> confirmed)
    {
        Id = id;
        Subject = subject;
        Kind = kind;
        RaisedAt = raisedAt;
        Restricted = restricted;
        Reason = reason;
        Purpose = purpose;
        Change = change;
        Status = ErasureStatus.AwaitingSubscribers;
        NextAttemptAt = raisedAt;
        _confirmed = confirmed;
    }

    /// <summary>
    /// What the row is held under.
    /// </summary>
    public DeliveryId Id { get; }

    /// <summary>
    /// Whose fact it is.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// Which fact it is.
    /// </summary>
    public SubjectEventKind Kind { get; }

    /// <summary>
    /// When the fact became true.
    /// </summary>
    public DateTimeOffset RaisedAt { get; }

    /// <summary>
    /// Whether the subject is now restricted, where the fact is a restriction change.
    /// </summary>
    public bool Restricted { get; }

    /// <summary>
    /// Why the erasure happened, where the fact is an erasure.
    /// </summary>
    public ErasureReason Reason { get; }

    /// <summary>
    /// The purpose the change concerns, where the fact is a consent or an objection
    /// change, and nothing otherwise.
    /// </summary>
    public string? Purpose { get; }

    /// <summary>
    /// The change the event announces, where the fact is a consent or an objection
    /// change, and nothing otherwise.
    /// </summary>
    public DeliveryChange? Change { get; }

    /// <summary>
    /// How far the subscribers have got.
    /// </summary>
    public ErasureStatus Status { get; private set; }

    /// <summary>
    /// How many delivery attempts have been made across the subscribers.
    /// </summary>
    public int Attempts { get; private set; }

    /// <summary>
    /// When the next attempt is due.
    /// </summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    /// <summary>
    /// Which subscribers have confirmed.
    /// </summary>
    public IReadOnlySet<string> Confirmed => _confirmed;

    /// <summary>
    /// The key a subscriber recognises a repeat by, which is the delivery and never
    /// the attempt: a retry of the same fact carries the key the first attempt did.
    /// </summary>
    public string IdempotencyKey => Id.Key();

    /// <summary>
    /// A subject event just made true, with every subscriber outstanding.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="kind">Which fact it is.</param>
    /// <param name="raisedAt">When it became true.</param>
    /// <param name="restricted">Whether the subject is now restricted.</param>
    /// <param name="reason">Why the erasure happened, where it is one.</param>
    /// <returns>The delivery.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The kind is a consent or an objection change.</exception>
    public static Delivery Of(
        SubjectId subject,
        SubjectEventKind kind,
        DateTimeOffset raisedAt,
        bool restricted = false,
        ErasureReason reason = ErasureReason.ErasureRequest)
    {
        if (kind is SubjectEventKind.ConsentChanged or SubjectEventKind.ObjectionChanged)
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "A consent or an objection change names its purpose and its change.");
        }

        return new(DeliveryId.Of(raisedAt), subject, kind, raisedAt, restricted, reason, null, null, []);
    }

    /// <summary>
    /// A consent change just made true, with every purpose handler naming its purpose
    /// outstanding.
    /// </summary>
    /// <param name="subject">Whose consent it is.</param>
    /// <param name="purpose">The purpose it was given for.</param>
    /// <param name="change">What happened to it.</param>
    /// <param name="raisedAt">When it became true.</param>
    /// <returns>The delivery.</returns>
    /// <exception cref="ArgumentException">The purpose is absent or blank.</exception>
    public static Delivery OfConsent(
        SubjectId subject,
        string purpose,
        ConsentChange change,
        DateTimeOffset raisedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        DeliveryChange announced = change switch
        {
            ConsentChange.Granted => DeliveryChange.Granted,
            ConsentChange.Withdrawn => DeliveryChange.Withdrawn,
            _ => DeliveryChange.Superseded,
        };

        return new(
            DeliveryId.Of(raisedAt),
            subject,
            SubjectEventKind.ConsentChanged,
            raisedAt,
            restricted: false,
            ErasureReason.ErasureRequest,
            purpose,
            announced,
            []);
    }

    /// <summary>
    /// An objection change just made true, with every purpose handler naming its
    /// purpose outstanding.
    /// </summary>
    /// <param name="subject">Whose objection it is.</param>
    /// <param name="purpose">The purpose it concerns.</param>
    /// <param name="objecting">Whether an objection was recorded rather than withdrawn.</param>
    /// <param name="raisedAt">When it became true.</param>
    /// <returns>The delivery.</returns>
    /// <exception cref="ArgumentException">The purpose is absent or blank.</exception>
    public static Delivery OfObjection(
        SubjectId subject,
        string purpose,
        bool objecting,
        DateTimeOffset raisedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        return new(
            DeliveryId.Of(raisedAt),
            subject,
            SubjectEventKind.ObjectionChanged,
            raisedAt,
            restricted: false,
            ErasureReason.ErasureRequest,
            purpose,
            objecting ? DeliveryChange.Recorded : DeliveryChange.Withdrawn,
            []);
    }

    /// <summary>
    /// The delivery as the row already holds it.
    /// </summary>
    /// <param name="id">What it is held under.</param>
    /// <param name="subject">Whose it is.</param>
    /// <param name="kind">Which fact it is.</param>
    /// <param name="raisedAt">When it became true.</param>
    /// <param name="restricted">Whether the subject is now restricted.</param>
    /// <param name="reason">Why the erasure happened, where it is one.</param>
    /// <param name="purpose">The purpose a consent or an objection change concerns.</param>
    /// <param name="change">The change a consent or an objection change announces.</param>
    /// <param name="status">How far the subscribers have got.</param>
    /// <param name="attempts">How many attempts have been made.</param>
    /// <param name="nextAttemptAt">When the next attempt is due.</param>
    /// <param name="confirmed">Which subscribers have confirmed.</param>
    /// <returns>The delivery.</returns>
    /// <exception cref="ArgumentNullException">The confirmations are absent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The attempts are negative.</exception>
    public static Delivery Existing(
        DeliveryId id,
        SubjectId subject,
        SubjectEventKind kind,
        DateTimeOffset raisedAt,
        bool restricted,
        ErasureReason reason,
        string? purpose,
        DeliveryChange? change,
        ErasureStatus status,
        int attempts,
        DateTimeOffset nextAttemptAt,
        IReadOnlyCollection<string> confirmed)
    {
        ArgumentNullException.ThrowIfNull(confirmed);
        ArgumentOutOfRangeException.ThrowIfNegative(attempts);

        return new Delivery(
            id,
            subject,
            kind,
            raisedAt,
            restricted,
            reason,
            purpose,
            change,
            new HashSet<string>(confirmed, StringComparer.Ordinal))
        {
            Status = status,
            Attempts = attempts,
            NextAttemptAt = nextAttemptAt,
        };
    }

    /// <summary>
    /// The event the delivery carries, rebuilt from the row so that a retry days
    /// later raises what the transaction raised.
    /// </summary>
    /// <returns>The event.</returns>
    /// <exception cref="InvalidOperationException">The row carries a kind that is not one of the three subject events.</exception>
    public SubjectEvent Raised() => Kind switch
    {
        SubjectEventKind.ErasureRequested =>
            new ErasureRequested(RaisedAt, IdempotencyKey, Reason) { Subject = Subject },
        SubjectEventKind.RestrictionChanged =>
            new RestrictionChanged(RaisedAt, IdempotencyKey, Restricted) { Subject = Subject },
        SubjectEventKind.TakedownExecuted =>
            new TakedownExecuted(RaisedAt, IdempotencyKey) { Subject = Subject },
        _ => throw new InvalidOperationException("The delivery carries no subject event."),
    };

    /// <summary>
    /// The consent change the delivery carries, rebuilt from the row under the
    /// delivery's own key, so a retry is the same change to the handler.
    /// </summary>
    /// <returns>The event.</returns>
    /// <exception cref="InvalidOperationException">The row carries no consent change.</exception>
    public ConsentChanged RaisedConsent() =>
        Kind is SubjectEventKind.ConsentChanged && Purpose is string purpose
            ? new ConsentChanged(
                RaisedAt,
                IdempotencyKey,
                purpose,
                Change switch
                {
                    DeliveryChange.Granted => ConsentChange.Granted,
                    DeliveryChange.Withdrawn => ConsentChange.Withdrawn,
                    DeliveryChange.Superseded => ConsentChange.Superseded,
                    _ => throw new InvalidOperationException("The row carries no consent change."),
                })
            {
                Subject = Subject,
            }
            : throw new InvalidOperationException("The row carries no consent change.");

    /// <summary>
    /// The objection change the delivery carries, rebuilt from the row under the
    /// delivery's own key, so a retry is the same change to the handler.
    /// </summary>
    /// <returns>The event.</returns>
    /// <exception cref="InvalidOperationException">The row carries no objection change.</exception>
    public ObjectionChanged RaisedObjection() =>
        Kind is SubjectEventKind.ObjectionChanged && Purpose is string purpose
            ? new ObjectionChanged(
                RaisedAt,
                IdempotencyKey,
                purpose,
                Change switch
                {
                    DeliveryChange.Recorded => true,
                    DeliveryChange.Withdrawn => false,
                    _ => throw new InvalidOperationException("The row carries no objection change."),
                })
            {
                Subject = Subject,
            }
            : throw new InvalidOperationException("The row carries no objection change.");

    /// <summary>
    /// Records that one subscriber has done its work. A repeat changes nothing,
    /// which is what makes a redelivery safe.
    /// </summary>
    /// <param name="subscriber">What the subscriber is called.</param>
    public void Confirm(string subscriber) => _confirmed.Add(subscriber);

    /// <summary>
    /// Counts one attempt across the subscribers and schedules the next, the delay
    /// growing by the factor per attempt with full jitter.
    /// </summary>
    /// <param name="at">When the attempt was made.</param>
    /// <param name="initial">The first retry delay.</param>
    /// <param name="factor">The multiplier per further attempt.</param>
    /// <param name="jitter">A fraction of the computed delay, in [0, 1].</param>
    /// <exception cref="ArgumentOutOfRangeException">The jitter is outside its range.</exception>
    public void Attempted(DateTimeOffset at, TimeSpan initial, decimal factor, double jitter)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(jitter);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(jitter, 1);

        Attempts++;

        double backoff = initial.TotalSeconds * Math.Pow((double)factor, Attempts - 1);

        NextAttemptAt = at.AddSeconds(backoff * jitter);
    }

    /// <summary>
    /// Whether every subscriber a request waits for has confirmed.
    /// </summary>
    /// <param name="required">What the required subscribers are called.</param>
    /// <returns>Whether the delivery is done.</returns>
    /// <exception cref="ArgumentNullException">The names are absent.</exception>
    public bool Satisfies(IReadOnlyCollection<string> required)
    {
        ArgumentNullException.ThrowIfNull(required);

        foreach (string subscriber in required)
        {
            if (!_confirmed.Contains(subscriber))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Every required subscriber confirmed.
    /// </summary>
    /// <exception cref="InvalidOperationException">The delivery is no longer outstanding.</exception>
    public void Complete()
    {
        Outstanding();

        Status = ErasureStatus.Complete;
    }

    /// <summary>
    /// The retry budget is spent, so the manual completion path is pending.
    /// </summary>
    /// <exception cref="InvalidOperationException">The delivery is no longer outstanding.</exception>
    public void Fail()
    {
        Outstanding();

        Status = ErasureStatus.Failed;
    }

    /// <summary>
    /// The manual completion path ran and closed the delivery.
    /// </summary>
    /// <exception cref="InvalidOperationException">The delivery never failed.</exception>
    public void CompleteManually()
    {
        if (Status is not ErasureStatus.Failed)
        {
            throw new InvalidOperationException(
                "The manual completion path closes a delivery that failed.");
        }

        Status = ErasureStatus.Complete;
    }

    private void Outstanding()
    {
        if (Status is not ErasureStatus.AwaitingSubscribers)
        {
            throw new InvalidOperationException("The delivery is no longer awaiting subscribers.");
        }
    }
}
