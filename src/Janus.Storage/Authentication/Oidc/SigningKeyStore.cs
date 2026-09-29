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
/// The token signing keys, over the <c>signing_keys</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="deployment">The deployment's data key, which the private material is wrapped under.</param>
/// <remarks>
/// Implements AUTH-KEY-001, AUTH-KEY-002, PRIV-RIGHT-005a and CONV-DESIGN-003. The
/// private material is wrapped on the way in and unwrapped for the one caller that
/// signs with it, so it is at rest under the deployment's data key and nowhere else.
/// </remarks>
internal sealed class SigningKeyStore(
    StoreContext context,
    DeploymentDataKeyStore deployment) : ISigningKeyStore
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SigningKey>> PublishedAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await context.SigningKeys
            .Where(key => key.RetiresAt == null || key.RetiresAt > now)
            .OrderByDescending(key => key.CreatedAt)
            .Select(key => SigningKey.Existing(
                key.KeyId,
                key.Algorithm,
                key.PublicKey,
                key.CreatedAt,
                key.SupersededAt,
                key.RetiresAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<byte[]?> PrivateKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        SigningKeyRecord? record = await HeldAsync(keyId, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        byte[] deploymentKey = await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return PersonalFieldCipher.Unwrap(record.PrivateKey, deploymentKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(deploymentKey);
        }
    }

    /// <inheritdoc/>
    public async ValueTask AddAsync(
        SigningKey key,
        [NeverLogged] byte[] privateKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(privateKey);

        byte[] deploymentKey = await deployment.UnwrappedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await context.SigningKeys
                .AddAsync(
                    new SigningKeyRecord
                    {
                        KeyId = key.KeyId,
                        Algorithm = key.Algorithm,
                        PublicKey = key.PublicKey,
                        PrivateKey = PersonalFieldCipher.Wrap(privateKey, deploymentKey),
                        CreatedAt = key.CreatedAt,
                        SupersededAt = key.SupersededAt,
                        RetiresAt = key.RetiresAt,
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
    public async ValueTask RecordAsync(SigningKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        SigningKeyRecord record = await HeldAsync(key.KeyId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The signing key has no row to carry the change.");

        record.SupersededAt = key.SupersededAt;
        record.RetiresAt = key.RetiresAt;
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.SigningKeys
            .Where(key => key.RetiresAt != null && key.RetiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask<SigningKeyRecord?> HeldAsync(
        string keyId,
        CancellationToken cancellationToken) =>
        await context.SigningKeys.FindAsync([keyId], cancellationToken).ConfigureAwait(false);
}
