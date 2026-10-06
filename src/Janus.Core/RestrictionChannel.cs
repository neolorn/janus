using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The channel a restriction governs sends on.
/// </summary>
/// <remarks>Implements chapter 10 section 5.15a, AUTH-ABUSE-004.</remarks>
public enum RestrictionChannel
{
    /// <summary>
    /// Sends on either channel, which a restriction naming none governs.
    /// </summary>
    [JsonStringEnumMemberName("any")]
    Any = 0,

    /// <summary>
    /// Text messages.
    /// </summary>
    [JsonStringEnumMemberName("sms")]
    Sms = 1,

    /// <summary>
    /// Mail.
    /// </summary>
    [JsonStringEnumMemberName("email")]
    Email = 2,
}
