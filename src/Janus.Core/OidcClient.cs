using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One manually registered client, as the registry holds it.
/// </summary>
/// <param name="ClientId">What the client calls itself in a request.</param>
/// <param name="Name">What the deployment calls it.</param>
/// <param name="Kind">Which of the two kinds it is.</param>
/// <param name="Redirect">
/// The one destination a code is returned to, matched exactly and never by prefix or
/// pattern (API-REDIR-001).
/// </param>
/// <param name="Scopes">What it may ask for.</param>
/// <remarks>
/// Implements AUTH-OIDC-001, API-REDIR-001 and API-REDIR-002. Registration is manual:
/// nothing in the library creates a client from a request, and no dynamic
/// registration endpoint exists.
/// </remarks>
public sealed record OidcClient(
    string ClientId,
    string Name,
    OidcClientKind Kind,
    string Redirect,
    IReadOnlyList<string> Scopes);
