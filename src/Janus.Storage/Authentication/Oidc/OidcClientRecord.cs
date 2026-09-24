using Janus.Core;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The <c>oidc_clients</c> row: one manually registered client.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-001 and API-REDIR-001. The secret is held as what it hashes
/// to, so a dump of the table authenticates nothing, and the destination is held as
/// one exact string because that is how it is matched.
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

    /// <summary>The <c>secret</c> column: what its secret hashes to.</summary>
    [NeverLogged]
    public byte[] Secret { get; set; } = [];

    /// <summary>The <c>scopes</c> column: what it may ask for.</summary>
    public string[] Scopes { get; set; } = [];
}
