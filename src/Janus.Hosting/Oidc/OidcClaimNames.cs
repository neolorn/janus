namespace Janus.Hosting.Oidc;

/// <summary>
/// The claims the library carries on a principal between the stages of one request and
/// puts in no token it issues.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-012 and AUTH-OIDC-003. A code and a refresh token are the
/// library's own opaque values: the server carries them from the stage that issued
/// them to the stage that writes them out, and a claim with no destination reaches no
/// access token and no identity token.
/// </remarks>
internal static class OidcClaimNames
{
    /// <summary>The one-time code the authorization endpoint issued.</summary>
    public const string Code = "janus_code";

    /// <summary>The refresh token the token endpoint issued.</summary>
    public const string RefreshToken = "janus_refresh";

    /// <summary>The session record the tokens stand on.</summary>
    public const string Session = "sid";
}
