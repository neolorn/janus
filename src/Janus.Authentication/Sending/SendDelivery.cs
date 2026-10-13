using System;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// One message the library has admitted, written in the transaction that undertook it
/// and carried afterwards.
/// </summary>
/// <param name="Id">What the row is held under.</param>
/// <param name="RecordedAt">When the send was admitted.</param>
/// <param name="Requested">What was undertaken, with what the restrictions judge it on.</param>
/// <param name="Reference">The reference drawn for it, which it is counted and carried under.</param>
/// <remarks>
/// Implements D-022, AUTH-ABUSE-004, INF-BG-001 and IDN-PRIN-003. A message is a working
/// artefact: the row exists so that a message admitted inside a transaction is not lost
/// with the process that took it, and it is removed once the handler has taken it, or
/// once it fails for good.
/// </remarks>
internal sealed record SendDelivery(
    SendDeliveryId Id,
    DateTimeOffset RecordedAt,
    OutboundMessage Requested,
    SendReference Reference)
{
    /// <summary>
    /// How many attempts have been made without the handler taking the message.
    /// </summary>
    public int Attempts { get; init; }

    /// <summary>
    /// When a pass may next claim it. A message whose immediate attempt follows the
    /// commit is written due the first retry delay after its admission, with no jitter,
    /// so that no pass takes it before that attempt, which claims it whatever this
    /// instant, has had its chance (CONV-DESIGN-003, D-188).
    /// </summary>
    public DateTimeOffset NextAttemptAt { get; init; }

    /// <summary>
    /// The admitted message as the handler that carries it receives it, with none of the
    /// restrictions' inputs.
    /// </summary>
    public SendRequest Admitted =>
        new(Requested.Destination, Requested.Message, Requested.Language, Reference)
        {
            Subject = Requested.Subject,
            Values = Requested.Values,
        };

    /// <summary>
    /// A message just admitted.
    /// </summary>
    /// <param name="message">What was undertaken.</param>
    /// <param name="reference">The reference drawn for it.</param>
    /// <param name="recordedAt">When it was admitted.</param>
    /// <param name="held">How long after its admission the row is due for a pass.</param>
    /// <returns>The delivery.</returns>
    /// <exception cref="ArgumentNullException">The message is absent.</exception>
    public static SendDelivery Of(
        OutboundMessage message,
        SendReference reference,
        DateTimeOffset recordedAt,
        TimeSpan held)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new SendDelivery(SendDeliveryId.Of(recordedAt), recordedAt, message, reference)
        {
            NextAttemptAt = recordedAt + held,
        };
    }

    /// <summary>
    /// The message once an attempt has left it untaken, its next attempt scheduled with
    /// the delay growing by the factor per attempt, with full jitter.
    /// </summary>
    /// <param name="at">When the attempt was made.</param>
    /// <param name="initial">The first retry delay.</param>
    /// <param name="factor">The multiplier per further attempt.</param>
    /// <param name="jitter">A fraction of the computed delay, in [0, 1].</param>
    /// <returns>The message.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The jitter is outside its range.</exception>
    public SendDelivery Refused(DateTimeOffset at, TimeSpan initial, decimal factor, double jitter)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(jitter);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(jitter, 1);

        int attempts = Attempts + 1;
        double backoff = initial.TotalSeconds * Math.Pow((double)factor, attempts - 1);

        return this with { Attempts = attempts, NextAttemptAt = at.AddSeconds(backoff * jitter) };
    }
}
