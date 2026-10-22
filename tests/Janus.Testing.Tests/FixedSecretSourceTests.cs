using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Xunit;

namespace Janus.Testing.Tests;

/// <summary>
/// The fixed secret source: keys drawn once for the instance, the secrets the test
/// gives, and the key document the commands read, with the same keys (LIB-TEST-003,
/// OPS-SEC-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class FixedSecretSourceTests : IDisposable
{
    private const string Connection = "Host=database.example.test;Database=identity";

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// LIB-TEST-003: the key-encryption key and the fingerprint key are each one version
    /// of 32 bytes, the same at every read of an instance and drawn anew for the next.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_EachInstanceDrawsKeysOfItsOwnAsync()
    {
        var source = new FixedSecretSource(_randomness);
        var next = new FixedSecretSource(_randomness);

        KeyEncryptionKeys first = await KeysAsync(source);
        KeyEncryptionKeys again = await KeysAsync(source);
        FingerprintKeys fingerprint = (await source.ReadFingerprintKeysAsync(TestContext.Current.CancellationToken))
            .Match(keys => keys, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(32, first.Current.Length);
        Assert.Equal(32, fingerprint.Current.Length);
        Assert.Single(first.Versions);
        Assert.Single(fingerprint.Versions);
        Assert.Equal(first.Current.ToArray(), again.Current.ToArray());
        Assert.NotEqual(first.Current.ToArray(), fingerprint.Current.ToArray());
        Assert.NotEqual(first.Current.ToArray(), (await KeysAsync(next)).Current.ToArray());
    }

    /// <summary>
    /// LIB-TEST-003, OPS-SEC-001: the key document carries the application connection the
    /// test gives and the instance's own keys, as the commands other than migrate read
    /// it, and nothing else.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_TheKeyDocumentCarriesTheConnectionAndTheSameKeysAsync()
    {
        var source = new FixedSecretSource(_randomness);
        KeyEncryptionKeys keys = await KeysAsync(source);
        FingerprintKeys fingerprint = (await source.ReadFingerprintKeysAsync(TestContext.Current.CancellationToken))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        using var document = JsonDocument.Parse(source.KeyDocument(Connection));
        JsonElement root = document.RootElement;

        Assert.Equal(
            ["connection", "keyEncryptionKeys", "fingerprintKeys"],
            root.EnumerateObject().Select(member => member.Name));
        Assert.Equal(Connection, root.GetProperty("connection").GetString());
        Assert.Equal(1, root.GetProperty("keyEncryptionKeys").GetProperty("current").GetInt32());
        Assert.Equal(
            keys.Current.ToArray(),
            root.GetProperty("keyEncryptionKeys").GetProperty("versions").GetProperty("1").GetBytesFromBase64());
        Assert.Equal(
            fingerprint.Current.ToArray(),
            root.GetProperty("fingerprintKeys").GetProperty("versions").GetProperty("1").GetBytesFromBase64());
    }

    /// <summary>
    /// LIB-TEST-003: the maintenance credential, the mail server's secret and a social
    /// provider's credential are answered as the test gives them, and one it gives none
    /// of is refused with <c>model.startup.secretunavailable</c> naming it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_TheSecretsTheTestGivesAreAnsweredAndNoOthersAsync()
    {
        var credential = ProviderCredential.Secret("the provider's secret"u8.ToArray());
        var given = new FixedSecretSource(_randomness)
        {
            MaintenanceCredential = "Host=maintenance.example.test"u8.ToArray(),
            MailServerSecret = "the mail server's secret"u8.ToArray(),
            ProviderCredentials = new Dictionary<string, ProviderCredential>(StringComparer.Ordinal) { ["google"] = credential },
        };
        var bare = new FixedSecretSource(_randomness);

        Assert.Equal(
            "Host=maintenance.example.test"u8.ToArray(),
            (await given.ReadMaintenanceCredentialAsync(TestContext.Current.CancellationToken)).Match(value => value.ToArray(), _ => []));
        Assert.Equal(
            "the mail server's secret"u8.ToArray(),
            (await given.ReadMailServerSecretAsync(TestContext.Current.CancellationToken)).Match(value => value.ToArray(), _ => []));
        Assert.Same(
            credential,
            (await given.ReadProviderCredentialAsync("google", TestContext.Current.CancellationToken)).Match<ProviderCredential?>(value => value, _ => null));
        Assert.Equal(
            "maintenanceCredential",
            Unavailable((await bare.ReadMaintenanceCredentialAsync(TestContext.Current.CancellationToken)).Match<Error?>(_ => null, error => error)));
        Assert.Equal(
            "mailServerSecret",
            Unavailable((await bare.ReadMailServerSecretAsync(TestContext.Current.CancellationToken)).Match<Error?>(_ => null, error => error)));
        Assert.Equal(
            "socialProvider.github",
            Unavailable((await given.ReadProviderCredentialAsync("github", TestContext.Current.CancellationToken)).Match<Error?>(_ => null, error => error)));
    }

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    private static async Task<KeyEncryptionKeys> KeysAsync(FixedSecretSource source) =>
        (await source.ReadKeyEncryptionKeysAsync(TestContext.Current.CancellationToken))
            .Match(keys => keys, error => throw new InvalidOperationException(error.Code.ToString()));

    private static string? Unavailable(Error? error)
    {
        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.StartupSecretUnavailable, error.Code);

        return error.Details["key"].GetString();
    }
}
