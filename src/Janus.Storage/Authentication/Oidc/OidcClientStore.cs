using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The registered clients, over the <c>oidc_clients</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="deployment">The deployment's data key, which the secrets are wrapped under.</param>
/// <remarks>
/// Implements AUTH-OIDC-001, OPS-SEC-001, OPS-SEC-002, PRIV-RIGHT-005a and
/// CONV-DESIGN-003. The secrets are wrapped on the way in and unwrapped for the one
/// caller that presents or judges them, so they are at rest under the deployment's data
/// key and nowhere else.
/// </remarks>
internal sealed class OidcClientStore(
    StoreContext context,
    DeploymentDataKeyStore deployment) : IOidcClientStore
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
    public async ValueTask<IReadOnlyList<OidcClient>> AllAsync(CancellationToken cancellationToken) =>
        await context.OidcClients
            .OrderBy(client => client.ClientId)
            .Select(client => Client(client))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask AddAsync(
        OidcClient client,
        [NeverLogged] ReadOnlyMemory<byte> secret,
        DateTimeOffset issuedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        byte[] deploymentKey = await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await context.OidcClients
                .AddAsync(
                    new OidcClientRecord
                    {
                        ClientId = client.ClientId,
                        Name = client.Name,
                        Kind = client.Kind,
                        Redirect = client.Redirect,
                        Secret = PersonalFieldCipher.Wrap(secret.Span, deploymentKey),
                        SecretIssuedAt = issuedAt,
                        Scopes = [.. client.Scopes],
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(deploymentKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(OidcClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        OidcClientRecord held = await HeldAsync(client.ClientId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The client has no row to carry the change.");

        held.Name = client.Name;
        held.Kind = client.Kind;
        held.Redirect = client.Redirect;
        held.Scopes = [.. client.Scopes];
    }

    /// <inheritdoc/>
    public async ValueTask<RegisteredSecret?> SecretAsync(string clientId, CancellationToken cancellationToken)
    {
        OidcClientRecord? record = await context.OidcClients
            .AsNoTracking()
            .SingleOrDefaultAsync(client => client.ClientId == clientId, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        byte[] deploymentKey = await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return new RegisteredSecret(
                PersonalFieldCipher.Unwrap(record.Secret, deploymentKey),
                record.SecretIssuedAt,
                record.PreviousSecret is byte[] previous ? PersonalFieldCipher.Unwrap(previous, deploymentKey) : null,
                record.PreviousSecretUntil);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(deploymentKey);
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
        byte[] deploymentKey = await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false);
        byte[] wrapped;

        try
        {
            wrapped = PersonalFieldCipher.Wrap(secret.Span, deploymentKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(deploymentKey);
        }

        // X3: the replacement stands only where the secret is still the one read, so of
        // two processes rotating together one replaces it and the other changes nothing.
        int replaced = await context.OidcClients
            .Where(client => client.ClientId == clientId && client.SecretIssuedAt == issuedAt)
            .ExecuteUpdateAsync(
                columns => columns
                    .SetProperty(client => client.PreviousSecret, client => client.Secret)
                    .SetProperty(client => client.PreviousSecretUntil, replacedUntil)
                    .SetProperty(client => client.Secret, wrapped)
                    .SetProperty(client => client.SecretIssuedAt, now),
                cancellationToken)
            .ConfigureAwait(false);

        return replaced == 1;
    }

    private static OidcClient Client(OidcClientRecord record) =>
        new(record.ClientId, record.Name, record.Kind, record.Redirect, record.Scopes);

    private async ValueTask<OidcClientRecord?> HeldAsync(
        string clientId,
        CancellationToken cancellationToken) =>
        await context.OidcClients.FindAsync([clientId], cancellationToken).ConfigureAwait(false);
}
