using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// What the named restrictions decide about one send, and what counts as loosening
/// one of them.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-004, OPS-CFG-002 and chapter 10 sections 5.14 to 5.16.
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
    public static bool Applies(Restriction restriction, SendRequest request)
    {
        ArgumentNullException.ThrowIfNull(restriction);
        ArgumentNullException.ThrowIfNull(request);

        if (restriction.Purpose is not RestrictionPurpose.Any
            && restriction.Purpose != request.Purpose)
        {
            return false;
        }

        if (restriction.Key is not RestrictionKeyKind.Destination)
        {
            return true;
        }

        // An alert answers to deduplication and to nothing else: an attacker able to
        // drain the operator address would otherwise silence the alerting, which is
        // the blackout two channels exist to prevent (OPS-ALERT-002, OPS-ALERT-003).
        // A security notice to an address its owner holds sits outside the destination
        // restrictions for the same reason, and answers to the one whose purpose names
        // notifications (AUTH-ABUSE-004).
        return !request.IsAlert
            && (!IsNoticeToHolder(request) || restriction.Purpose is not RestrictionPurpose.Any);
    }

    /// <summary>
    /// Whether one send is a security notice to an address an account already holds,
    /// which is outside the destination restrictions so that an attacker who drains a
    /// bucket cannot silence the notice that says so (AUTH-ABUSE-004).
    /// </summary>
    /// <param name="request">The send.</param>
    /// <returns>Whether it is such a notice.</returns>
    /// <exception cref="ArgumentNullException">The send is absent.</exception>
    public static bool IsNoticeToHolder(SendRequest request)
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
    /// When one bucket lets the next send through.
    /// </summary>
    /// <param name="bucket">The bucket.</param>
    /// <param name="sends">The times counted against the key.</param>
    /// <param name="now">The clock.</param>
    /// <returns>
    /// When the bucket lifts, or nothing where it is not exceeded and the send passes.
    /// </returns>
    /// <exception cref="ArgumentNullException">The bucket or the times are absent.</exception>
    public static DateTimeOffset? Lift(
        Bucket bucket,
        IReadOnlyList<DateTimeOffset> sends,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(bucket);
        ArgumentNullException.ThrowIfNull(sends);

        DateTimeOffset opened = Opened(bucket, now);
        List<DateTimeOffset> counting = [.. sends.Where(sent => sent >= opened).Order()];

        if (counting.Count < bucket.Maximum)
        {
            return null;
        }

        // A sliding bucket lifts when enough of what it counts has aged out of the
        // interval; a fixed one lifts when its interval turns over (section 5.16).
        return bucket.Window is BucketWindow.Sliding
            ? counting[counting.Count - bucket.Maximum] + bucket.Interval
            : opened + bucket.Interval;
    }

    /// <summary>
    /// When one restriction lets the next send through, over all of its buckets.
    /// </summary>
    /// <param name="restriction">The restriction.</param>
    /// <param name="sends">The times counted against the key.</param>
    /// <param name="now">The clock.</param>
    /// <returns>
    /// The earliest time an exceeded bucket lifts, or nothing where none is exceeded.
    /// </returns>
    /// <exception cref="ArgumentNullException">The restriction or the times are absent.</exception>
    public static DateTimeOffset? Lift(
        Restriction restriction,
        IReadOnlyList<DateTimeOffset> sends,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(restriction);

        return restriction.Buckets
            .Select(bucket => Lift(bucket, sends, now))
            .Where(lift => lift is not null)
            .Order()
            .FirstOrDefault();
    }

    /// <summary>
    /// Whether replacing one restriction with another lets more through than before,
    /// which needs a written reason and raises a Normal alert (OPS-CFG-002).
    /// </summary>
    /// <param name="before">What stood, or nothing where the restriction is new.</param>
    /// <param name="after">What replaces it, or nothing where it is deleted.</param>
    /// <returns>Whether the change is a loosening.</returns>
    public static bool IsLoosening(Restriction? before, Restriction? after)
    {
        if (before is null)
        {
            return false;
        }

        if (after is null)
        {
            return true;
        }

        if (after.Key != before.Key
            || after.HostKeyName != before.HostKeyName
            || (after.Purpose is not RestrictionPurpose.Any && after.Purpose != before.Purpose))
        {
            return true;
        }

        return before.Buckets.Any(bucket => !after.Buckets.Any(kept => AtLeastAsStrict(kept, bucket)));
    }

    private static bool AtLeastAsStrict(Bucket kept, Bucket bucket) =>
        kept.Maximum <= bucket.Maximum
        && kept.Interval >= bucket.Interval
        && (kept.Window == bucket.Window || kept.Window is BucketWindow.Sliding);

    private static DateTimeOffset Opened(Bucket bucket, DateTimeOffset now) =>
        bucket.Window is BucketWindow.Sliding
            ? now - bucket.Interval
            : new DateTimeOffset(now.UtcTicks - (now.UtcTicks % bucket.Interval.Ticks), TimeSpan.Zero);
}
