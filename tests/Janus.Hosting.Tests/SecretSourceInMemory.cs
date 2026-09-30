using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Tests;

/// <summary>
/// The host's secret source as a test deployment supplies it: the credentials it holds
/// by provider name, answering nothing for a provider it holds none for, and counting
/// what it was asked.
/// </summary>
/// <param name="credentials">What it answers, by the provider's name.</param>
internal sealed class SecretSourceInMemory(IReadOnlyDictionary<string, ProviderCredential> credentials) : ISecretSource
{
    private readonly List<string> _asked = [];

    /// <summary>
    /// The providers whose credential was asked for, in the order asked.
    /// </summary>
    public IReadOnlyList<string> Asked => _asked;

    /// <inheritdoc/>
    public ValueTask<KeyEncryptionKeys> ReadKeyEncryptionKeysAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }));

    /// <inheritdoc/>
    public ValueTask<FingerprintKeys> ReadFingerprintKeysAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(new FingerprintKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }));

    /// <inheritdoc/>
    public ValueTask<ReadOnlyMemory<byte>> ReadMaintenanceCredentialAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<ReadOnlyMemory<byte>>("Host=maintenance.example.test"u8.ToArray());

    /// <inheritdoc/>
    public ValueTask<Result<ProviderCredential>> ReadProviderCredentialAsync(
        string provider,
        CancellationToken cancellationToken)
    {
        _asked.Add(provider);

        return ValueTask.FromResult(credentials.TryGetValue(provider, out ProviderCredential? credential)
            ? Result.Success(credential)
            : Result.Failure<ProviderCredential>(Error.From(
                ErrorCodes.StartupSecretUnavailable,
                "key",
                JsonSerializer.SerializeToElement("socialProvider." + provider))));
    }
}
