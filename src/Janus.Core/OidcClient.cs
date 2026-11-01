using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One manually registered client, as the registry holds it.
/// </summary>
/// <param name="ClientId">What the client calls itself in a request.</param>
/// <param name="Name">What the deployment calls it.</param>
/// <param name="Kind">Which of the three kinds it is.</param>
/// <param name="Redirect">
/// The destination a code is returned to, matched exactly and never by prefix or
/// pattern (API-REDIR-001); a public application's first.
/// </param>
/// <param name="Scopes">What it may ask for.</param>
/// <remarks>
/// Implements AUTH-OIDC-001, API-REDIR-001 and API-REDIR-002 (D-252). Registration is
/// manual: nothing in the library creates a client from a request, and no dynamic
/// registration endpoint exists.
/// </remarks>
public sealed record OidcClient(
    string ClientId,
    string Name,
    OidcClientKind Kind,
    string Redirect,
    IReadOnlyList<string> Scopes)
{
    /// <summary>
    /// The destinations a public application is returned to beside
    /// <see cref="Redirect"/>, one for each further return address it was registered
    /// with, each matched exactly; empty for every other kind (D-252).
    /// </summary>
    public IReadOnlyList<string> OtherRedirects { get; init; } = [];

    /// <summary>
    /// The client a public application's access token is for, named as its <c>aud</c>, or
    /// nothing where its tokens name the client itself; nothing for every other kind
    /// (INT-MAIL-004, D-252).
    /// </summary>
    public string? Audience { get; init; }
}
