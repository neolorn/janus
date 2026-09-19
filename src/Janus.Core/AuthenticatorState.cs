using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Where an enrolled authenticator stands: usable, reported lost and on its way out,
/// or gone.
/// </summary>
/// <remarks>
/// Implements chapter 10 section 5.3a and AUTH-RECOV-007. A reported-lost
/// authenticator is suspended rather than removed at once, so an attacker who
/// reported it cannot strip a factor from an account in one step.
/// </remarks>
public enum AuthenticatorState
{
    /// <summary>
    /// Enrolled and usable.
    /// </summary>
    [JsonStringEnumMemberName("active")]
    Active = 0,

    /// <summary>
    /// Reported lost and refused, awaiting the end of the notified window.
    /// </summary>
    [JsonStringEnumMemberName("suspended")]
    Suspended = 1,

    /// <summary>
    /// Gone: the window elapsed, or the account was recovered.
    /// </summary>
    [JsonStringEnumMemberName("invalidated")]
    Invalidated = 2,
}
