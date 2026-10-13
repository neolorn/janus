using System.Text.Json.Serialization;

namespace Janus.Hosting.Credentials;

/// <summary>
/// The part of a social provider a round trip could not reach or read, spelled as the
/// details of the degradation it raises spell it.
/// </summary>
/// <remarks>Implements IDN-LIFE-012 AC6 and chapter 10 section 5.23.</remarks>
internal enum ProviderPart
{
    /// <summary>The provider's discovery document.</summary>
    [JsonStringEnumMemberName("discovery")]
    Discovery = 0,

    /// <summary>The keys the provider publishes.</summary>
    [JsonStringEnumMemberName("keys")]
    Keys = 1,

    /// <summary>The endpoint a code is traded at.</summary>
    [JsonStringEnumMemberName("token")]
    Token = 2,
}
