using System;
using System.Collections.Generic;
using System.Text.Json;
using Janus.Core;

namespace Janus.Storage.Tests;

/// <summary>
/// The key ring as a test deployment fills it: the key-encryption key and the fingerprint
/// key it was handed, lent as the library's ring lends them, and nothing else.
/// </summary>
/// <param name="keyEncryptionKeys">The key-encryption key, or nothing where the test holds none.</param>
/// <param name="fingerprintKeys">The fingerprint key.</param>
internal sealed class KeyRingInMemory(KeyEncryptionKeys? keyEncryptionKeys, FingerprintKeys fingerprintKeys) : IKeyRing
{
    /// <inheritdoc/>
    public Result<TValue> BorrowKeyEncryptionKeys<TValue>(Func<KeyEncryptionKeys, TValue> use)
    {
        ArgumentNullException.ThrowIfNull(use);

        return keyEncryptionKeys is KeyEncryptionKeys held
            ? Result.Success(use(held))
            : Result.Failure<TValue>(Unavailable("keyEncryptionKeys"));
    }

    /// <inheritdoc/>
    public Result<TValue> BorrowKeyEncryptionKey<TValue>(int version, Func<ReadOnlyMemory<byte>, TValue> use)
    {
        ArgumentNullException.ThrowIfNull(use);

        return keyEncryptionKeys is KeyEncryptionKeys held && held.Versions.TryGetValue(version, out ReadOnlyMemory<byte> key)
            ? Result.Success(use(key))
            : Result.Failure<TValue>(new Error(
                ErrorCodes.StartupSecretUnavailable,
                new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["key"] = JsonSerializer.SerializeToElement("keyEncryptionKeys"),
                    ["version"] = JsonSerializer.SerializeToElement(version),
                }));
    }

    /// <inheritdoc/>
    public Result<TValue> BorrowFingerprintKeys<TValue>(Func<FingerprintKeys, TValue> use)
    {
        ArgumentNullException.ThrowIfNull(use);

        return Result.Success(use(fingerprintKeys));
    }

    /// <inheritdoc/>
    public Result<TValue> BorrowMaintenanceCredential<TValue>(Func<ReadOnlyMemory<byte>, TValue> use) =>
        Result.Failure<TValue>(Unavailable("maintenanceCredential"));

    /// <inheritdoc/>
    public Result<TValue> BorrowProviderCredential<TValue>(string provider, Func<ProviderCredential, TValue> use) =>
        Result.Failure<TValue>(Unavailable("socialProvider." + provider));

    /// <inheritdoc/>
    public Result<TValue> BorrowMailServerSecret<TValue>(Func<ReadOnlyMemory<byte>, TValue> use) =>
        Result.Failure<TValue>(Unavailable("mailServerSecret"));

    private static Error Unavailable(string key) =>
        Error.From(ErrorCodes.StartupSecretUnavailable, "key", JsonSerializer.SerializeToElement(key));
}
