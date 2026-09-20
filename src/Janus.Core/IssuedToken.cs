using System;

namespace Janus.Core;

/// <summary>
/// What an exchange at the token endpoint settled: whose session the access token is
/// minted from, what it covers, and the refresh token where one was issued.
/// </summary>
/// <param name="Subject">Whose account the token is for.</param>
/// <param name="Session">The session record the token is minted from.</param>
/// <param name="Scope">What the token covers, space separated.</param>
/// <param name="Nonce">What the identity token is to carry back, where the request carried one.</param>
/// <param name="Lifetime">How long the access token lives.</param>
/// <param name="RefreshToken">
/// The rotated refresh token, and nothing for a browser application's own layer,
/// which holds no token after the exchange (AUTH-OIDC-002).
/// </param>
/// <remarks>
/// Implements AUTH-OIDC-002, AUTH-OIDC-003 and AUTH-OIDC-004. The token stands on the
/// session record, so revoking the record stops the next validation that reaches it.
/// </remarks>
public sealed record IssuedToken(
    SubjectId Subject,
    SessionId Session,
    string Scope,
    string? Nonce,
    TimeSpan Lifetime,
    string? RefreshToken);
