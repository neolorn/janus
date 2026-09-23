using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// The registered clients, held in memory.
/// </summary>
internal sealed class OidcClientStoreInMemory : IOidcClientStore
{
    private readonly Dictionary<string, (OidcClient Client, byte[] Secret)> _clients =
        new(StringComparer.Ordinal);

    /// <summary>
    /// The clients as they were registered, with what each one's secret hashes to,
    /// for a caller that reads the registry as the protocol server reads it.
    /// </summary>
    public IReadOnlyList<(OidcClient Client, byte[] Secret)> Registered => [.. _clients.Values];

    /// <inheritdoc/>
    public ValueTask<OidcClient?> FindAsync(string clientId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            _clients.TryGetValue(clientId, out (OidcClient Client, byte[] Secret) held)
                ? held.Client
                : null);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<OidcClient>> AllAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<OidcClient>>(
            [.. _clients.Values.Select(held => held.Client).OrderBy(client => client.ClientId, StringComparer.Ordinal)]);

    /// <inheritdoc/>
    public ValueTask RecordAsync(
        OidcClient client,
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        _clients[client.ClientId] = (client, fingerprint);

        return ValueTask.CompletedTask;
    }
}
