using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The kind of send a restriction applies to.
/// </summary>
/// <remarks>Implements chapter 10 section 5.15, AUTH-ABUSE-004.</remarks>
public enum RestrictionPurpose
{
    /// <summary>
    /// Every send except a security notice to an existing holder, which is governed
    /// by the notification restriction alone.
    /// </summary>
    [JsonStringEnumMemberName("any")]
    Any = 0,

    /// <summary>
    /// Verifying an address the person gave.
    /// </summary>
    [JsonStringEnumMemberName("verification")]
    Verification = 1,

    /// <summary>
    /// Signing in.
    /// </summary>
    [JsonStringEnumMemberName("signin")]
    SignIn = 2,

    /// <summary>
    /// A second step.
    /// </summary>
    [JsonStringEnumMemberName("secondfactor")]
    SecondFactor = 3,

    /// <summary>
    /// Telling the person something happened, including a recovery link.
    /// </summary>
    [JsonStringEnumMemberName("notification")]
    Notification = 4,
}
