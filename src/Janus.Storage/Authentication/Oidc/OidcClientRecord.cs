using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The <c>oidc_clients</c> row: one manually registered client.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-001, API-REDIR-001, OPS-SEC-001 and OPS-SEC-002. The secret is
/// held wrapped under the deployment's data key, so a dump of the table authenticates
/// nothing, and the destination is held as one exact string because that is how it is
/// matched. A secret that replaced another leaves the one it replaced accepted for the
/// overlap, and no longer.
/// </remarks>
internal sealed class OidcClientRecord
{
    /// <summary>The <c>client_id</c> column: what the client calls itself.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>The <c>name</c> column: what the deployment calls it.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The <c>kind</c> column: which of the two kinds it is.</summary>
    public OidcClientKind Kind { get; set; }

    /// <summary>The <c>redirect</c> column: the one destination a code is returned to.</summary>
    public string Redirect { get; set; } = string.Empty;

    /// <summary>The <c>secret</c> column: its secret, wrapped.</summary>
    [NeverLogged]
    public byte[] Secret { get; set; } = [];

    /// <summary>
    /// The <c>secret_issued_at</c> column: when the secret was drawn, which its rotation
    /// is read from.
    /// </summary>
    public DateTimeOffset SecretIssuedAt { get; set; }

    /// <summary>
    /// The <c>previous_secret</c> column: the secret the current one replaced, wrapped,
    /// or nothing where none was replaced.
    /// </summary>
    [NeverLogged]
    public byte[]? PreviousSecret { get; set; }

    /// <summary>
    /// The <c>previous_secret_until</c> column: the instant the replaced secret stops
    /// being accepted.
    /// </summary>
    public DateTimeOffset? PreviousSecretUntil { get; set; }

    /// <summary>The <c>scopes</c> column: what it may ask for.</summary>
    public string[] Scopes { get; set; } = [];
}
