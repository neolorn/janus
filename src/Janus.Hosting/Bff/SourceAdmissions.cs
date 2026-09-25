using System;
using System.Collections.Generic;
using System.Threading;

namespace Janus.Hosting.Bff;

/// <summary>
/// The requests this instance admitted from each source address in the minute ending
/// now.
/// </summary>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements BFF-ORDER-001 stage 4. The counts are held in this instance's memory, so
/// a source already over its limit is refused without a read of any store, and each
/// instance of a deployment admits the limit on its own. The window slides: a request
/// is counted for the minute after it and no longer, and a source over its limit is
/// admitted again at the instant enough of its requests have left the window. A source
/// that sent nothing for a whole window is forgotten when the next one begins, so what
/// is held is what the last two windows brought and nothing older.
/// </remarks>
internal sealed class SourceAdmissions(TimeProvider time)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly Lock _gate = new();

    private Dictionary<string, Admitted> _current = new(StringComparer.Ordinal);

    private Dictionary<string, Admitted> _previous = new(StringComparer.Ordinal);

    private DateTimeOffset _began = time.GetUtcNow();

    /// <summary>
    /// The instant a source that went over its limit is admitted again, while that
    /// instant has not come.
    /// </summary>
    /// <param name="source">Where the request came from.</param>
    /// <returns>The instant, or nothing where the source is not held.</returns>
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
    /// now, and counts it.
    /// </summary>
    /// <param name="source">Where the request came from.</param>
    /// <param name="limit">How many requests a source is admitted with a minute.</param>
    /// <returns>
    /// Nothing where the request is admitted, or the instant the source is admitted
    /// again where it is not.
    /// </returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public DateTimeOffset? Admit(string source, int limit)
    {
        ArgumentNullException.ThrowIfNull(source);

        DateTimeOffset now = time.GetUtcNow();

        lock (_gate)
        {
            Admitted admitted = Found(source, now) ?? Added(source);
            List<DateTimeOffset> counted = admitted.Counted;

            int left = counted.FindIndex(at => at + Window > now);
            counted.RemoveRange(0, left < 0 ? counted.Count : left);

            if (counted.Count < limit)
            {
                counted.Add(now);

                return null;
            }

            // The source is admitted again when enough of its requests have left the
            // window to bring it under the limit. A limit below one admits nothing, and
            // the source is asked about again once a whole window has passed.
            admitted.Lifts = limit > 0 ? counted[counted.Count - limit] + Window : now + Window;

            return admitted.Lifts;
        }
    }

    // Finds a source in this window or the last one, bringing it into this one. A new
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

    // What one source was admitted with, oldest first, and when its hold lifts.
    private sealed class Admitted
    {
        public List<DateTimeOffset> Counted { get; } = [];

        public DateTimeOffset Lifts { get; set; } = DateTimeOffset.MinValue;
    }
}
