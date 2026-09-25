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
    /// Registers a client or carries a change to one, which the deployment does and a
    /// request never does.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="fingerprint">What its secret hashes to.</param>
    /// <param name="replacedUntil">
    /// Until when a secret this one replaces stays accepted beside it (OPS-SEC-002).
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of registering it.</returns>
    ValueTask RecordAsync(
        OidcClient client,
        byte[] fingerprint,
        DateTimeOffset replacedUntil,
        CancellationToken cancellationToken);
}
