using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;

namespace Janus.Authentication.Tests.Oidc;

/// <summary>
/// The signing keys, held in memory. The private material is held as it was written,
/// which is what a store over a database holds wrapped.
/// </summary>
internal sealed class SigningKeyStoreInMemory : ISigningKeyStore
{
    private readonly Dictionary<string, (SigningKey Key, byte[] PrivateKey)> _keys =
        new(StringComparer.Ordinal);

    /// <summary>
    /// How many keys the store holds, published or not.
    /// </summary>
    public int Count => _keys.Count;

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SigningKey>> PublishedAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<SigningKey>>(
            [.. _keys.Values
                .Select(held => held.Key)
                .Where(key => !key.HasRetired(now))
                .OrderByDescending(key => key.CreatedAt)]);

    /// <inheritdoc/>
    public ValueTask<byte[]?> PrivateKeyAsync(string keyId, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            _keys.TryGetValue(keyId, out (SigningKey Key, byte[] PrivateKey) held)
                ? held.PrivateKey.ToArray()
                : null);

    /// <inheritdoc/>
    public ValueTask AddAsync(SigningKey key, byte[] privateKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(privateKey);

        _keys[key.KeyId] = (key, privateKey.ToArray());

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(SigningKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        _keys[key.KeyId] = (key, _keys[key.KeyId].PrivateKey);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<string> retired = [.. _keys
            .Where(entry => entry.Value.Key.HasRetired(now))
            .Select(entry => entry.Key)];

        foreach (string keyId in retired)
        {
            _ = _keys.Remove(keyId);
        }

        return ValueTask.FromResult(retired.Count);
    }
}
