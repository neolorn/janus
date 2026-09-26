using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Dapper;
using Janus.Storage.Tests;
using Npgsql;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// What the <c>rotate-fingerprint-key</c> command does, in the shape of
/// <c>rotate-kek</c>: it runs only under the maintenance credential, prints the escrow
/// copy of the new version, retires the previous one once the copy is sealed, and
/// records each step (OPS-SEC-003 AC6). What it does to each fingerprint is tested
/// where the fingerprints are written.
/// </summary>
/// <remarks>
/// The cases share one database and run one after another, so each begins with no
/// rotation recorded and the ledger lines it seeds itself.
/// </remarks>
[Trait("kind", "integration")]
public sealed class FingerprintKeyRotationTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const string Command = "rotate-fingerprint-key";

    private const string Sealed = "--sealed";

    private const string Maintenance = "identity_maintenance";

    private static readonly byte[] Previous = RandomNumberGenerator.GetBytes(32);

    private static readonly byte[] Next = RandomNumberGenerator.GetBytes(32);

    /// <summary>
    /// OPS-SEC-003 AC1, AC6: the command is refused under the application's own
    /// credential, and under a superuser, which holds the application's rights as it
    /// holds every role's; nothing is read or written.
    /// </summary>
    /// <param name="role">The role the connection runs as, or nothing for the superuser.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("identity_app")]
    [InlineData(null)]
    public async Task OPS_SEC_003_AC6_TheCommandIsRefusedWithoutTheMaintenanceCredentialAsync(string? role)
    {
        await using NpgsqlConnection connection = await ResetAsync();
        await CountedAsync(connection, 1);

        Invocation refused = await Invocation.PipedAsync([Command], Rotating(role));

        Assert.Equal(1, refused.ExitCode);
        Assert.Empty(refused.Output);
        Assert.Equal("""{"code":"authz.denied","details":{}}""", refused.Error.Trim());
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM identity.key_rotations"));
    }

    /// <summary>
    /// OPS-SEC-003 AC4, AC6: the command prints the escrow copy of the new version, the
    /// rotation stays unretired until the seal is confirmed, and the confirmation retires
    /// the previous version and forgets what was hashed under it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_TheEscrowCopyIsPrintedAndTheRotationRetiresOnlyOnceItIsSealedAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        await CountedAsync(connection, 1);

        Invocation rotated = await Invocation.PipedAsync([Command], Rotating());

        Assert.Equal(0, rotated.ExitCode);
        Assert.True(JsonNode.DeepEquals(
            new JsonObject
            {
                ["fingerprintKeys"] = new JsonObject
                {
                    ["current"] = 2,
                    ["versions"] = new JsonObject { ["2"] = Convert.ToBase64String(Next) },
                },
            },
            JsonNode.Parse(Lines(rotated)[0])));
        Assert.Equal("""{"version":2,"processed":0}""", Lines(rotated)[1]);
        Assert.Null(await connection.ExecuteScalarAsync<DateTime?>("SELECT retired_at FROM identity.key_rotations"));

        await CountedAsync(connection, 2);

        Invocation sealedCopy = await Invocation.PipedAsync([Command, Sealed], Rotating());

        Assert.Equal(0, sealedCopy.ExitCode);
        Assert.Equal("""{"version":2,"processed":0,"retired":[1]}""", sealedCopy.Output.Trim());
        Assert.NotNull(await connection.ExecuteScalarAsync<DateTime?>("SELECT retired_at FROM identity.key_rotations"));
        Assert.Equal(
            [2],
            await connection.QueryAsync<int>("SELECT fingerprint_version FROM identity.throttle_counters"));
    }

    /// <summary>
    /// OPS-SEC-003 AC4, AC6: a seal confirmed before the command has produced a copy to
    /// seal is refused and retires nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_ASealBeforeTheRotationCompletesIsRefusedAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        await CountedAsync(connection, 1);

        Invocation refused = await Invocation.PipedAsync([Command, Sealed], Rotating());

        Assert.Equal(1, refused.ExitCode);
        Assert.Equal("""{"code":"api.request.malformed","details":{"member":"sealed"}}""", refused.Error.Trim());
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM identity.throttle_counters"));
    }

    /// <summary>
    /// OPS-SEC-003 AC5, AC6: the start, the completion and the retirement are recorded
    /// with the key, the version, the count processed and the principal that ran them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_EachStepIsRecordedWithTheVersionTheCountAndThePrincipalAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        await CountedAsync(connection, 1);

        await Invocation.PipedAsync([Command], Rotating());
        await Invocation.PipedAsync([Command, Sealed], Rotating());

        IReadOnlyList<(string Action, string Principal, string Reason, Guid Acting, string Details)> recorded =
            [.. await connection.QueryAsync<(string, string, string, Guid, string)>(
                """
                SELECT action, principal, principal_reason, acting_subject, details::text
                FROM identity.audit_records
                WHERE action LIKE 'ops.keyrotation.%'
                ORDER BY occurred_at, id
                """)];

        Assert.Equal(
            ["ops.keyrotation.started", "ops.keyrotation.completed", "ops.keyrotation.retired"],
            recorded.Select(row => row.Action));
        Assert.All(
            recorded,
            row => Assert.Equal(("rotate-fingerprint-key", "OPS-SEC-003", Guid.Empty), (row.Principal, row.Reason, row.Acting)));

        using var retired = JsonDocument.Parse(recorded[^1].Details);

        Assert.Equal("fingerprint-key", retired.RootElement.GetProperty("kind").GetString());
        Assert.Equal(2, retired.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(0, retired.RootElement.GetProperty("processed").GetInt32());
        Assert.Equal([1], retired.RootElement.GetProperty("retired").EnumerateArray().Select(version => version.GetInt32()));
    }

    /// <summary>
    /// OPS-SEC-003 AC6: a rotation is to a version no rotation has reached, and runs only
    /// where it holds every version a fingerprint still read is under.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_ARotationNeedsANewVersionAndEveryVersionInUseAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        await CountedAsync(connection, 1);

        Invocation missing = await Invocation.PipedAsync([Command], Document(Maintenance, 2, (2, Next)));

        Assert.Equal(1, missing.ExitCode);
        Assert.Equal(
            """{"code":"model.startup.kekunavailable","details":{"member":"fingerprintKeys"}}""",
            missing.Error.Trim());
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM identity.key_rotations"));

        Assert.Equal(0, (await Invocation.PipedAsync([Command], Rotating())).ExitCode);
        Assert.Equal(0, (await Invocation.PipedAsync([Command, Sealed], Rotating())).ExitCode);

        Invocation again = await Invocation.PipedAsync([Command], Rotating());

        Assert.Equal(1, again.ExitCode);
        Assert.Equal(missing.Error, again.Error);
    }

    private static string[] Lines(Invocation invocation) =>
        invocation.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // A throttle counter hashed under the given version, as an application holding that
    // version as current counts one.
    private static async Task CountedAsync(NpgsqlConnection connection, int version) =>
        await connection.ExecuteAsync(
            """
            INSERT INTO identity.throttle_counters (scope, key, fingerprint_version, failures, at)
            VALUES ('source', @key, @version, 1, now());
            """,
            new { key = RandomNumberGenerator.GetBytes(32), version });

    private async Task<NpgsqlConnection> ResetAsync()
    {
        NpgsqlConnection connection = await database.OpenAsync();

        await connection.ExecuteAsync(
            """
            TRUNCATE identity.key_rotations, identity.throttle_counters, identity.username_holds;
            DELETE FROM identity.audit_records WHERE action LIKE 'ops.keyrotation.%';
            """);

        return connection;
    }

    // The document the operator pipes once the new version is in the secrets manager as
    // current and the previous one is kept beside it.
    private JsonObject Rotating(string? role = Maintenance) => Document(role, 2, (1, Previous), (2, Next));

    private JsonObject Document(string? role, int current, params (int Version, byte[] Key)[] versions)
    {
        var connection = new NpgsqlConnectionStringBuilder(database.ConnectionString);

        if (role is not null)
        {
            connection.Options = "-c role=" + role;
        }

        var held = new JsonObject();

        foreach ((int version, byte[] key) in versions)
        {
            held[version.ToString(CultureInfo.InvariantCulture)] = Convert.ToBase64String(key);
        }

        return new JsonObject
        {
            ["connection"] = connection.ConnectionString,
            ["keyEncryptionKeys"] = new JsonObject
            {
                ["current"] = 1,
                ["versions"] = new JsonObject { ["1"] = Invocation.KeyEncryptionKey },
            },
            ["fingerprintKeys"] = new JsonObject { ["current"] = current, ["versions"] = held },
        };
    }
}
