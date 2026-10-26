using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Testing;

/// <summary>
/// A secret source for tests: a key-encryption key and a fingerprint key drawn once for
/// the instance, the other secrets the test gives it, and the key document the commands
/// read, written with the same keys.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-003, LIB-EXT-001 and OPS-SEC-001. Each instance draws keys of its
/// own, so each test's deployment starts clean; a development setup that keeps its
/// database across restarts supplies a secret source of its own. What the test gives it
/// none of is answered with <c>model.startup.secretunavailable</c> naming it, as a
/// deployment's own source answers a secret it does not hold.
/// </remarks>
public sealed class FixedSecretSource : ISecretSource
{
    private const int KeyLength = 32;

    private const int Version = 1;

    private readonly byte[] _keyEncryptionKey;

    private readonly byte[] _fingerprintKey;

    /// <summary>
    /// Draws the instance's keys.
    /// </summary>
    /// <param name="randomness">Where the keys are drawn from.</param>
    /// <exception cref="ArgumentNullException">The randomness is absent.</exception>
    public FixedSecretSource(RandomNumberGenerator randomness)
    {
        ArgumentNullException.ThrowIfNull(randomness);

        _keyEncryptionKey = new byte[KeyLength];
        _fingerprintKey = new byte[KeyLength];
        randomness.GetBytes(_keyEncryptionKey);
        randomness.GetBytes(_fingerprintKey);
    }

    /// <summary>
    /// The maintenance credential it answers (OPS-MIG-003a), or nothing where the test
    /// gives none.
    /// </summary>
    public ReadOnlyMemory<byte>? MaintenanceCredential { get; init; }

    /// <summary>
    /// The mail server's secret it answers (INT-MAIL-001), or nothing where the test gives
    /// none.
    /// </summary>
    public ReadOnlyMemory<byte>? MailServerSecret { get; init; }

    /// <summary>
    /// Each social provider's credential it answers, by the provider's name.
    /// </summary>
    public IReadOnlyDictionary<string, ProviderCredential> ProviderCredentials { get; init; } =
        new Dictionary<string, ProviderCredential>(StringComparer.Ordinal);

    /// <summary>
    /// Writes the key document a command other than <c>migrate</c> reads on its standard
    /// input: the application connection the test gives, and the instance's keys.
    /// </summary>
    /// <param name="connection">The application connection.</param>
    /// <returns>The document, as the bytes piped to the command.</returns>
    /// <exception cref="ArgumentException">The connection is empty.</exception>
    public byte[] KeyDocument(string connection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connection);

        using var buffer = new MemoryStream();

        try
        {
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject();
                json.WriteString("connection", connection);
                Versions(json, "keyEncryptionKeys", _keyEncryptionKey);
                Versions(json, "fingerprintKeys", _fingerprintKey);
                json.WriteEndObject();
            }

            return buffer.ToArray();
        }
        finally
        {
            // CONV-CODE-007: the stream's buffer holds the keys too, and is cleared once
            // the document is copied out of it.
            CryptographicOperations.ZeroMemory(buffer.GetBuffer());
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<KeyEncryptionKeys>> ReadKeyEncryptionKeysAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(new KeyEncryptionKeys(Version, Held(_keyEncryptionKey))));

    /// <inheritdoc/>
    public ValueTask<Result<FingerprintKeys>> ReadFingerprintKeysAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(new FingerprintKeys(Version, Held(_fingerprintKey))));

    /// <inheritdoc/>
    public ValueTask<Result<ReadOnlyMemory<byte>>> ReadMaintenanceCredentialAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(MaintenanceCredential is ReadOnlyMemory<byte> credential
            ? Result.Success(credential)
            : Result.Failure<ReadOnlyMemory<byte>>(Unavailable("maintenanceCredential")));

    /// <inheritdoc/>
    public ValueTask<Result<ProviderCredential>> ReadProviderCredentialAsync(
        string provider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return ValueTask.FromResult(ProviderCredentials.TryGetValue(provider, out ProviderCredential? credential)
            ? Result.Success(credential)
            : Result.Failure<ProviderCredential>(Unavailable("socialProvider." + provider)));
    }

    /// <inheritdoc/>
    public ValueTask<Result<ReadOnlyMemory<byte>>> ReadMailServerSecretAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(MailServerSecret is ReadOnlyMemory<byte> secret
            ? Result.Success(secret)
            : Result.Failure<ReadOnlyMemory<byte>>(Unavailable("mailServerSecret")));

    private static Dictionary<int, ReadOnlyMemory<byte>> Held(byte[] key) => new() { [Version] = key };

    private static void Versions(Utf8JsonWriter json, string name, byte[] key)
    {
        json.WriteStartObject(name);
        json.WriteNumber("current", Version);
        json.WriteStartObject("versions");
        json.WriteBase64String(Version.ToString(System.Globalization.CultureInfo.InvariantCulture), key);
        json.WriteEndObject();
        json.WriteEndObject();
    }

    private static Error Unavailable(string key) =>
        Error.From(ErrorCodes.StartupSecretUnavailable, "key", JsonSerializer.SerializeToElement(key));
}
