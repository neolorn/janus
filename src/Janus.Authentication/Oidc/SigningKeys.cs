using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Oidc;

/// <summary>
/// The stored signing keys and the changes their times make due: the next key made,
/// the next key made current, a replaced key retired, a kept key removed.
/// </summary>
/// <param name="keys">Where the keys are held.</param>
/// <param name="configuration">Where the algorithm and the cadence come from.</param>
/// <param name="work">The one transaction a change runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-KEY-001 and AUTH-KEY-002. No timer and no job runs a change: the
/// credential source asks for one at the first read that finds it due, in a scope of its
/// own, so the transaction here is never joined to a unit of work its caller holds open.
/// The change reads the stored keys and writes only where they still stand as read, so
/// of two processes finding the same change due one makes it and the other writes
/// nothing (D-166 X3).
/// </remarks>
internal sealed class SigningKeys(
    ISigningKeyStore keys,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time)
{
    // AUTH-KEY-001 AC6: a retired key's public key is kept for the longest a session can
    // last, the ceiling of session.default.absolute, counted from its replacement.
    private static readonly TimeSpan Keeping =
        Settings.SessionDefaultAbsolute.Ceiling ?? Settings.SessionDefaultAbsolute.Default;

    /// <summary>
    /// Whether a change is due to a set of keys: a first key where none signs, the next
    /// key where the current one nears the cadence, the next key made current, a
    /// replaced key retired, or a kept key removed.
    /// </summary>
    /// <param name="held">The keys as the set carries them.</param>
    /// <param name="now">Now.</param>
    /// <param name="cadence">How often a key is replaced.</param>
    /// <returns>Whether the stored keys are to be changed.</returns>
    /// <exception cref="ArgumentNullException">The keys are absent.</exception>
    public static bool IsChangeDue(IReadOnlyList<SigningKey> held, DateTimeOffset now, TimeSpan cadence)
    {
        ArgumentNullException.ThrowIfNull(held);

        SigningKey? current = held.SingleOrDefault(key => key.IsCurrent);
        SigningKey? next = held.SingleOrDefault(key => key.IsNext);

        return current is null
            || (next is null && current.IsNextDue(now, cadence))
            || (next is not null && next.TakesOver(current, now, cadence))
            || held.Any(key => key.IsRetirementDue(now) || key.IsRemovalDue(now));
    }

    /// <summary>
    /// Whether this version signs with an algorithm: ES256 is the one the key admits
    /// (chapter 10 section 4.9), and P-256 the one curve it names.
    /// </summary>
    /// <param name="algorithm">The algorithm <c>token.signing.algorithm</c> holds.</param>
    /// <returns>Whether a key can be made for it.</returns>
    /// <remarks>
    /// Implements AUTH-KEY-001, for a key made at a change and for a change of the key
    /// from the server (D-166, 319; D-181).
    /// </remarks>
    internal static bool Signs(string algorithm) =>
        string.Equals(algorithm, "ES256", StringComparison.Ordinal);

    /// <summary>
    /// Every key the database holds.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The stored keys.</returns>
    public ValueTask<IReadOnlyList<SigningKey>> HeldAsync(CancellationToken cancellationToken) =>
        keys.HeldAsync(cancellationToken);

    /// <summary>
    /// The private material of one key, to be cleared by the caller once its credential
    /// is made.
    /// </summary>
    /// <param name="keyId">Which key.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The material, or nothing where the key or its private key is gone.</returns>
    public ValueTask<byte[]?> PrivateKeyAsync(string keyId, CancellationToken cancellationToken) =>
        keys.PrivateKeyAsync(keyId, cancellationToken);

    /// <summary>
    /// Makes the changes due to the stored keys, in one transaction of its own that
    /// commits only where the keys it made or made current stood as read.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success whether or not another process had already made the change, or the
    /// refusal where a setting could not be read or the transaction not opened or
    /// committed.
    /// </returns>
    public async ValueTask<Result> ChangeAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;
        DateTimeOffset now = time.GetUtcNow();

        TimeSpan cadence = (await configuration
                .ReadAsync(Settings.TokenSigningRotation, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        string algorithm = (await configuration
                .ReadAsync(Settings.TokenSigningAlgorithm, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<string>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        IReadOnlyList<SigningKey> stored = await keys.HeldAsync(cancellationToken).ConfigureAwait(false);
        SigningKey? current = stored.SingleOrDefault(key => key.IsCurrent);
        SigningKey? next = stored.SingleOrDefault(key => key.IsNext);
        bool made = true;

        if (current is null)
        {
            made = await MadeAsync(algorithm, now, first: true, cancellationToken).ConfigureAwait(false);
        }
        else if (next is null && current.IsNextDue(now, cadence))
        {
            made = await MadeAsync(algorithm, now, first: false, cancellationToken).ConfigureAwait(false);
        }
        else if (next is not null && next.TakesOver(current, now, cadence))
        {
            made = await keys
                .PromoteAsync(next, current, now, current.OverlapEnd(now), now + Keeping, cancellationToken)
                .ConfigureAwait(false);
        }

        // AUTH-KEY-001 AC5, AC6: a retirement or a removal another process already made
        // leaves nothing to write, and the same end either way.
        foreach (SigningKey key in stored.Where(key => key.IsRetirementDue(now)))
        {
            await keys.RetireAsync(key, now, cancellationToken).ConfigureAwait(false);
        }

        foreach (SigningKey key in stored.Where(key => key.IsRemovalDue(now)))
        {
            await keys.RemoveAsync(key, now, cancellationToken).ConfigureAwait(false);
        }

        // X3: where another process made the key or the change first, nothing of this
        // transaction is committed: the scope it runs in rolls it back as it ends, and
        // the caller reads the stored keys that process left.
        if (!made)
        {
            return Result.Success();
        }

        return await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Stores a longer access-token lifetime with the current key before it signs an
    /// access token under it, in one transaction of its own, which changes nothing where
    /// the key is no longer current or already carries as long a lifetime.
    /// </summary>
    /// <param name="current">The key the caller read as current.</param>
    /// <param name="lifetime">The lifetime the access token is signed under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal where the transaction could not be opened or committed.
    /// </returns>
    /// <exception cref="ArgumentNullException">The key is absent.</exception>
    public async ValueTask<Result> LengthenAsync(
        SigningKey current,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(current);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        _ = await keys.LengthenAsync(current, lifetime, cancellationToken).ConfigureAwait(false);

        return await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static ECCurve Curve(string algorithm) =>
        Signs(algorithm)
            ? ECCurve.NamedCurves.nistP256
            : throw new InvalidOperationException("The signing algorithm names no curve this version holds.");

    // AUTH-KEY-001: a key pair nobody made by hand, its private material cleared once
    // the store has wrapped it.
    private async ValueTask<bool> MadeAsync(
        string algorithm,
        DateTimeOffset now,
        bool first,
        CancellationToken cancellationToken)
    {
        using var created = ECDsa.Create(Curve(algorithm));
        byte[] publicKey = created.ExportSubjectPublicKeyInfo();
        byte[] privateKey = created.ExportPkcs8PrivateKey();

        try
        {
            string keyId = Convert.ToHexString(SHA256.HashData(publicKey))[..32];

            return await keys
                .AddAsync(
                    first
                        ? SigningKey.First(keyId, algorithm, publicKey, now)
                        : SigningKey.Next(keyId, algorithm, publicKey, now),
                    privateKey,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }
}
