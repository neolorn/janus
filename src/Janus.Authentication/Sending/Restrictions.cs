using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// What the named restrictions decide about one send, and what counts as loosening
/// one of them.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-004, OPS-CFG-002 and chapter 10 sections 5.14 to 5.16 and
/// 5.15a.
/// Every part of the decision is a function of the restriction, the times already
/// counted and the clock, so it is decided without reaching storage.
/// </remarks>
internal static class Restrictions
{
    /// <summary>
    /// Whether one restriction governs one send.
    /// </summary>
    /// <param name="restriction">The restriction.</param>
    /// <param name="request">The send.</param>
    /// <returns>Whether its buckets are consulted.</returns>
    /// <exception cref="ArgumentNullException">Either is absent.</exception>
    public static bool Applies(Restriction restriction, OutboundMessage request)
    {
        ArgumentNullException.ThrowIfNull(restriction);
        ArgumentNullException.ThrowIfNull(request);

        // An alert answers to deduplication and to nothing else: an attacker able to
        // spend any bucket an alert counted under would otherwise silence the
        // alerting, which is the blackout two channels exist to prevent (OPS-ALERT-002,
        // OPS-ALERT-003).
        if (request.IsAlert)
        {
            return false;
        }

        if (restriction.Channel is not RestrictionChannel.Any && !Carries(restriction.Channel, request.Kind))
        {
            return false;
        }

        // A security notice to an address its owner holds answers to the restrictions
        // whose purpose names notifications and to no other, so draining a bucket
        // cannot silence the notice that says so (AUTH-ABUSE-004).
        if (IsNoticeToHolder(request))
        {
            return restriction.Purpose is RestrictionPurpose.Notification;
        }

        return restriction.Purpose is RestrictionPurpose.Any || restriction.Purpose == request.Purpose;
    }

    /// <summary>
    /// Whether one send is a security notice to an address an account already holds,
    /// which is outside the destination restrictions so that an attacker who drains a
    /// bucket cannot silence the notice that says so (AUTH-ABUSE-004).
    /// </summary>
    /// <param name="request">The send.</param>
    /// <returns>Whether it is such a notice.</returns>
    /// <exception cref="ArgumentNullException">The send is absent.</exception>
    public static bool IsNoticeToHolder(OutboundMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return MessageChannels.Notices.Contains(request.Message) && request.Subject is not null;
    }

    /// <summary>
    /// How long a key's times still decide something, after which its record holds
    /// nothing and is deleted.
    /// </summary>
    /// <param name="restriction">The restriction.</param>
    /// <returns>The longest interval any of its buckets counts over.</returns>
    /// <exception cref="ArgumentNullException">The restriction is absent.</exception>
    public static TimeSpan Retain(Restriction restriction)
    {
        ArgumentNullException.ThrowIfNull(restriction);

        return restriction.Buckets.Max(bucket => bucket.Interval);
    }

    /// <summary>
    /// When one bucket has room for a send of so many messages.
    /// </summary>
    /// <param name="bucket">The bucket.</param>
    /// <param name="sends">The times counted against the key.</param>
    /// <param name="now">The clock.</param>
    /// <param name="weight">How many messages the send is, each of which counts.</param>
    /// <returns>
    /// When the bucket lifts, or nothing where it has room for all of them and the send
    /// passes.
    /// </returns>
    /// <exception cref="ArgumentNullException">The bucket or the times are absent.</exception>
    public static DateTimeOffset? Lift(
        Bucket bucket,
        IReadOnlyList<DateTimeOffset> sends,
        DateTimeOffset now,
        int weight)
    {
        ArgumentNullException.ThrowIfNull(bucket);
        ArgumentNullException.ThrowIfNull(sends);

        DateTimeOffset opened = Opened(bucket, now);
        List<DateTimeOffset> counting = [.. sends.Where(sent => sent >= opened).Order()];

        int over = counting.Count + weight - bucket.Maximum;

        if (over <= 0)
        {
            return null;
        }

        // A fixed bucket lifts when its interval turns over (section 5.16). A sliding
        // one lifts when as many of the times it counts as the send is over by have
        // aged out of the interval; a send wider than the bucket waits out the whole
        // of what is counted.
        if (bucket.Window is not BucketWindow.Sliding)
        {
            return opened + bucket.Interval;
        }

        return counting.Count == 0
            ? now + bucket.Interval
            : counting[Math.Min(over, counting.Count) - 1] + bucket.Interval;
    }

    /// <summary>
    /// When one restriction has room for a send of so many messages, over all of its
    /// buckets.
    /// </summary>
    /// <param name="restriction">The restriction.</param>
    /// <param name="sends">The times counted against the key.</param>
    /// <param name="now">The clock.</param>
    /// <param name="weight">How many messages the send is, each of which counts.</param>
    /// <returns>
    /// The earliest time an exceeded bucket lifts, or nothing where none is exceeded.
    /// </returns>
    /// <exception cref="ArgumentNullException">The restriction or the times are absent.</exception>
    public static DateTimeOffset? Lift(
        Restriction restriction,
        IReadOnlyList<DateTimeOffset> sends,
        DateTimeOffset now,
        int weight)
    {
        ArgumentNullException.ThrowIfNull(restriction);

        return restriction.Buckets
            .Select(bucket => Lift(bucket, sends, now, weight))
            .Where(lift => lift is not null)
            .Order()
            .FirstOrDefault();
    }

    /// <summary>
    /// How many of a send's messages one restriction has no room for, which is the
    /// credit the send spends where credit carries it through.
    /// </summary>
    /// <param name="restriction">The restriction.</param>
    /// <param name="sends">The times counted against the key.</param>
    /// <param name="now">The clock.</param>
    /// <param name="weight">How many messages the send is.</param>
    /// <returns>The messages beyond the room of its fullest bucket, at most the weight.</returns>
    /// <exception cref="ArgumentNullException">The restriction or the times are absent.</exception>
    public static int Over(
        Restriction restriction,
        IReadOnlyList<DateTimeOffset> sends,
        DateTimeOffset now,
        int weight)
    {
        ArgumentNullException.ThrowIfNull(restriction);
        ArgumentNullException.ThrowIfNull(sends);

        return restriction.Buckets.Max(bucket =>
        {
            DateTimeOffset opened = Opened(bucket, now);

            return Math.Clamp(sends.Count(sent => sent >= opened) + weight - bucket.Maximum, 0, weight);
        });
    }

    /// <summary>
    /// Whether replacing one restriction with another lets more through than before,
    /// which needs a written reason and raises a Normal alert (OPS-CFG-002).
    /// </summary>
    /// <param name="before">What stood, or nothing where the restriction is new.</param>
    /// <param name="after">What replaces it, or nothing where it is deleted.</param>
    /// <returns>Whether the change is a loosening.</returns>
    public static bool IsLoosening(Restriction? before, Restriction? after) =>
        RestrictionSetSetting.Loosens(before, after);

    private static bool Carries(RestrictionChannel channel, SendKind kind) =>
        channel is RestrictionChannel.Sms ? kind is SendKind.Sms : kind is SendKind.Email;

    private static DateTimeOffset Opened(Bucket bucket, DateTimeOffset now) =>
        bucket.Window is BucketWindow.Sliding
            ? now - bucket.Interval
            : new DateTimeOffset(now.UtcTicks - (now.UtcTicks % bucket.Interval.Ticks), TimeSpan.Zero);
}
