using System;

namespace Janus.Privacy.Exports;

/// <summary>
/// Where the export's assembly keeps what an export contributor threw.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-003, BFF-ERR-002 AC2, CONV-LOG-003 and CONV-DESIGN-007 (D-210,
/// D-211). The entry every other fault the library catches is kept as is internal to
/// <c>Janus.Hosting</c>, so <c>Janus.Hosting</c>, which registers the contributors,
/// keeps it; the error the export answers names the contributor and holds no fault.
/// </remarks>
internal interface IFaultEntry
{
    /// <summary>
    /// Keeps one contributor's fault in the log, by its type and frames and never its
    /// message.
    /// </summary>
    /// <param name="contributor">The name of the contributor that threw.</param>
    /// <param name="fault">What it threw.</param>
    void Keep(string contributor, Exception fault);
}
