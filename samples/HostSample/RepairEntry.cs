using System;
using System.Collections.Generic;

namespace HostSample;

/// <summary>
/// A repair as a workshop's list shows it, with what its reader may do to it.
/// </summary>
/// <param name="Ticket">The ticket on the kite's tag.</param>
/// <param name="Kite">What the kite is.</param>
/// <param name="ContactName">The name to contact the customer by, or nothing where the customer's key is erased.</param>
/// <param name="ReleasedAt">When the kite was released, or nothing while it is in the workshop.</param>
/// <param name="Can">The actions the reader's grants confer on it.</param>
/// <param name="Requires">What each of those actions still requires.</param>
public sealed record RepairEntry(
    string Ticket,
    string Kite,
    string? ContactName,
    DateTimeOffset? ReleasedAt,
    IReadOnlyList<string> Can,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Requires);
