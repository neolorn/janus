using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Which of the two kinds of client a registry entry is, which decides what the token
/// endpoint will issue it.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-001, AUTH-OIDC-002 and AUTH-SESS-012. A browser application's
/// own layer uses the code flow once to establish its session and holds no token
/// afterwards, so it is never issued a refresh token; a protocol client is.
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
}
