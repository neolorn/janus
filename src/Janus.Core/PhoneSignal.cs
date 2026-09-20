using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What a deployment can learn about a number before a text is used to authenticate
/// with: whether the carrier reports a recent change of SIM or of network.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-002b and LIB-HOST-001. Where a deployment registers nothing
/// to answer this, the absence is what is recorded: the library knows no gateway and
/// invents no answer for one.
/// </remarks>
public enum PhoneSignal
{
    /// <summary>
    /// The provider holds nothing about this number.
    /// </summary>
    [JsonStringEnumMemberName("none")]
    None = 0,

    /// <summary>
    /// The provider holds something about this number and it reports no change.
    /// </summary>
    [JsonStringEnumMemberName("clear")]
    Clear = 1,

    /// <summary>
    /// The provider reports a change of SIM or of network recent enough to matter.
    /// </summary>
    [JsonStringEnumMemberName("risk")]
    Risk = 2,
}
