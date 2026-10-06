using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// The token signing keys, over the <c>signing_keys</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="connections">The connection and transaction a new key is written through.</param>
/// <param name="deployment">The deployment's data key, which the private material is wrapped under.</param>
/// <remarks>
/// Implements AUTH-KEY-001, AUTH-KEY-002, PRIV-RIGHT-005a and CONV-DESIGN-003. The
/// private material is wrapped on the way in and unwrapped for the one caller that makes
/// a credential of it, so it is at rest under the deployment's data key and nowhere
/// else. Every write is one statement conditional on the key as its caller read it, and a
/// new key is one insert that does nothing where the table already holds a key of its
/// kind (D-166 X3).
/// </remarks>
internal sealed class SigningKeyStore(
    StoreContext context,
    DataConnections connections,
    DeploymentDataKeyStore deployment) : ISigningKeyStore
{
    private const string Insert =
        """
        INSERT INTO identity.signing_keys
            (key_id, algorithm, public_key, private_key, created_at, signing_from, longest_lifetime)
        VALUES
            (@KeyId, @Algorithm, @PublicKey, @PrivateKey, @CreatedAt, @SigningFrom, @LongestLifetime)
        ON CONFLICT DO NOTHING;
        """;

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SigningKey>> HeldAsync(CancellationToken cancellationToken) =>
        await context.SigningKeys
            .OrderBy(key => key.CreatedAt)
            .Select(key => SigningKey.Existing(
                key.KeyId,
                key.Algorithm,
                key.PublicKey,
                key.CreatedAt,
                key.SigningFrom,
                key.LongestLifetime,
                key.SupersededAt,
                key.RetiresAt,
                key.KeptUntil,
                key.PrivateKey != null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<byte[]?> PrivateKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        byte[]? wrapped = await context.SigningKeys
            .Where(key => key.KeyId == keyId)
            .Select(key => key.PrivateKey)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (wrapped is null)
        {
            return null;
        }

        byte[] deploymentKey = await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return PersonalFieldCipher.Unwrap(wrapped, deploymentKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(deploymentKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> AddAsync(
        SigningKey key,
        [NeverLogged] byte[] privateKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(privateKey);

        byte[] deploymentKey = await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false);
        byte[] wrapped;

        try
        {
            wrapped = PersonalFieldCipher.Wrap(privateKey, deploymentKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(deploymentKey);
        }

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        int added = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Insert,
                new
                {
                    key.KeyId,
                    key.Algorithm,
                    key.PublicKey,
                    PrivateKey = wrapped,
                    CreatedAt = key.PublishedAt,
                    key.SigningFrom,
                    key.LongestLifetime,
                },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return added == 1;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> PromoteAsync(
        SigningKey next,
        SigningKey current,
        DateTimeOffset at,
        DateTimeOffset overlapEndsAt,
        DateTimeOffset keptUntil,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(current);

        // X3: the current key is replaced only where it is still current and carries
        // the lifetime its overlap was worked out from, and the next key made current
        // only where it is still next.
        int replaced = await context.SigningKeys
            .Where(key => key.KeyId == current.KeyId
                && key.SigningFrom != null
                && key.SupersededAt == null
                && key.LongestLifetime == current.LongestLifetime)
            .ExecuteUpdateAsync(
                columns => columns
                    .SetProperty(key => key.SupersededAt, at)
                    .SetProperty(key => key.RetiresAt, overlapEndsAt)
                    .SetProperty(key => key.KeptUntil, keptUntil),
                cancellationToken)
            .ConfigureAwait(false);

        if (replaced != 1)
        {
            return false;
        }

        int promoted = await context.SigningKeys
            .Where(key => key.KeyId == next.KeyId && key.SigningFrom == null)
            .ExecuteUpdateAsync(
                columns => columns.SetProperty(key => key.SigningFrom, at),
                cancellationToken)
            .ConfigureAwait(false);

        return promoted == 1;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> LengthenAsync(
        SigningKey current,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(current);

        // AUTH-KEY-001 AC2: never lowered, and only while the key is current.
        int lengthened = await context.SigningKeys
            .Where(key => key.KeyId == current.KeyId && key.SigningFrom != null && key.SupersededAt == null)
            .ExecuteUpdateAsync(
                columns => columns.SetProperty(
                    key => key.LongestLifetime,
                    key => key.LongestLifetime < lifetime ? lifetime : key.LongestLifetime),
                cancellationToken)
            .ConfigureAwait(false);

        return lengthened == 1;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RetireAsync(SigningKey key, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        int retired = await context.SigningKeys
            .Where(held => held.KeyId == key.KeyId
                && held.PrivateKey != null
                && held.RetiresAt != null
                && held.RetiresAt <= now)
            .ExecuteUpdateAsync(
                columns => columns.SetProperty(held => held.PrivateKey, (byte[]?)null),
                cancellationToken)
            .ConfigureAwait(false);

        return retired > 0;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RemoveAsync(SigningKey key, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        int removed = await context.SigningKeys
            .Where(held => held.KeyId == key.KeyId && held.KeptUntil != null && held.KeptUntil <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return removed > 0;
    }
}
