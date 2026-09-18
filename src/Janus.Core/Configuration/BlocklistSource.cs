using System.Text.Json.Serialization;

namespace Janus.Core.Configuration;

/// <summary>
/// Where the leaked-password list comes from.
/// </summary>
/// <remarks>Implements chapter 10 section 4.2, AUTH-PASS-004.</remarks>
public enum BlocklistSource
{
    /// <summary>
    /// The range API of a compromised-password service, queried by prefix.
    /// </summary>
    [JsonStringEnumMemberName("rangeApi")]
    RangeApi = 0,

    /// <summary>
    /// The offline copy the deployment falls back to when the range API is
    /// unreachable.
    /// </summary>
    [JsonStringEnumMemberName("offline")]
    Offline = 1,

    /// <summary>
    /// A corpus the deployment hosts itself.
    /// </summary>
    [JsonStringEnumMemberName("selfHosted")]
    SelfHosted = 2,
}
