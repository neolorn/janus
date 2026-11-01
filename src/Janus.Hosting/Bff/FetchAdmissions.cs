using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Janus.Hosting.Bff;

/// <summary>
/// The fetches of the provider's discovery document and key set this instance admitted
/// from each source, in fixed windows of a minute.
/// </summary>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements BFF-MACH-001 AC4 (D-241). The counts are held in this instance's memory
/// and nowhere else, so a fetch writes nothing, which is why the two routes left the
/// browser profile; each instance of a deployment admits the limit on its own. A
/// source's window begins with its first fetch and lasts a minute, and the counts of
/// windows that have ended are forgotten once enough sources have been seen.
/// </remarks>
internal sealed class FetchAdmissions(TimeProvider time)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    // Past this many sources held, the windows that have ended are let go.
    private const int Held = 4096;

    private readonly Lock _gate = new();

    private readonly Dictionary<(string Route, string Source), (DateTimeOffset Began, int Count)> _windows = [];

    /// <summary>
    /// Admits one fetch of a route from a source within its limit for the window it
    /// falls in.
    /// </summary>
    /// <param name="route">Which route is fetched.</param>
    /// <param name="source">Where the fetch came from.</param>
    /// <param name="limit">How many fetches a window admits.</param>
    /// <returns>Nothing where it is admitted, or how long until the source is admitted again.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public TimeSpan? Admit(string route, string source, int limit)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(source);

        DateTimeOffset now = time.GetUtcNow();

        lock (_gate)
        {
            if (_windows.Count >= Held)
            {
                foreach ((string Route, string Source) ended in _windows
                    .Where(window => window.Value.Began + Window <= now)
                    .Select(window => window.Key)
                    .ToList())
                {
                    _ = _windows.Remove(ended);
                }
            }

            (DateTimeOffset began, int count) = _windows.TryGetValue((route, source), out (DateTimeOffset Began, int Count) held)
                && held.Began + Window > now
                ? held
                : (now, 0);

            if (count >= limit)
            {
                return began + Window - now;
            }

            _windows[(route, source)] = (began, count + 1);

            return null;
        }
    }
}
