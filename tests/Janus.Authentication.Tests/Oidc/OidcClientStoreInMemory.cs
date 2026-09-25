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
    private readonly Dictionary<string, RegisteredClient> _clients = new(StringComparer.Ordinal);

    /// <summary>
    /// The clients as they were registered, with what each one's secret hashes to and
    /// the secret it replaced, for a caller that reads the registry as the protocol
    /// server reads it.
    /// </summary>
    public IReadOnlyList<RegisteredClient> Registered => [.. _clients.Values];

    /// <inheritdoc/>
    public ValueTask<OidcClient?> FindAsync(string clientId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            _clients.TryGetValue(clientId, out RegisteredClient? held)
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
        DateTimeOffset replacedUntil,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(fingerprint);

        _clients[client.ClientId] = _clients.TryGetValue(client.ClientId, out RegisteredClient? held)
            && !held.Secret.AsSpan().SequenceEqual(fingerprint)
                ? new RegisteredClient(client, fingerprint, held.Secret, replacedUntil)
                : new RegisteredClient(client, fingerprint, held?.Previous, held?.PreviousUntil);

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// One client as the registry holds it.
/// </summary>
/// <param name="Client">The client.</param>
/// <param name="Secret">What its secret hashes to.</param>
/// <param name="Previous">What the secret it replaced hashes to, where one was.</param>
/// <param name="PreviousUntil">Until when the replaced secret is taken.</param>
internal sealed record RegisteredClient(
    OidcClient Client,
    byte[] Secret,
    byte[]? Previous,
    DateTimeOffset? PreviousUntil);
