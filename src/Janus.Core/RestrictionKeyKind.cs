using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What a sending restriction counts against.
/// </summary>
/// <remarks>Implements chapter 10 section 5.14, AUTH-ABUSE-004.</remarks>
public enum RestrictionKeyKind
{
    /// <summary>
    /// An HMAC of the canonical address the send is going to.
    /// </summary>
    [JsonStringEnumMemberName("destination")]
    Destination = 0,

    /// <summary>
    /// The account the send belongs to.
    /// </summary>
    [JsonStringEnumMemberName("account")]
    Account = 1,

    /// <summary>
    /// The source the send was asked for from.
    /// </summary>
    [JsonStringEnumMemberName("source")]
    Source = 2,

    /// <summary>
    /// Every send, counted together.
    /// </summary>
    [JsonStringEnumMemberName("global")]
    Global = 3,

    /// <summary>
    /// A per-send value a host-registered supplier returns, named after the colon.
    /// </summary>
    [JsonStringEnumMemberName("host")]
    Host = 4,
}
