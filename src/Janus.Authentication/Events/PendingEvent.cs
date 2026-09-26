using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Events;

/// <summary>
/// One emitted event, written in the transaction that made it true and kept until
/// every consumer registered for its kind has taken it.
/// </summary>
/// <remarks>
/// Implements LIB-API-001, CONV-DESIGN-002 and IDN-LIFE-003a. The row carries each
/// consumer that has taken the event, so a retry offers it only to the ones that have
/// not, and an event whose budget is spent is failed rather than quietly dropped.
/// </remarks>
internal sealed class PendingEvent
{
    private readonly HashSet<string> _taken;

    private PendingEvent(
        PendingEventId id,
        DomainEvent raised,
        int attempts,
        DateTimeOffset nextAttemptAt,
        HashSet<string> taken,
        DateTimeOffset? publishedAt,
        DateTimeOffset? failedAt)
    {
        Id = id;
        Raised = raised;
        Attempts = attempts;
        NextAttemptAt = nextAttemptAt;
        PublishedAt = publishedAt;
        FailedAt = failedAt;
        _taken = taken;
    }

    /// <summary>
    /// What the row is held under.
    /// </summary>
    public PendingEventId Id { get; }

    /// <summary>
    /// The event as it was raised.
    /// </summary>
    public DomainEvent Raised { get; }

    /// <summary>
    /// How many passes have offered it without every consumer taking it.
    /// </summary>
    public int Attempts { get; private set; }

    /// <summary>
    /// When the next pass is due to offer it.
    /// </summary>
    public DateTimeOffset NextAttemptAt { get; private set; }

    /// <summary>
    /// The consumers that have taken it, by name.
    /// </summary>
    public IReadOnlySet<string> Taken => _taken;

    /// <summary>
    /// When the last consumer took it, which marks the row.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>
    /// When its budget was spent with a consumer still refusing it.
    /// </summary>
    public DateTimeOffset? FailedAt { get; private set; }

    /// <summary>
    /// An event just raised, due at once.
    /// </summary>
    /// <param name="raised">The event.</param>
    /// <returns>The row to write.</returns>
    /// <exception cref="ArgumentNullException">The event is absent.</exception>
    public static PendingEvent Of(DomainEvent raised)
    {
        ArgumentNullException.ThrowIfNull(raised);

        return new PendingEvent(
            PendingEventId.Of(raised.RaisedAt),
            raised,
            attempts: 0,
            raised.RaisedAt,
            new HashSet<string>(StringComparer.Ordinal),
            publishedAt: null,
            failedAt: null);
    }

    /// <summary>
    /// An event as its row holds it.
    /// </summary>
    /// <param name="id">What the row is held under.</param>
    /// <param name="raised">The event, read back.</param>
    /// <param name="attempts">How many passes have offered it.</param>
    /// <param name="nextAttemptAt">When the next pass is due.</param>
    /// <param name="taken">The consumers that have taken it.</param>
    /// <param name="publishedAt">When the last consumer took it.</param>
    /// <param name="failedAt">When its budget was spent.</param>
    /// <returns>The event.</returns>
    /// <exception cref="ArgumentNullException">The event or the consumers are absent.</exception>
    public static PendingEvent Existing(
        PendingEventId id,
        DomainEvent raised,
        int attempts,
        DateTimeOffset nextAttemptAt,
        IEnumerable<string> taken,
        DateTimeOffset? publishedAt,
        DateTimeOffset? failedAt)
    {
        ArgumentNullException.ThrowIfNull(raised);
        ArgumentNullException.ThrowIfNull(taken);

        return new PendingEvent(
            id,
            raised,
            attempts,
            nextAttemptAt,
            new HashSet<string>(taken, StringComparer.Ordinal),
            publishedAt,
            failedAt);
    }

    /// <summary>
    /// Records that one consumer has taken the event.
    /// </summary>
    /// <param name="consumer">The consumer's name.</param>
    public void Take(string consumer) => _taken.Add(consumer);

    /// <summary>
    /// Marks the row: every consumer registered for the event has taken it.
    /// </summary>
    /// <param name="at">When the last one did.</param>
    public void Published(DateTimeOffset at) => PublishedAt = at;

    /// <summary>
    /// Counts a pass in which a consumer did not take the event and schedules the next,
    /// the delay growing by the factor per attempt with full jitter, until the budget
    /// is spent.
    /// </summary>
    /// <param name="at">When the pass was made.</param>
    /// <param name="initial">The first retry delay.</param>
    /// <param name="factor">The multiplier per further attempt.</param>
    /// <param name="maximum">How many attempts the budget holds.</param>
    /// <param name="jitter">A fraction of the computed delay, in [0, 1].</param>
    /// <returns>Whether this attempt spent the budget.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The jitter is outside its range.</exception>
    public bool Refused(DateTimeOffset at, TimeSpan initial, decimal factor, int maximum, double jitter)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(jitter);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(jitter, 1);

        Attempts++;

        if (Attempts >= maximum)
        {
            FailedAt = at;

            return true;
        }

        double backoff = initial.TotalSeconds * Math.Pow((double)factor, Attempts - 1);

        NextAttemptAt = at.AddSeconds(backoff * jitter);

        return false;
    }
}
