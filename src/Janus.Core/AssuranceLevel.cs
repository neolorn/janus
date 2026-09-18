using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// The assurance a session asserts, and the assurance an account can reach with the
/// factors it holds.
/// </summary>
/// <remarks>Implements chapter 10 section 5.4, AUTH-SESS-002, AUTH-STEP-006.</remarks>
public enum AssuranceLevel
{
    /// <summary>
    /// No asserted level: signed in on a social provider's word, below
    /// <see cref="Aal1"/> wherever a tier is needed.
    /// </summary>
    [JsonStringEnumMemberName("delegated")]
    Delegated = 0,

    /// <summary>
    /// One factor.
    /// </summary>
    [JsonStringEnumMemberName("aal1")]
    Aal1 = 1,

    /// <summary>
    /// Two factors, or one that stands alone.
    /// </summary>
    [JsonStringEnumMemberName("aal2")]
    Aal2 = 2,

    /// <summary>
    /// Hardware-bound, verifier-impersonation-resistant.
    /// </summary>
    [JsonStringEnumMemberName("aal3")]
    Aal3 = 3,
}
