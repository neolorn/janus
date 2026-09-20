namespace Janus.Core;

/// <summary>
/// What a client is asking the authorization endpoint for.
/// </summary>
/// <param name="ClientId">Which client is asking.</param>
/// <param name="Redirect">Where it says the code should be returned.</param>
/// <param name="Scope">What it is asking for, space separated.</param>
/// <param name="CodeChallenge">The challenge the verifier will be judged against.</param>
/// <param name="CodeChallengeMethod">How the challenge was computed.</param>
/// <param name="Nonce">What the identity token is to carry back, where it carries one.</param>
/// <param name="Silent">
/// Whether the request carried <c>prompt=none</c>, which is a client asking whether a
/// session already exists rather than asking for one to be established.
/// </param>
/// <remarks>Implements AUTH-SESS-012, AUTH-OIDC-001 and API-REDIR-001.</remarks>
public sealed record AuthorizationIntent(
    string ClientId,
    string Redirect,
    string Scope,
    string CodeChallenge,
    string CodeChallengeMethod,
    string? Nonce,
    bool Silent);
