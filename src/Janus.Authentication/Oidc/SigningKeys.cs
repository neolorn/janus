using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Oidc;

/// <summary>
/// The keys tokens are signed with and validated against, and the rotation that keeps
/// them fresh.
/// </summary>
/// <param name="keys">Where the keys are held.</param>
/// <param name="configuration">Where the algorithm, the cadence and the lifetime come from.</param>
/// <param name="work">The one transaction a rotation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-KEY-001 and AUTH-KEY-002. Rotation needs no human step and no
/// restart: the first caller to reach a key that is older than the cadence rotates it,
/// the previous key stays published for the access-token lifetime plus five minutes so
/// that every token it signed validates until the last of them expires, and it leaves
/// the set afterwards.
/// </remarks>
internal sealed class SigningKeys(
    ISigningKeyStore keys,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time)
{
    // AUTH-KEY-001: the margin over the access-token lifetime, for clock skew and for
    // a relying party's cached copy of the key set.
    private static readonly TimeSpan Margin = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The key signing now, with the material to sign with, rotating first where the
    /// key in use has reached the cadence.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The material, or the refusal where a setting could not be read.</returns>
    public async ValueTask<Result<SigningMaterial>> SigningAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        SigningKey signing = (await CurrentAsync(cancellationToken).ConfigureAwait(false))
            .Match(key => key, error => Withheld<SigningKey>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SigningMaterial>(failure);
        }

        return await keys.PrivateKeyAsync(signing.KeyId, cancellationToken).ConfigureAwait(false)
            is byte[] material
            ? Result.Success(new SigningMaterial(signing.KeyId, signing.Algorithm, material))
            : Result.Failure<SigningMaterial>(Error.From(ErrorCodes.SystemFault));
    }

    /// <summary>
    /// The keys a relying party validates against: the one signing now and, through
    /// the overlap, the one before it.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The published set, or the refusal where a setting could not be read.</returns>
    public async ValueTask<Result<IReadOnlyList<PublishedSigningKey>>> PublishedAsync(
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        _ = (await CurrentAsync(cancellationToken).ConfigureAwait(false))
            .Match(key => key, error => Withheld<SigningKey>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IReadOnlyList<PublishedSigningKey>>(failure);
        }

        IReadOnlyList<SigningKey> published = await keys
            .PublishedAsync(time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        var set = new List<PublishedSigningKey>(published.Count);

        foreach (SigningKey key in published)
        {
            set.Add(new PublishedSigningKey(key.KeyId, key.Algorithm, key.PublicKey, key.RetiresAt));
        }

        return Result.Success<IReadOnlyList<PublishedSigningKey>>(set);
    }

    /// <summary>
    /// Removes the keys that have left the published set (AUTH-KEY-003).
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many went.</returns>
    public ValueTask<int> SweepAsync(CancellationToken cancellationToken) =>
        keys.SweepAsync(time.GetUtcNow(), cancellationToken);

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // ES256 is the one algorithm the key admits (chapter 10 section 4.9), and P-256 is
    // the one curve it names.
    private static ECCurve Curve(string algorithm) =>
        string.Equals(algorithm, "ES256", StringComparison.Ordinal)
            ? ECCurve.NamedCurves.nistP256
            : throw new InvalidOperationException("The signing algorithm names no curve this version holds.");

    // The key signing now: the one the store holds, or a new one where none is held or
    // the one held has reached the cadence.
    private async ValueTask<Result<SigningKey>> CurrentAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;
        DateTimeOffset now = time.GetUtcNow();

        string algorithm = (await configuration
                .ReadAsync(Settings.TokenSigningAlgorithm, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<string>(error, ref failure));

        TimeSpan cadence = (await configuration
                .ReadAsync(Settings.TokenSigningRotation, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.OidcAccessTokenLifetime, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SigningKey>(failure);
        }

        SigningKey? signing = null;

        foreach (SigningKey key in await keys.PublishedAsync(now, cancellationToken).ConfigureAwait(false))
        {
            if (key.IsSigning && string.Equals(key.Algorithm, algorithm, StringComparison.Ordinal))
            {
                signing = key;
            }
        }

        return signing is not null && !signing.IsDue(now, cadence)
            ? Result.Success(signing)
            : Result.Success(
                await RotateAsync(signing, algorithm, lifetime + Margin, now, cancellationToken)
                    .ConfigureAwait(false));
    }

    // AUTH-KEY-001: the new key begins signing, the previous stays published for the
    // overlap, and nothing about either step waits for a person.
    private async ValueTask<SigningKey> RotateAsync(
        SigningKey? signing,
        string algorithm,
        TimeSpan overlap,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var created = ECDsa.Create(Curve(algorithm));
        byte[] publicKey = created.ExportSubjectPublicKeyInfo();
        byte[] privateKey = created.ExportPkcs8PrivateKey();

        try
        {
            var key = SigningKey.Create(
                Convert.ToHexString(SHA256.HashData(publicKey))[..32],
                algorithm,
                publicKey,
                now);

            await work.BeginAsync(cancellationToken).ConfigureAwait(false);

            if (signing is not null)
            {
                signing.Supersede(now, overlap);

                await keys.RecordAsync(signing, cancellationToken).ConfigureAwait(false);
            }

            await keys.AddAsync(key, privateKey, cancellationToken).ConfigureAwait(false);
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            return key;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }
}
