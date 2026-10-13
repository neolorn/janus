using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// The signing keys, held in memory. The private material is held as it was written,
/// which is what a store over a database holds wrapped, and every write is conditional
/// on the key as its caller read it, as the database's statements are.
/// </summary>
internal sealed class SigningKeyStoreInMemory : ISigningKeyStore
{
    private readonly Dictionary<string, (SigningKey Key, byte[]? PrivateKey)> _keys =
        new(StringComparer.Ordinal);

    private readonly List<byte[]> _handed = [];
    private readonly List<byte[]> _lent = [];

    /// <summary>
    /// How many keys the store holds, published or not.
    /// </summary>
    public int Count => _keys.Count;

    /// <summary>
    /// How many times the stored keys were read whole.
    /// </summary>
    public int Reads { get; private set; }

    /// <summary>
    /// The arrays of private material the library handed to be written, as it handed
    /// them.
    /// </summary>
    public IReadOnlyList<byte[]> Handed => _handed;

    /// <summary>
    /// The arrays of private material the store lent the library, as it lent them.
    /// </summary>
    public IReadOnlyList<byte[]> Lent => _lent;

    /// <summary>
    /// The key the store holds under an identifier, as stored.
    /// </summary>
    /// <param name="keyId">Which key.</param>
    /// <returns>The key, or nothing where the store holds none.</returns>
    public SigningKey? Held(string keyId) =>
        _keys.TryGetValue(keyId, out (SigningKey Key, byte[]? PrivateKey) held) ? held.Key : null;

    /// <summary>
    /// Whether the store holds a key's private key.
    /// </summary>
    /// <param name="keyId">Which key.</param>
    /// <returns>Whether it does.</returns>
    public bool HoldsPrivateKey(string keyId) =>
        _keys.TryGetValue(keyId, out (SigningKey Key, byte[]? PrivateKey) held) && held.PrivateKey is not null;

    /// <summary>
    /// Whether the next whole read answers as it would have before any key was stored,
    /// which is what a process reads that another is about to make the first key under.
    /// </summary>
    public bool ReadsBeforeTheFirstKey { get; set; }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SigningKey>> HeldAsync(CancellationToken cancellationToken)
    {
        Reads++;

        if (ReadsBeforeTheFirstKey)
        {
            ReadsBeforeTheFirstKey = false;

            return ValueTask.FromResult<IReadOnlyList<SigningKey>>([]);
        }

        return ValueTask.FromResult<IReadOnlyList<SigningKey>>(
            [.. _keys.Values.Select(held => held.Key).OrderBy(key => key.PublishedAt)]);
    }

    /// <inheritdoc/>
    public ValueTask<byte[]?> PrivateKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        if (!_keys.TryGetValue(keyId, out (SigningKey Key, byte[]? PrivateKey) held) || held.PrivateKey is null)
        {
            return ValueTask.FromResult<byte[]?>(null);
        }

        byte[] lent = held.PrivateKey.ToArray();

        _lent.Add(lent);

        return ValueTask.FromResult<byte[]?>(lent);
    }

    /// <inheritdoc/>
    public ValueTask<bool> AddAsync(SigningKey key, byte[] privateKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(privateKey);

        _handed.Add(privateKey);

        // One next key and one current key, as the table's constraints admit.
        if (_keys.ContainsKey(key.KeyId)
            || _keys.Values.Any(held => (held.Key.IsNext && key.IsNext) || (held.Key.IsCurrent && key.IsCurrent)))
        {
            return ValueTask.FromResult(false);
        }

        _keys[key.KeyId] = (key, privateKey.ToArray());

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> PromoteAsync(
        SigningKey next,
        SigningKey current,
        DateTimeOffset at,
        DateTimeOffset overlapEndsAt,
        DateTimeOffset keptUntil,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(current);

        if (!_keys.TryGetValue(current.KeyId, out (SigningKey Key, byte[]? PrivateKey) replaced)
            || !replaced.Key.IsCurrent
            || replaced.Key.LongestLifetime != current.LongestLifetime
            || !_keys.TryGetValue(next.KeyId, out (SigningKey Key, byte[]? PrivateKey) promoted)
            || !promoted.Key.IsNext)
        {
            return ValueTask.FromResult(false);
        }

        _keys[current.KeyId] = (
            With(replaced.Key, replaced.Key.SigningFrom, replaced.Key.LongestLifetime, at, overlapEndsAt, keptUntil, true),
            replaced.PrivateKey);
        _keys[next.KeyId] = (
            With(promoted.Key, at, TimeSpan.Zero, null, null, null, true),
            promoted.PrivateKey);

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> LengthenAsync(SigningKey current, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (!_keys.TryGetValue(current.KeyId, out (SigningKey Key, byte[]? PrivateKey) held) || !held.Key.IsCurrent)
        {
            return ValueTask.FromResult(false);
        }

        _keys[current.KeyId] = (
            With(
                held.Key,
                held.Key.SigningFrom,
                held.Key.LongestLifetime < lifetime ? lifetime : held.Key.LongestLifetime,
                null,
                null,
                null,
                true),
            held.PrivateKey);

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> RetireAsync(SigningKey key, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!_keys.TryGetValue(key.KeyId, out (SigningKey Key, byte[]? PrivateKey) held)
            || held.PrivateKey is null
            || held.Key.OverlapEndsAt is not DateTimeOffset ends
            || ends > now)
        {
            return ValueTask.FromResult(false);
        }

        _keys[key.KeyId] = (
            With(
                held.Key,
                held.Key.SigningFrom,
                held.Key.LongestLifetime,
                held.Key.ReplacedAt,
                held.Key.OverlapEndsAt,
                held.Key.KeptUntil,
                false),
            null);

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> RemoveAsync(SigningKey key, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        return ValueTask.FromResult(
            _keys.TryGetValue(key.KeyId, out (SigningKey Key, byte[]? PrivateKey) held)
            && held.Key.KeptUntil is DateTimeOffset kept
            && kept <= now
            && _keys.Remove(key.KeyId));
    }

    private static SigningKey With(
        SigningKey key,
        DateTimeOffset? signingFrom,
        TimeSpan longestLifetime,
        DateTimeOffset? replacedAt,
        DateTimeOffset? overlapEndsAt,
        DateTimeOffset? keptUntil,
        bool holdsPrivateKey) =>
        SigningKey.Existing(
            key.KeyId,
            key.Algorithm,
            key.PublicKey,
            key.PublishedAt,
            signingFrom,
            longestLifetime,
            replacedAt,
            overlapEndsAt,
            keptUntil,
            holdsPrivateKey);
}
