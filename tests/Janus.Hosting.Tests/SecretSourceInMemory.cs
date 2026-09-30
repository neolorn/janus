using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Tests;

/// <summary>
/// The host's secret source as a test deployment supplies it: the credentials it holds
/// by provider name and the mail server's key where it holds one, answering nothing for
/// what it holds none of, and counting what it was asked.
/// </summary>
/// <param name="credentials">What it answers, by the provider's name.</param>
internal sealed class SecretSourceInMemory(IReadOnlyDictionary<string, ProviderCredential> credentials) : ISecretSource
{
    private readonly List<string> _asked = [];

    /// <summary>
    /// The providers whose credential was asked for, and <c>mailServerSecret</c> where the
    /// mail server's key was, in the order asked.
    /// </summary>
    public IReadOnlyList<string> Asked => _asked;

    /// <summary>
    /// The mail server's key it answers, or nothing where it holds none.
    /// </summary>
    public byte[]? MailServerSecret { get; init; }

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

    /// <inheritdoc/>
    public ValueTask<Result<ReadOnlyMemory<byte>>> ReadMailServerSecretAsync(CancellationToken cancellationToken)
    {
        _asked.Add("mailServerSecret");

        return ValueTask.FromResult(MailServerSecret is byte[] secret
            ? Result.Success<ReadOnlyMemory<byte>>(secret)
            : Result.Failure<ReadOnlyMemory<byte>>(Error.From(
                ErrorCodes.StartupSecretUnavailable,
                "key",
                JsonSerializer.SerializeToElement("mailServerSecret"))));
    }
}
