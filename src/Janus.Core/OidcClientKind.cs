using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Which of the three kinds of client a registry entry is, which decides what the token
/// endpoint will issue it.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-001, AUTH-OIDC-002 and AUTH-SESS-012 (D-252). A browser
/// application's own layer uses the code flow once to establish its session and holds no
/// token afterwards, so it is never issued a refresh token; a protocol client is. A
/// public application holds no secret and receives one access token for the mail server,
/// and no refresh token.
/// </remarks>
public enum OidcClientKind
{
    /// <summary>
    /// A browser application's own layer, which exchanges one code for the session it
    /// keeps and discards what the exchange returned.
    /// </summary>
    [JsonStringEnumMemberName("browser-application")]
    BrowserApplication = 0,

    /// <summary>
    /// A client that holds tokens of its own and refreshes them, which is what the
    /// mail server is.
    /// </summary>
    [JsonStringEnumMemberName("protocol")]
    Protocol = 1,

    /// <summary>
    /// A browser application that can hold no secret, which signs a person in to the
    /// mail server: it may send its authorization request directly, and receives an
    /// access token for the mail server alone and never a refresh token (D-252).
    /// </summary>
    [JsonStringEnumMemberName("public-application")]
    PublicApplication = 2,
}
