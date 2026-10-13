using System;
using System.Collections.Generic;
using System.Threading;

namespace Janus.Hosting.Bff;

/// <summary>
/// The requests this instance admitted from each source, and from the /48 that encloses
/// each IPv6 source, in the minute ending now.
/// </summary>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements BFF-ORDER-001 stage 4. A source is the address the connection arrived on
/// after the proxies the host names to the framework as trusted, in the form
/// AUTH-ABUSE-001 counts by: an IPv4 address, or the /64 of an IPv6 address, since one
/// host holds every address of its subnet. A site holds a /48 or a /56 (RFC 6177), so
/// each IPv6 source's enclosing /48 is counted too, under its own limit, and walking a
/// site's subnets is bounded. A deployment that names no trusted proxy to the framework
/// is one source. The counts are held in this instance's memory, so a source or a /48
/// already over its limit is refused without a read of any store, and each instance of
/// a deployment admits both limits on its own: a deployment of several instances sets
/// each key to its share. The window slides: a request is counted for the minute after
/// it and no longer, and a key over its limit is admitted again at the instant enough
/// of its requests have left the window. A key that saw nothing for a whole window is
/// forgotten when the next one begins, so what is held is what the last two windows
/// brought and nothing older.
/// </remarks>
internal sealed class SourceAdmissions(TimeProvider time)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();

    private Dictionary<string, Admitted> _current = new(StringComparer.Ordinal);

    private Dictionary<string, Admitted> _previous = new(StringComparer.Ordinal);

    private DateTimeOffset _began = time.GetUtcNow();

    /// <summary>
    /// The instant a source, or a /48, that went over its limit is admitted again, while
    /// that instant has not come.
    /// </summary>
    /// <param name="source">Where the request came from, or the /48 that encloses it.</param>
    /// <returns>The instant, or nothing where it is not held.</returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public DateTimeOffset? HeldUntil(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        DateTimeOffset now = time.GetUtcNow();

        lock (_gate)
        {
            return Found(source, now) is { } admitted && admitted.Lifts > now ? admitted.Lifts : null;
        }
    }

    /// <summary>
    /// Admits one request from a source that is within its limit for the minute ending
    /// now, and whose enclosing /48 is within its own, and counts it against both.
    /// </summary>
    /// <param name="source">Where the request came from.</param>
    /// <param name="limit">How many requests a source is admitted with a minute.</param>
    /// <param name="site">The /48 that encloses an IPv6 source, or nothing for any other.</param>
    /// <param name="siteLimit">How many requests a /48 is admitted with a minute.</param>
    /// <returns>
    /// Nothing where the request is admitted, or the instant the source or its /48 is
    /// admitted again where it is not. A request refused at the /48 leaves no entry for
    /// its source.
    /// </returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public DateTimeOffset? Admit(string source, int limit, string? site, int siteLimit)
    {
        ArgumentNullException.ThrowIfNull(source);

        DateTimeOffset now = time.GetUtcNow();

        lock (_gate)
        {
            Admitted? enclosing = site is null ? null : Found(site, now) ?? Added(site);

            if (enclosing is not null && Over(enclosing, siteLimit, now) is DateTimeOffset held)
            {
                return held;
            }

            Admitted admitted = Found(source, now) ?? Added(source);

            if (Over(admitted, limit, now) is DateTimeOffset lifts)
            {
                return lifts;
            }

            admitted.Counted.Add(now);
            enclosing?.Counted.Add(now);

            return null;
        }
    }

    // Forgets what has left the window, and where what is left reaches the limit, holds
    // the key until enough of it has left to bring it under. A limit below one admits
    // nothing, and the key is asked about again once a whole window has passed.
    private static DateTimeOffset? Over(Admitted admitted, int limit, DateTimeOffset now)
    {
        List<DateTimeOffset> counted = admitted.Counted;

        int left = counted.FindIndex(at => at + Window > now);
        counted.RemoveRange(0, left < 0 ? counted.Count : left);

        if (counted.Count < limit)
        {
            return null;
        }

        admitted.Lifts = limit > 0 ? counted[counted.Count - limit] + Window : now + Window;

        return admitted.Lifts;
    }

    // Finds a key in this window or the last one, bringing it into this one. A new
    // window forgets the one before the last, whose every request has left the window
    // and whose every hold has lifted.
    private Admitted? Found(string source, DateTimeOffset now)
    {
        if (now - _began >= Window)
        {
            _previous = now - _began < Window + Window ? _current : new(StringComparer.Ordinal);
            _current = new(StringComparer.Ordinal);
            _began = now;
        }

        if (_current.TryGetValue(source, out Admitted? admitted))
        {
            return admitted;
        }

        if (_previous.Remove(source, out admitted))
        {
            _current.Add(source, admitted);
        }

        return admitted;
    }

    private Admitted Added(string source)
    {
        var admitted = new Admitted();

        _current.Add(source, admitted);

        return admitted;
    }

    // What one key was admitted with, oldest first, and when its hold lifts.
    private sealed class Admitted
    {
        public List<DateTimeOffset> Counted { get; } = [];

        public DateTimeOffset Lifts { get; set; } = DateTimeOffset.MinValue;
    }
}
