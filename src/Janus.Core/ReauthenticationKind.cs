using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// What an expired session asks for: one factor bound to the session secret it still
/// holds, or a full authentication.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-005 and chapter 10 section 1.2. The single-factor restore is
/// written for a short gap and applies only where the policy requires AAL2: a
/// customer whose session lapsed after three months of silence signs in normally.
/// </remarks>
public enum ReauthenticationKind
{
    /// <summary>
    /// A full authentication.
    /// </summary>
    [JsonStringEnumMemberName("full")]
    Full = 0,

    /// <summary>
    /// One factor that may begin an authentication, presented beside the session
    /// secret.
    /// </summary>
    [JsonStringEnumMemberName("single-factor")]
    SingleFactor = 1,
}
