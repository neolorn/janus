using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Dapper;
using Janus.Storage.Tests;
using Npgsql;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// Where the key material a command is piped goes: into the run's memory and out through
/// the escrow copy, never into the library's schema (INF-HOST-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class KeyDocumentTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const string RotateKeyEncryptionKey = "rotate-kek";

    private const string RotateFingerprintKey = "rotate-fingerprint-key";

    private const string Sealed = "--sealed";

    private const string Maintenance = "identity_maintenance";

    private static readonly string NextKeyEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static readonly byte[] NextFingerprintKey = RandomNumberGenerator.GetBytes(32);

    /// <summary>
    /// INF-HOST-003 AC4: after bootstrap has fingerprinted the first administrator's
    /// identifiers and mailbox, and both keys have been rotated and sealed, no column of
    /// the library's schema holds either version of the fingerprint key, as bytes or in a
    /// written form.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_HOST_003_AC4_TheFingerprintKeyIsNeverWrittenToTheDatabaseAsync()
    {
        JsonObject beforeTheFingerprintKey = Document(
            Versions(2, (1, Invocation.KeyEncryptionKey), (2, NextKeyEncryptionKey)),
            Versions(1, (1, Invocation.FingerprintKey)));
        JsonObject afterIt = Document(
            Versions(2, (2, NextKeyEncryptionKey)),
            Versions(2, (1, Invocation.FingerprintKey), (2, Convert.ToBase64String(NextFingerprintKey))));

        int[] exitCodes =
        [
            (await Invocation.PipedAsync(Invocation.Bootstrap(), Invocation.Keys(database.ConnectionString))).ExitCode,
            (await Invocation.PipedAsync([RotateKeyEncryptionKey], beforeTheFingerprintKey)).ExitCode,
            (await Invocation.PipedAsync([RotateKeyEncryptionKey, Sealed], beforeTheFingerprintKey)).ExitCode,
            (await Invocation.PipedAsync([RotateFingerprintKey], afterIt)).ExitCode,
            (await Invocation.PipedAsync([RotateFingerprintKey, Sealed], afterIt)).ExitCode,
        ];

        await using NpgsqlConnection connection = await database.OpenAsync();
        IReadOnlyList<int> fingerprintVersions = [.. await connection.QueryAsync<int>(
            "SELECT DISTINCT fingerprint_version FROM identity.identifiers")];
        IReadOnlyList<string> holdingTheOrigin = await FingerprintKeyScan.HoldingAsync(connection, [Invocation.Origin]);
        IReadOnlyList<string> holdingTheKey = await FingerprintKeyScan.HoldingAsync(
            connection,
            [
                .. FingerprintKeyScan.WrittenForms(Convert.FromBase64String(Invocation.FingerprintKey)),
                .. FingerprintKeyScan.WrittenForms(NextFingerprintKey),
            ]);

        Assert.Equal([0, 0, 0, 0, 0], exitCodes);
        Assert.Equal([2], fingerprintVersions);
        Assert.NotEmpty(holdingTheOrigin);
        Assert.Empty(holdingTheKey);
    }

    private static JsonObject Versions(int current, params (int Version, string Key)[] versions)
    {
        var held = new JsonObject();

        foreach ((int version, string key) in versions)
        {
            held[version.ToString(CultureInfo.InvariantCulture)] = key;
        }

        return new JsonObject { ["current"] = current, ["versions"] = held };
    }

    // The document the operator pipes to a rotation, under the maintenance credential.
    private JsonObject Document(JsonObject keyEncryptionKeys, JsonObject fingerprintKeys) =>
        new()
        {
            ["connection"] = new NpgsqlConnectionStringBuilder(database.ConnectionString)
            {
                Options = "-c role=" + Maintenance,
            }.ConnectionString,
            ["keyEncryptionKeys"] = keyEncryptionKeys,
            ["fingerprintKeys"] = fingerprintKeys,
        };
}
