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

    private readonly Lock _gate = new();

    /// <summary>
    /// The clients as they were registered, with their secrets, for a caller that reads
    /// the registry as the protocol server reads it.
    /// </summary>
    public IReadOnlyList<RegisteredClient> Registered
    {
        get
        {
            lock (_gate)
            {
                return [.. _clients.Values];
            }
        }
    }

    /// <summary>
    /// How many times a secret was replaced.
    /// </summary>
    public int Replacements { get; private set; }

    /// <summary>
    /// What happens once, before the next replacement is attempted: another process's
    /// work, run between this one's read of the secret and its replacement of it.
    /// </summary>
    public Func<CancellationToken, ValueTask>? BeforeReplacement { get; set; }

    /// <inheritdoc/>
    public ValueTask<OidcClient?> FindAsync(string clientId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(
                _clients.TryGetValue(clientId, out RegisteredClient? held)
                    ? held.Client
                    : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<OidcClient>> AllAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<IReadOnlyList<OidcClient>>(
                [.. _clients.Values.Select(held => held.Client).OrderBy(client => client.ClientId, StringComparer.Ordinal)]);
        }
    }

    /// <inheritdoc/>
    public ValueTask AddAsync(
        OidcClient client,
        [NeverLogged] ReadOnlyMemory<byte> secret,
        DateTimeOffset issuedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        lock (_gate)
        {
            _clients.Add(client.ClientId, new RegisteredClient(client, secret.ToArray(), issuedAt, null, null));
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(OidcClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        lock (_gate)
        {
            RegisteredClient held = _clients[client.ClientId];

            _clients[client.ClientId] = held with { Client = client };
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<RegisteredSecret?> SecretAsync(string clientId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(
                _clients.TryGetValue(clientId, out RegisteredClient? held)
                    ? new RegisteredSecret(
                        [.. held.Secret],
                        held.IssuedAt,
                        held.Previous is byte[] previous ? [.. previous] : null,
                        held.PreviousUntil)
                    : null);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ReplaceSecretAsync(
        string clientId,
        DateTimeOffset issuedAt,
        [NeverLogged] ReadOnlyMemory<byte> secret,
        DateTimeOffset now,
        DateTimeOffset replacedUntil,
        CancellationToken cancellationToken)
    {
        if (BeforeReplacement is Func<CancellationToken, ValueTask> first)
        {
            BeforeReplacement = null;

            await first(cancellationToken);
        }

        lock (_gate)
        {
            if (!_clients.TryGetValue(clientId, out RegisteredClient? held) || held.IssuedAt != issuedAt)
            {
                return false;
            }

            _clients[clientId] = new RegisteredClient(held.Client, secret.ToArray(), now, held.Secret, replacedUntil);
            Replacements++;

            return true;
        }
    }
}

/// <summary>
/// One client as the registry holds it.
/// </summary>
/// <param name="Client">The client.</param>
/// <param name="Secret">Its secret as it presents it.</param>
/// <param name="IssuedAt">When the secret was drawn.</param>
/// <param name="Previous">The secret it replaced, where one was.</param>
/// <param name="PreviousUntil">Until when the replaced secret is taken.</param>
internal sealed record RegisteredClient(
    OidcClient Client,
    byte[] Secret,
    DateTimeOffset IssuedAt,
    byte[]? Previous,
    DateTimeOffset? PreviousUntil);
