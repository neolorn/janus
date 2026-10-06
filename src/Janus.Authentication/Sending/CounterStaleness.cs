using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// Before when a counter's newest time decides nothing, for the destinations and for
/// every other key apart.
/// </summary>
/// <param name="Destinations">
/// A destination's record whose newest time is older than this holds nothing.
/// </param>
/// <param name="Keys">
/// An account, source, global or host key's record whose newest time is older than this
/// holds nothing.
/// </param>
/// <remarks>
/// Implements AUTH-ABUSE-004 AC6 and PRIV-RET-005 AC2. Each is the longest interval of
/// the restrictions now declared over that table's key kinds, so a destination is kept
/// no longer than a destination restriction needs it, whatever a restriction on another
/// key counts over, and a shortened interval reaches what was already counted.
/// </remarks>
internal sealed record CounterStaleness(DateTimeOffset Destinations, DateTimeOffset Keys)
{
    /// <summary>
    /// The staleness the restrictions now declared set at one instant.
    /// </summary>
    /// <param name="declared">The restrictions in force.</param>
    /// <param name="now">The clock.</param>
    /// <returns>The two instants.</returns>
    /// <exception cref="ArgumentNullException">The restrictions are absent.</exception>
    public static CounterStaleness Of(IReadOnlyList<Restriction> declared, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(declared);

        return new CounterStaleness(
            now - Longest(declared.Where(restriction => restriction.Key is RestrictionKeyKind.Destination)),
            now - Longest(declared.Where(restriction => restriction.Key is not RestrictionKeyKind.Destination)));
    }

    // A table no restriction counts under keeps nothing, so its longest is none.
    private static TimeSpan Longest(IEnumerable<Restriction> restrictions) =>
        restrictions.Select(Restrictions.Retain).DefaultIfEmpty(TimeSpan.Zero).Max();
}
