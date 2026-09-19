using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What an evaluation decided.
/// </summary>
/// <remarks>Implements AUTHZ-GATE-004.</remarks>
public enum AccessOutcome
{
    /// <summary>
    /// Nothing conferred the permission, or something took it away.
    /// </summary>
    [JsonStringEnumMemberName("denied")]
    Denied = 0,

    /// <summary>
    /// A live grant conferred the permission and nothing took it away.
    /// </summary>
    [JsonStringEnumMemberName("allowed")]
    Allowed = 1,
}
