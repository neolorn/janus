namespace Janus.Core;

/// <summary>
/// A protocol client presenting a refresh token at the token endpoint.
/// </summary>
/// <param name="RefreshToken">The token it holds.</param>
/// <param name="ClientId">Which client is presenting it.</param>
/// <param name="ClientSecret">What it authenticates with.</param>
/// <remarks>
/// Implements AUTH-OIDC-003. Every use rotates the token, and a token presented twice
/// revokes everything derived from the same session.
/// </remarks>
public sealed record RefreshRedemption(string RefreshToken, string ClientId, string ClientSecret);
