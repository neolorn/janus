using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// One message the library has undertaken to send, written in the transaction that
/// made it necessary and carried afterwards.
/// </summary>
/// <param name="Id">What the row is held under.</param>
/// <param name="RecordedAt">When the send was recorded.</param>
/// <param name="Requested">What is to be sent.</param>
/// <remarks>
/// Implements D-022, INF-BG-001 and IDN-PRIN-003. A message is a working artefact: the
/// row exists so that a message undertaken inside a transaction is not lost with the
/// process that took it, and it is removed once a transport has taken it in every
/// language it goes out in, or once its retry budget is spent.
/// </remarks>
internal sealed record SendDelivery(
    SendDeliveryId Id,
    DateTimeOffset RecordedAt,
    SendRequest Requested)
{
    private static readonly IReadOnlyList<string> None = [];

    /// <summary>
    /// How many attempts have been made without a transport taking every language.
    /// </summary>
    public int Attempts { get; init; }

    /// <summary>
    /// When the publisher next carries it. A message just undertaken is held back by
    /// the first retry delay, so the publisher does not carry a message the path that
    /// undertook it is carrying at that moment.
    /// </summary>
    public DateTimeOffset NextAttemptAt { get; init; }

    /// <summary>
    /// The languages a transport has already taken it in, which no retry sends again.
    /// </summary>
    public IReadOnlyList<string> Taken { get; init; } = None;

    /// <summary>
    /// A message just undertaken.
    /// </summary>
    /// <param name="request">What is to be sent.</param>
    /// <param name="recordedAt">When it was undertaken.</param>
    /// <param name="held">How long the publisher leaves it to the path that undertook it.</param>
    /// <returns>The delivery.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static SendDelivery Of(SendRequest request, DateTimeOffset recordedAt, TimeSpan held)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new SendDelivery(SendDeliveryId.Of(recordedAt), recordedAt, request)
        {
            NextAttemptAt = recordedAt + held,
        };
    }

    /// <summary>
    /// The message once a transport has taken it in further languages.
    /// </summary>
    /// <param name="languages">The languages just taken.</param>
    /// <returns>The message.</returns>
    public SendDelivery Carried(IEnumerable<string> languages) =>
        this with { Taken = [.. Taken.Union(languages, StringComparer.Ordinal)] };

    /// <summary>
    /// The message once an attempt has left a language untaken, its next attempt
    /// scheduled with the delay growing by the factor per attempt, with full jitter.
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
