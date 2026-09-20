namespace Janus.Core;

/// <summary>
/// A client presenting a code at the token endpoint.
/// </summary>
/// <param name="Code">The code it was issued.</param>
/// <param name="ClientId">Which client is presenting it.</param>
/// <param name="ClientSecret">What it authenticates with.</param>
/// <param name="Redirect">The destination the code was issued against.</param>
/// <param name="CodeVerifier">The verifier of the challenge the code was issued against.</param>
/// <remarks>Implements AUTH-SESS-012 and AUTH-OIDC-001.</remarks>
public sealed record CodeRedemption(
    string Code,
    string ClientId,
    string ClientSecret,
    string Redirect,
    string CodeVerifier);
