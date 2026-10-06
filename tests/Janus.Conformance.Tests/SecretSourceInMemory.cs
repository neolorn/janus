using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Conformance.Tests;

/// <summary>
/// The sample host's secret source, which answers the keys and the maintenance
/// credential the host holds, and nothing for a social provider or a mail server, since
/// the sample host declares neither.
/// </summary>
/// <param name="keyEncryptionKeys">The key-encryption key versions it answers.</param>
/// <param name="fingerprintKeys">The fingerprint key versions it answers.</param>
/// <param name="maintenanceCredential">The maintenance connection it answers, as its UTF-8 bytes.</param>
internal sealed class SecretSourceInMemory(
    KeyEncryptionKeys keyEncryptionKeys,
    FingerprintKeys fingerprintKeys,
    ReadOnlyMemory<byte> maintenanceCredential) : ISecretSource
{
    /// <inheritdoc/>
    public ValueTask<Result<KeyEncryptionKeys>> ReadKeyEncryptionKeysAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(keyEncryptionKeys));

    /// <inheritdoc/>
    public ValueTask<Result<FingerprintKeys>> ReadFingerprintKeysAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(fingerprintKeys));

    /// <inheritdoc/>
    public ValueTask<Result<ReadOnlyMemory<byte>>> ReadMaintenanceCredentialAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(maintenanceCredential));

    /// <inheritdoc/>
    public ValueTask<Result<ProviderCredential>> ReadProviderCredentialAsync(
        string provider,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Failure<ProviderCredential>(Error.From(
            ErrorCodes.StartupSecretUnavailable,
            "key",
            JsonSerializer.SerializeToElement("socialProvider." + provider))));

    /// <inheritdoc/>
    public ValueTask<Result<ReadOnlyMemory<byte>>> ReadMailServerSecretAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Failure<ReadOnlyMemory<byte>>(Error.From(
            ErrorCodes.StartupSecretUnavailable,
            "key",
            JsonSerializer.SerializeToElement("mailServerSecret"))));
}
