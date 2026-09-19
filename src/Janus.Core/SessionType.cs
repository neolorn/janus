using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Which of the three kinds a session is. All three derive from one record, and
/// revoking that record ends every one of them.
/// </summary>
/// <remarks>Implements chapter 10 section 5.2, AUTH-SESS-004.</remarks>
public enum SessionType
{
    /// <summary>
    /// The session the authentication application holds, which is what makes silent
    /// single sign-on between applications possible.
    /// </summary>
    [JsonStringEnumMemberName("auth")]
    Auth = 0,

    /// <summary>
    /// The session an application's own layer holds for a person.
    /// </summary>
    [JsonStringEnumMemberName("per-app")]
    PerApp = 1,

    /// <summary>
    /// A token a protocol client holds.
    /// </summary>
    [JsonStringEnumMemberName("oidc-token")]
    OidcToken = 2,
}
