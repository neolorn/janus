using Janus.Core;

namespace HostSample;

/// <summary>
/// A fact of the workshop's own data: whose kite a repair is. The repair's owner holds
/// the repair-owner role on it for as long as the fact holds.
/// </summary>
internal sealed class RepairOwner
{
    /// <summary>
    /// The repair, under its ticket.
    /// </summary>
    public required string RepairId { get; init; }

    /// <summary>
    /// The customer whose kite it is.
    /// </summary>
    public required SubjectId Owner { get; init; }
}
