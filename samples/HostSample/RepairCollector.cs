using Janus.Core;

namespace HostSample;

/// <summary>
/// A fact of the workshop's own data: who the owner named to collect the repaired kite.
/// The collector holds the repair-collector role on the repair for as long as the fact
/// holds.
/// </summary>
internal sealed class RepairCollector
{
    /// <summary>
    /// The repair, under its ticket.
    /// </summary>
    public required string RepairId { get; init; }

    /// <summary>
    /// The person named to collect the kite.
    /// </summary>
    public required SubjectId Collector { get; init; }
}
