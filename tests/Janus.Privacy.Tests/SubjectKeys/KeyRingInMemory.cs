using System;
using System.Text.Json;
using Janus.Core;

namespace Janus.Privacy.Tests.SubjectKeys;

/// <summary>
/// The key ring a rotation command is handed, holding the versions a test gives it and
/// answering every other secret as one it was not handed.
/// </summary>
internal sealed class KeyRingInMemory : IKeyRing
{
    /// <summary>
    /// Gets or sets the key-encryption keys held, or nothing where the command was
    /// handed none.
    /// </summary>
    public KeyEncryptionKeys? KeyEncryptionKeys { get; set; }

    /// <summary>
    /// Gets or sets the fingerprint keys held, or nothing where the command was handed
    /// none.
    /// </summary>
    public FingerprintKeys? FingerprintKeys { get; set; }

    /// <inheritdoc/>
    public Result<TValue> BorrowKeyEncryptionKeys<TValue>(Func<KeyEncryptionKeys, TValue> use)
    {
        ArgumentNullException.ThrowIfNull(use);

        return KeyEncryptionKeys is KeyEncryptionKeys held
            ? Result.Success(use(held))
            : Unavailable<TValue>("keyEncryptionKeys");
    }

    /// <inheritdoc/>
    public Result<TValue> BorrowKeyEncryptionKey<TValue>(int version, Func<ReadOnlyMemory<byte>, TValue> use)
    {
        ArgumentNullException.ThrowIfNull(use);

        return KeyEncryptionKeys is KeyEncryptionKeys held
            && held.Versions.TryGetValue(version, out ReadOnlyMemory<byte> key)
                ? Result.Success(use(key))
                : Unavailable<TValue>("keyEncryptionKeys");
    }

    /// <inheritdoc/>
    public Result<TValue> BorrowFingerprintKeys<TValue>(Func<FingerprintKeys, TValue> use)
    {
        ArgumentNullException.ThrowIfNull(use);

        return FingerprintKeys is FingerprintKeys held
            ? Result.Success(use(held))
            : Unavailable<TValue>("fingerprintKeys");
    }

    /// <inheritdoc/>
    public Result<TValue> BorrowMaintenanceCredential<TValue>(Func<ReadOnlyMemory<byte>, TValue> use) =>
        Unavailable<TValue>("maintenanceCredential");

    /// <inheritdoc/>
    public Result<TValue> BorrowProviderCredential<TValue>(string provider, Func<ProviderCredential, TValue> use) =>
        Unavailable<TValue>(provider);

    /// <inheritdoc/>
    public Result<TValue> BorrowMailServerSecret<TValue>(Func<ReadOnlyMemory<byte>, TValue> use) =>
        Unavailable<TValue>("mailServerSecret");

    private static Result<TValue> Unavailable<TValue>(string key) =>
        Result.Failure<TValue>(Error.From(
            ErrorCodes.StartupSecretUnavailable,
            "key",
            JsonSerializer.SerializeToElement(key)));
}
