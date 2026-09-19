using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// How far an erasure's host-side work has got. It never describes the library's own
/// steps, which have committed if the row exists.
/// </summary>
/// <remarks>Implements IDN-LIFE-003b and chapter 10 section 5.12.</remarks>
public enum ErasureStatus
{
    /// <summary>
    /// Required subscribers have not all confirmed.
    /// </summary>
    [JsonStringEnumMemberName("awaiting-subscribers")]
    AwaitingSubscribers = 0,

    /// <summary>
    /// Every required subscriber confirmed.
    /// </summary>
    [JsonStringEnumMemberName("complete")]
    Complete = 1,

    /// <summary>
    /// A subscriber exhausted its retries and the manual completion path is pending.
    /// </summary>
    [JsonStringEnumMemberName("failed")]
    Failed = 2,
}
