using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// How urgent an alert is. Email carries every condition; SMS carries the high ones.
/// </summary>
/// <remarks>Implements OPS-ALERT-001, OPS-ALERT-003.</remarks>
public enum AlertSeverity
{
    /// <summary>
    /// Reported, and read in the ordinary course.
    /// </summary>
    [JsonStringEnumMemberName("normal")]
    Normal = 0,

    /// <summary>
    /// Reported on every channel, because someone has to look now.
    /// </summary>
    [JsonStringEnumMemberName("high")]
    High = 1,
}
