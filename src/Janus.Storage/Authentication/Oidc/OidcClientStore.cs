using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The registered clients, over the <c>oidc_clients</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>Implements AUTH-OIDC-001 and CONV-DESIGN-003.</remarks>
internal sealed class OidcClientStore(JanusDbContext context) : IOidcClientStore
{
    /// <inheritdoc/>
    public async ValueTask<OidcClient?> FindAsync(
        string clientId,
        CancellationToken cancellationToken)
    {
        OidcClientRecord? record = await HeldAsync(clientId, cancellationToken).ConfigureAwait(false);

        return record is null ? null : Client(record);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> AuthenticatesAsync(
        string clientId,
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        OidcClientRecord? record = await HeldAsync(clientId, cancellationToken).ConfigureAwait(false);

        return record is not null
            && CryptographicOperations.FixedTimeEquals(record.Secret, fingerprint);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<OidcClient>> AllAsync(CancellationToken cancellationToken) =>
        await context.OidcClients
            .OrderBy(client => client.ClientId)
            .Select(client => Client(client))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask RecordAsync(
        OidcClient client,
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(fingerprint);

        OidcClientRecord? held = await HeldAsync(client.ClientId, cancellationToken).ConfigureAwait(false);

        if (held is null)
        {
            await context.OidcClients
                .AddAsync(
                    new OidcClientRecord
                    {
                        ClientId = client.ClientId,
                        Name = client.Name,
                        Kind = client.Kind,
                        Redirect = client.Redirect,
                        Secret = fingerprint,
                        Scopes = [.. client.Scopes],
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        held.Name = client.Name;
        held.Kind = client.Kind;
        held.Redirect = client.Redirect;
        held.Secret = fingerprint;
        held.Scopes = [.. client.Scopes];
    }

    private static OidcClient Client(OidcClientRecord record) =>
        new(record.ClientId, record.Name, record.Kind, record.Redirect, record.Scopes);

    private async ValueTask<OidcClientRecord?> HeldAsync(
        string clientId,
        CancellationToken cancellationToken) =>
        await context.OidcClients.FindAsync([clientId], cancellationToken).ConfigureAwait(false);
}
