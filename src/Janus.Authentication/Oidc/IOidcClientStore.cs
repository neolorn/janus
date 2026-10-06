using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Oidc;

/// <summary>
/// The registry of manually registered clients. Nothing in the library writes to it
/// from a request: a client is registered by the deployment, from the server, and by
/// nothing else (AUTH-OIDC-001).
/// </summary>
internal interface IOidcClientStore
{
    /// <summary>
    /// The client a request named.
    /// </summary>
    /// <param name="clientId">What the request called it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The client, or nothing where the registry holds none.</returns>
    ValueTask<OidcClient?> FindAsync(string clientId, CancellationToken cancellationToken);

    /// <summary>
    /// Every registered client, which is what a deployment's own listing reads.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The clients.</returns>
    ValueTask<IReadOnlyList<OidcClient>> AllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Registers a client with the first secret the library drew for it, which the
    /// deployment does and a request never does.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="secret">Its secret as it presents it, which the store wraps.</param>
    /// <param name="issuedAt">When the secret was drawn.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of registering it.</returns>
    ValueTask AddAsync(
        OidcClient client,
        [NeverLogged] ReadOnlyMemory<byte> secret,
        DateTimeOffset issuedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Carries a change to a registered client's name, kind, destination and scopes,
    /// leaving its secret as it stands (OPS-SEC-002).
    /// </summary>
    /// <param name="client">The client as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of carrying it.</returns>
    ValueTask RecordAsync(OidcClient client, CancellationToken cancellationToken);

    /// <summary>
    /// A registered client's secrets: the current one and, where one was replaced, the
    /// one it replaced and until when that is taken.
    /// </summary>
    /// <param name="clientId">Which client.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The secrets, which the caller clears, or nothing where no client is registered.</returns>
    ValueTask<RegisteredSecret?> SecretAsync(string clientId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a client's secret where it still stands as it was read: the current one
    /// becomes the replaced one, taken until the instant given, and the new one current.
    /// </summary>
    /// <param name="clientId">Which client.</param>
    /// <param name="issuedAt">When the secret read was issued, which the replacement is conditional on.</param>
    /// <param name="secret">The new secret as the client presents it, which the store wraps.</param>
    /// <param name="now">When the new one is issued.</param>
    /// <param name="replacedUntil">Until when the replaced secret is taken.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether this call replaced it; another that read the same secret may have first.</returns>
    ValueTask<bool> ReplaceSecretAsync(
        string clientId,
        DateTimeOffset issuedAt,
        [NeverLogged] ReadOnlyMemory<byte> secret,
        DateTimeOffset now,
        DateTimeOffset replacedUntil,
        CancellationToken cancellationToken);
}
