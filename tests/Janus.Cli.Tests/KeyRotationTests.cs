using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Storage.Tests;
using Npgsql;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// What the <c>rotate-kek</c> command does to a deployment's wrapped keys: every one is
/// re-wrapped under the new version in batches that survive a killed run, the escrow
/// copy is printed, and the previous version retires once the copy is sealed
/// (OPS-SEC-003, DR-009a).
/// </summary>
/// <remarks>
/// The cases share one database and run one after another, so each begins with no
/// rotation recorded and the keys it seeds itself.
/// </remarks>
[Trait("kind", "integration")]
public sealed class KeyRotationTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const string Command = "rotate-kek";

    private const string Sealed = "--sealed";

    // OPS-SEC-003, D-153: the subject keys one transaction takes.
    private const int Batch = 500;

    private const string Maintenance = "identity_maintenance";

    private static readonly byte[] Previous = RandomNumberGenerator.GetBytes(32);

    private static readonly byte[] Next = RandomNumberGenerator.GetBytes(32);

    /// <summary>
    /// OPS-SEC-003 AC1: the command is refused under the application's own credential,
    /// and under a superuser, which holds the application's rights as it holds every
    /// role's; nothing is read or written.
    /// </summary>
    /// <param name="role">The role the connection runs as, or nothing for the superuser.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("identity_app")]
    [InlineData(null)]
    public async Task OPS_SEC_003_AC1_TheCommandIsRefusedWithoutTheMaintenanceCredentialAsync(string? role)
    {
        await using NpgsqlConnection connection = await ResetAsync();
        await SeedAsync(connection, 3);

        Invocation refused = await Invocation.PipedAsync([Command], Rotating(role));

        Assert.Equal(1, refused.ExitCode);
        Assert.Empty(refused.Output);
        Assert.Equal("""{"code":"authz.denied","details":{}}""", refused.Error.Trim());
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM identity.key_rotations"));
        Assert.Equal(3, await UnderAsync(connection, 1));
    }

    /// <summary>
    /// OPS-SEC-003 AC2, AC3: a run killed mid-batch leaves the batches it committed and
    /// the point it reached; the keys it had not reached still unwrap under the previous
    /// version; run again, it resumes from that point, and when it reports complete every
    /// key is under the new version, re-wrapped once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC2_AKilledRunResumesFromItsProgressAndReWrapsEachKeyOnceAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using NpgsqlConnection connection = await ResetAsync();
        IReadOnlyDictionary<Guid, byte[]> seeded = await SeedAsync(connection, 1200);
        Guid[] ordered = [.. await connection.QueryAsync<Guid>("SELECT subject FROM identity.subject_keys ORDER BY subject")];

        // A key of the second batch is held by another transaction, so the run stops on
        // it with the first batch committed and the second in hand.
        await using NpgsqlConnection holder = await database.OpenAsync();
        await using NpgsqlTransaction holding = await holder.BeginTransactionAsync(cancellationToken);
        await holder.ExecuteAsync(
            "SELECT 1 FROM identity.subject_keys WHERE subject = @subject FOR UPDATE",
            new { subject = ordered[Batch + 100] },
            holding);

        using var killed = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<Invocation> run = Invocation.StoppedAsync([Command], Rotating(), killed.Token);

        await ProcessedAsync(connection, Batch);
        await killed.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        await holding.RollbackAsync(cancellationToken);

        (int processed, Guid last) = await connection.QuerySingleAsync<(int, Guid)>(
            "SELECT processed, last_subject FROM identity.key_rotations");

        Assert.Equal(Batch, processed);
        Assert.Equal(ordered[Batch - 1], last);
        Assert.Equal(ordered[..Batch], await SubjectsUnderAsync(connection, 2));

        foreach ((Guid subject, byte[] wrapped) in await WrappedUnderAsync(connection, 1))
        {
            Assert.Equal(seeded[subject], Unwrapped(wrapped, Previous));
        }

        Invocation resumed = await Invocation.PipedAsync([Command], Rotating());

        Assert.Equal(0, resumed.ExitCode);
        Assert.Equal("""{"version":2,"processed":1200}""", Lines(resumed)[^1]);
        Assert.Equal(0, await UnderAsync(connection, 1));

        foreach ((Guid subject, byte[] wrapped) in await WrappedUnderAsync(connection, 2))
        {
            Assert.Equal(seeded[subject], Unwrapped(wrapped, Next));
        }

        Assert.Equal(
            [("ops.keyrotation.started", 0), ("ops.keyrotation.resumed", Batch), ("ops.keyrotation.completed", 1200)],
            await RecordedAsync(connection));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC18 and OPS-SEC-003 (D-174): the rotation walks the subject-key
    /// table in key order, and its point in it is a row of that table, not a subject, so
    /// the last row it reaches is the deployment's data key under the max UUID, which it
    /// re-wraps and records as it does any other.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC18_TheRotationsPointReachesTheDeploymentKeysRowAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        IReadOnlyDictionary<Guid, byte[]> seeded = await SeedAsync(connection, 3);
        byte[] deploymentKey = await DeploymentKeyAsync(connection);

        Assert.Equal(0, (await Invocation.PipedAsync([Command], Rotating())).ExitCode);

        Assert.Equal(
            Guid.AllBitsSet,
            await connection.QuerySingleAsync<Guid>("SELECT last_subject FROM identity.key_rotations"));
        Assert.Equal(4, await UnderAsync(connection, 2));

        foreach ((Guid subject, byte[] wrapped) in await WrappedUnderAsync(connection, 2))
        {
            Assert.Equal(subject == Guid.AllBitsSet ? deploymentKey : seeded[subject], Unwrapped(wrapped, Next));
        }
    }

    /// <summary>
    /// OPS-SEC-003 AC3, PRIV-RIGHT-005a AC13 and AC16: once the rotation retires the
    /// previous version, no row of the subject-key table is wrapped under it, the
    /// deployment's data key among them, and every value that belongs to no subject
    /// reads under that key as it did before (D-166, 316).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC3_AfterRetirementEveryValueOfNoSubjectStillReadsAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        IReadOnlyDictionary<Guid, byte[]> seeded = await SeedAsync(connection, 3);
        byte[] deploymentKey = await DeploymentKeyAsync(connection);
        byte[] signing = RandomNumberGenerator.GetBytes(121);
        byte[] message = RandomNumberGenerator.GetBytes(32);

        await connection.ExecuteAsync(
            """
            INSERT INTO identity.signing_keys (key_id, algorithm, public_key, private_key, created_at)
            VALUES ('k1', 'ES256', '\x00', @signing, now());
            INSERT INTO identity.send_outbox (id, recorded_at, wrapped_key, enc_message)
            VALUES (gen_random_uuid(), now(), @message, '\x00');
            """,
            new { signing = Wrapped(signing, deploymentKey), message = Wrapped(message, deploymentKey) });

        Assert.Equal(0, (await Invocation.PipedAsync([Command], Rotating())).ExitCode);
        Assert.Equal(0, (await Invocation.PipedAsync([Command, Sealed], Rotating())).ExitCode);

        Assert.Equal([2], await connection.QueryAsync<int>("SELECT DISTINCT key_version FROM identity.subject_keys"));
        Assert.Equal(
            signing,
            Unwrapped(await connection.QuerySingleAsync<byte[]>("SELECT private_key FROM identity.signing_keys"), deploymentKey));
        Assert.Equal(
            message,
            Unwrapped(await connection.QuerySingleAsync<byte[]>("SELECT wrapped_key FROM identity.send_outbox"), deploymentKey));

        foreach ((Guid subject, byte[] wrapped) in await WrappedUnderAsync(connection, 2))
        {
            Assert.Equal(subject == Guid.AllBitsSet ? deploymentKey : seeded[subject], Unwrapped(wrapped, Next));
        }
    }

    /// <summary>
    /// OPS-SEC-003 AC1, OPS-SEC-002: a client's secrets, the current one and the one it
    /// replaced, are held under the deployment's data key, so the rotation re-wraps that
    /// key, leaves the secrets as they were, and both read after the retirement as they
    /// did before (D-166, 340).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC1_AKeyRotationReWrapsTheClientSecretsAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        _ = await SeedAsync(connection, 1);
        byte[] deploymentKey = await DeploymentKeyAsync(connection);
        byte[] current = RandomNumberGenerator.GetBytes(43);
        byte[] replaced = RandomNumberGenerator.GetBytes(43);

        await connection.ExecuteAsync(
            """
            DELETE FROM identity.oidc_clients WHERE client_id = 'rotated-client';
            INSERT INTO identity.oidc_clients
                (client_id, name, kind, redirect, scopes, secret, secret_issued_at, previous_secret, previous_secret_until)
            VALUES ('rotated-client', 'Rotated client', 'protocol', 'https://mail.example.test/callback',
                ARRAY['openid'], @current, now(), @replaced, now() + interval '15 minutes');
            """,
            new { current = Wrapped(current, deploymentKey), replaced = Wrapped(replaced, deploymentKey) });

        (byte[] Secret, byte[] Previous) before = await SecretsAsync(connection);

        Assert.Equal(0, (await Invocation.PipedAsync([Command], Rotating())).ExitCode);
        Assert.Equal(0, (await Invocation.PipedAsync([Command, Sealed], Rotating())).ExitCode);

        (byte[] Secret, byte[] Previous) after = await SecretsAsync(connection);
        byte[] rewrapped = await connection.QuerySingleAsync<byte[]>(
            "SELECT wrapped_key FROM identity.subject_keys WHERE subject = @reserved AND key_version = 2",
            new { reserved = Guid.AllBitsSet });
        byte[] unwrapped = Unwrapped(rewrapped, Next);

        Assert.Equal(before.Secret, after.Secret);
        Assert.Equal(before.Previous, after.Previous);
        Assert.Equal(deploymentKey, unwrapped);
        Assert.Equal(current, Unwrapped(after.Secret, unwrapped));
        Assert.Equal(replaced, Unwrapped(after.Previous, unwrapped));
    }

    /// <summary>
    /// OPS-SEC-003 AC3 and IDN-LIFE-012: the proof key a round trip to a social provider
    /// holds while the browser is away is under the deployment's data key, so the
    /// rotation leaves it where it is and it reads after the retirement as it did
    /// before (D-166, 316).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC3_AProofKeyInFlightStillReadsAfterRetirementAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        byte[] deploymentKey = await DeploymentKeyAsync(connection);
        byte[] verifier = RandomNumberGenerator.GetBytes(43);
        byte[] browser = RandomNumberGenerator.GetBytes(32);
        byte[] wrapped = Wrapped(verifier, deploymentKey);

        await connection.ExecuteAsync(
            """
            INSERT INTO identity.preauthentication_sessions (fingerprint, csrf_fingerprint, created_at, expires_at)
            VALUES (@browser, @csrf, now(), now() + interval '1 hour');
            INSERT INTO identity.provider_attempts
                (id, preauthentication, provider, intent, state, nonce, verifier, return_to, created_at)
            VALUES (gen_random_uuid(), @browser, 'google', 'signin', @state, @nonce, @wrapped, '/', now());
            """,
            new
            {
                browser,
                csrf = RandomNumberGenerator.GetBytes(32),
                state = RandomNumberGenerator.GetBytes(32),
                nonce = RandomNumberGenerator.GetBytes(32),
                wrapped,
            });

        Assert.Equal(0, (await Invocation.PipedAsync([Command], Rotating())).ExitCode);
        Assert.Equal(0, (await Invocation.PipedAsync([Command, Sealed], Rotating())).ExitCode);

        byte[] held = await connection.QuerySingleAsync<byte[]>("SELECT verifier FROM identity.provider_attempts");

        Assert.Equal(wrapped, held);
        Assert.Equal(verifier, Unwrapped(held, deploymentKey));
    }

    /// <summary>
    /// OPS-SEC-003, OPS-MIG-003a AC4: the rotation writes to the subject-key table and to
    /// its own progress, appends to the trail (AC5), and leaves every other table as it
    /// found it, whatever those hold under the deployment's data key (D-166, 316).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_ARotationTouchesNoTableButTheSubjectKeysAndItsProgressAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        await SeedAsync(connection, 3);
        byte[] deploymentKey = await DeploymentKeyAsync(connection);

        await connection.ExecuteAsync(
            """
            INSERT INTO identity.signing_keys (key_id, algorithm, public_key, private_key, created_at)
            VALUES ('k1', 'ES256', '\x00', @wrapped, now());
            """,
            new { wrapped = Wrapped(RandomNumberGenerator.GetBytes(121), deploymentKey) });

        IReadOnlyList<(string Table, string? Digest)> before = await TablesAsync(connection);

        Assert.Equal(0, (await Invocation.PipedAsync([Command], Rotating())).ExitCode);
        Assert.Equal(0, (await Invocation.PipedAsync([Command, Sealed], Rotating())).ExitCode);

        Assert.Equal(before, await TablesAsync(connection));
    }

    /// <summary>
    /// OPS-SEC-003, PRIV-RIGHT-005a: a key rewritten at the same version between the
    /// rotation's read and its write is not overwritten with what the rotation read; the
    /// rotation finds it again and re-wraps the value that stands (D-166, 316).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_ARotationDoesNotOverwriteAKeyRewrittenAtTheSameVersionAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using NpgsqlConnection connection = await ResetAsync();
        Guid subject = (await SeedAsync(connection, 1)).Keys.Single();
        byte[] rewritten = RandomNumberGenerator.GetBytes(32);

        // The row is held by another transaction, so the run reads it and waits on it
        // with its write; the holder rewrites it at the same version meanwhile.
        await using NpgsqlConnection holder = await database.OpenAsync();
        await using NpgsqlTransaction holding = await holder.BeginTransactionAsync(cancellationToken);
        await holder.ExecuteAsync(
            "UPDATE identity.subject_keys SET wrapped_key = @wrapped WHERE subject = @subject",
            new { subject, wrapped = Wrapped(rewritten, Previous) },
            holding);

        Task<Invocation> run = Invocation.PipedAsync([Command], Rotating());

        await WaitingAsync(connection);
        await holding.CommitAsync(cancellationToken);

        Assert.Equal(0, (await run).ExitCode);
        Assert.Equal(
            rewritten,
            Unwrapped(
                await connection.QuerySingleAsync<byte[]>(
                    "SELECT wrapped_key FROM identity.subject_keys WHERE subject = @subject AND key_version = 2",
                    new { subject }),
                Next));
    }

    /// <summary>
    /// OPS-SEC-003 AC4: the command prints the escrow copy of the new version, the
    /// rotation stays unretired until the seal is confirmed, and the confirmation
    /// retires the previous version.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC4_TheEscrowCopyIsPrintedAndTheRotationRetiresOnlyOnceItIsSealedAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        await SeedAsync(connection, 3);

        Invocation rotated = await Invocation.PipedAsync([Command], Rotating());

        Assert.Equal(0, rotated.ExitCode);
        Assert.True(JsonNode.DeepEquals(
            new JsonObject
            {
                ["keyEncryptionKeys"] = new JsonObject
                {
                    ["current"] = 2,
                    ["versions"] = new JsonObject { ["2"] = Convert.ToBase64String(Next) },
                },
            },
            JsonNode.Parse(Lines(rotated)[0])));
        Assert.Equal("""{"version":2,"processed":3}""", Lines(rotated)[1]);
        Assert.Null(await connection.ExecuteScalarAsync<DateTime?>("SELECT retired_at FROM identity.key_rotations"));

        Invocation sealedCopy = await Invocation.PipedAsync([Command, Sealed], Rotating());

        Assert.Equal(0, sealedCopy.ExitCode);
        Assert.Equal("""{"version":2,"processed":3,"retired":[1]}""", sealedCopy.Output.Trim());
        Assert.NotNull(await connection.ExecuteScalarAsync<DateTime?>("SELECT retired_at FROM identity.key_rotations"));
    }

    /// <summary>
    /// OPS-SEC-003 AC4: a seal confirmed before the command has produced a copy to seal
    /// is refused and retires nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC4_ASealBeforeTheRotationCompletesIsRefusedAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        await SeedAsync(connection, 3);

        Invocation refused = await Invocation.PipedAsync([Command, Sealed], Rotating());

        Assert.Equal(1, refused.ExitCode);
        Assert.Equal("""{"code":"api.request.malformed","details":{"member":"sealed"}}""", refused.Error.Trim());
        Assert.Equal(3, await UnderAsync(connection, 1));
    }

    /// <summary>
    /// OPS-SEC-003 AC5: the start, the completion and the retirement are recorded with the
    /// key, the version, the count processed and the principal that ran them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC5_EachStepIsRecordedWithTheVersionTheCountAndThePrincipalAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        await SeedAsync(connection, 4);

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
        Assert.All(recorded, row => Assert.Equal(("rotate-kek", "OPS-SEC-003", Guid.Empty), (row.Principal, row.Reason, row.Acting)));

        using var retired = JsonDocument.Parse(recorded[^1].Details);

        Assert.Equal("key-encryption-key", retired.RootElement.GetProperty("kind").GetString());
        Assert.Equal(2, retired.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(4, retired.RootElement.GetProperty("processed").GetInt32());
        Assert.Equal([1], retired.RootElement.GetProperty("retired").EnumerateArray().Select(version => version.GetInt32()));
    }

    /// <summary>
    /// OPS-SEC-003 AC3: while something still wraps under the previous version, which is
    /// an application not yet handed the new one, the seal is refused and the version
    /// stays; what was found is re-wrapped, and once nothing more appears the seal
    /// retires it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC3_RetirementWaitsWhileValuesAreStillWrappedUnderThePreviousVersionAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        await SeedAsync(connection, 2);

        Assert.Equal(0, (await Invocation.PipedAsync([Command], Rotating())).ExitCode);

        await SeedAsync(connection, 1);

        Invocation refused = await Invocation.PipedAsync([Command, Sealed], Rotating());

        Assert.Equal(1, refused.ExitCode);
        Assert.Equal(
            """{"code":"api.request.malformed","details":{"member":"sealed","pending":1}}""",
            refused.Error.Trim());
        Assert.Null(await connection.ExecuteScalarAsync<DateTime?>("SELECT retired_at FROM identity.key_rotations"));
        Assert.Equal(0, await UnderAsync(connection, 1));

        Invocation retired = await Invocation.PipedAsync([Command, Sealed], Rotating());

        Assert.Equal(0, retired.ExitCode);
        Assert.Equal("""{"version":2,"processed":3,"retired":[1]}""", retired.Output.Trim());
    }

    /// <summary>
    /// OPS-SEC-003: a rotation is to a version no rotation has reached, and runs only
    /// where it holds every version a value is wrapped under.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_ARotationNeedsANewVersionAndEveryVersionInUseAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        await SeedAsync(connection, 2);

        Invocation missing = await Invocation.PipedAsync([Command], Document(Maintenance, 2, (2, Next)));

        Assert.Equal(1, missing.ExitCode);
        Assert.Equal(
            """{"code":"model.startup.secretunavailable","details":{"key":"keyEncryptionKeys"}}""",
            missing.Error.Trim());
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM identity.key_rotations"));

        Assert.Equal(0, (await Invocation.PipedAsync([Command], Rotating())).ExitCode);
        Assert.Equal(0, (await Invocation.PipedAsync([Command, Sealed], Rotating())).ExitCode);

        Invocation again = await Invocation.PipedAsync([Command], Rotating());

        Assert.Equal(1, again.ExitCode);
        Assert.Equal(missing.Error, again.Error);
    }

    /// <summary>
    /// DR-009a AC2: the rotation re-wraps key material and never re-encrypts customer
    /// data. Every encrypted column reads byte for byte as it did before, and what a
    /// re-wrapped key protects still decrypts under it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC2_RotationLeavesEveryCiphertextAsItWasAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        await SeedAsync(connection, 3);
        byte[] deploymentKey = await DeploymentKeyAsync(connection);
        byte[] message = RandomNumberGenerator.GetBytes(32);
        byte[] ciphertext = RandomNumberGenerator.GetBytes(64);

        await connection.ExecuteAsync(
            """
            INSERT INTO identity.send_outbox (id, recorded_at, wrapped_key, enc_message)
            VALUES (gen_random_uuid(), now(), @wrapped, @ciphertext);
            """,
            new { wrapped = Wrapped(message, deploymentKey), ciphertext });

        IReadOnlyList<(string Column, string? Digest)> before = await CiphertextsAsync(connection);

        Assert.Equal(0, (await Invocation.PipedAsync([Command], Rotating())).ExitCode);
        Assert.Equal(0, (await Invocation.PipedAsync([Command, Sealed], Rotating())).ExitCode);

        Assert.Equal(before, await CiphertextsAsync(connection));
        Assert.Equal(ciphertext, await connection.QuerySingleAsync<byte[]>("SELECT enc_message FROM identity.send_outbox"));
        Assert.Equal(
            message,
            Unwrapped(await connection.QuerySingleAsync<byte[]>("SELECT wrapped_key FROM identity.send_outbox"), deploymentKey));
    }

    /// <summary>
    /// DR-009a AC3: a rotation out of cycle is run on demand. The command waits on no
    /// calendar, so a rotation started the moment another retired completes as the
    /// first did.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC3_ARotationRunsOnDemandOutOfCycleAsync()
    {
        byte[] suspected = RandomNumberGenerator.GetBytes(32);

        await using NpgsqlConnection connection = await ResetAsync();
        IReadOnlyDictionary<Guid, byte[]> seeded = await SeedAsync(connection, 3);

        Assert.Equal(0, (await Invocation.PipedAsync([Command], Rotating())).ExitCode);
        Assert.Equal(0, (await Invocation.PipedAsync([Command, Sealed], Rotating())).ExitCode);

        JsonObject outOfCycle = Document(Maintenance, 3, (2, Next), (3, suspected));

        Invocation rotated = await Invocation.PipedAsync([Command], outOfCycle);
        Invocation retired = await Invocation.PipedAsync([Command, Sealed], outOfCycle);

        Assert.Equal("""{"version":3,"processed":3}""", Lines(rotated)[^1]);
        Assert.Equal("""{"version":3,"processed":3,"retired":[2]}""", retired.Output.Trim());

        foreach ((Guid subject, byte[] wrapped) in await WrappedUnderAsync(connection, 3))
        {
            Assert.Equal(seeded[subject], Unwrapped(wrapped, suspected));
        }
    }

    /// <summary>
    /// DR-009a AC4: the escrowed copy is replaced in the same operation. The run that
    /// rotates prints the copy of the new version and nothing of the one it replaces, and
    /// the operation retires the previous version only once that copy is sealed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_009a_AC4_TheOperationThatRotatesReplacesTheEscrowCopyAsync()
    {
        await using NpgsqlConnection connection = await ResetAsync();
        await SeedAsync(connection, 2);

        Invocation rotated = await Invocation.PipedAsync([Command], Rotating());
        var copy = JsonNode.Parse(Lines(rotated)[0]);
        Invocation retired = await Invocation.PipedAsync([Command, Sealed], Rotating());

        Assert.Equal(
            ["2"],
            copy?["keyEncryptionKeys"]?["versions"]?.AsObject().Select(version => version.Key) ?? []);
        Assert.DoesNotContain(Convert.ToBase64String(Previous), rotated.Output, StringComparison.Ordinal);
        Assert.Equal("""{"version":2,"processed":2,"retired":[1]}""", retired.Output.Trim());
    }

    // A digest of every encrypted column of every table, by its name. The trail is
    // appended to and never changed, so it is left out.
    private static async Task<IReadOnlyList<(string Column, string? Digest)>> CiphertextsAsync(NpgsqlConnection connection)
    {
        IReadOnlyList<(string Table, string Column)> columns = [.. await connection.QueryAsync<(string, string)>(
            """
            SELECT table_name, column_name FROM information_schema.columns
            WHERE table_schema = 'identity' AND column_name LIKE 'enc\_%'
              AND table_name NOT LIKE 'audit\_records%'
            ORDER BY table_name, column_name
            """)];

        var digests = new List<(string, string?)>(columns.Count);

        foreach ((string table, string column) in columns)
        {
            digests.Add((
                table + "." + column,
                await connection.ExecuteScalarAsync<string?>(
                    $"SELECT md5(string_agg(encode({column}, 'hex'), ',' ORDER BY encode({column}, 'hex'))) FROM identity.{table}")));
        }

        return digests;
    }

    // A digest of every table of the library's schema but the subject keys, the
    // rotation's progress and the trail, by its name.
    private static async Task<IReadOnlyList<(string Table, string? Digest)>> TablesAsync(NpgsqlConnection connection)
    {
        IReadOnlyList<string> tables = [.. await connection.QueryAsync<string>(
            """
            SELECT table_name FROM information_schema.tables
            WHERE table_schema = 'identity' AND table_type = 'BASE TABLE'
              AND table_name NOT IN ('subject_keys', 'key_rotations')
              AND table_name NOT LIKE 'audit\_records%'
            ORDER BY table_name
            """)];

        var digests = new List<(string, string?)>(tables.Count);

        foreach (string table in tables)
        {
            digests.Add((
                table,
                await connection.ExecuteScalarAsync<string?>(
                    $"SELECT md5(string_agg(held::text, ',' ORDER BY held::text)) FROM identity.{table} AS held")));
        }

        return digests;
    }

    // The deployment's data key, as the application writes it the first time a value of
    // no subject needs it: a row of the subject-key table under the max UUID, wrapped
    // under the previous version.
    private static async Task<byte[]> DeploymentKeyAsync(NpgsqlConnection connection)
    {
        byte[] deploymentKey = RandomNumberGenerator.GetBytes(32);

        await connection.ExecuteAsync(
            """
            INSERT INTO identity.subject_keys (subject, format_marker, key_version, wrapped_key)
            VALUES (@reserved, 1, 1, @wrapped);
            """,
            new { reserved = Guid.AllBitsSet, wrapped = Wrapped(deploymentKey, Previous) });

        return deploymentKey;
    }

    private static async Task<(byte[] Secret, byte[] Previous)> SecretsAsync(NpgsqlConnection connection) =>
        await connection.QuerySingleAsync<(byte[], byte[])>(
            "SELECT secret, previous_secret FROM identity.oidc_clients WHERE client_id = 'rotated-client'");

    private static byte[] Wrapped(byte[] value, byte[] wrappingKey)
    {
        using var aes = Aes.Create();
        aes.Key = wrappingKey;

        return aes.EncryptKeyWrapPadded(value);
    }

    private static byte[] Unwrapped(byte[] wrapped, byte[] wrappingKey)
    {
        using var aes = Aes.Create();
        aes.Key = wrappingKey;

        return aes.DecryptKeyWrapPadded(wrapped);
    }

    private static string[] Lines(Invocation invocation) =>
        invocation.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static async Task<int> UnderAsync(NpgsqlConnection connection, int version) =>
        await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM identity.subject_keys WHERE key_version = @version",
            new { version });

    private static async Task<Guid[]> SubjectsUnderAsync(NpgsqlConnection connection, int version) =>
        [.. await connection.QueryAsync<Guid>(
            "SELECT subject FROM identity.subject_keys WHERE key_version = @version ORDER BY subject",
            new { version })];

    private static async Task<IReadOnlyList<(Guid Subject, byte[] Wrapped)>> WrappedUnderAsync(
        NpgsqlConnection connection,
        int version) =>
        [.. await connection.QueryAsync<(Guid, byte[])>(
            "SELECT subject, wrapped_key FROM identity.subject_keys WHERE key_version = @version",
            new { version })];

    private static async Task<IReadOnlyList<(string, int)>> RecordedAsync(NpgsqlConnection connection) =>
        [.. await connection.QueryAsync<(string, int)>(
            """
            SELECT action, (details->>'processed')::int
            FROM identity.audit_records
            WHERE action LIKE 'ops.keyrotation.%'
            ORDER BY occurred_at, id
            """)];

    // Waits until the running command waits on a row another transaction holds.
    private static async Task WaitingAsync(NpgsqlConnection connection)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        patience.CancelAfter(TimeSpan.FromSeconds(60));

        while (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM pg_locks WHERE NOT granted") == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), patience.Token);
        }
    }

    // Waits until the running command has committed the given count.
    private static async Task ProcessedAsync(NpgsqlConnection connection, int processed)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        patience.CancelAfter(TimeSpan.FromSeconds(60));

        while (await connection.ExecuteScalarAsync<int?>("SELECT max(processed) FROM identity.key_rotations") != processed)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), patience.Token);
        }
    }

    // Each subject's own data key, wrapped under the previous version as an application
    // holding only that version wraps it.
    private static async Task<IReadOnlyDictionary<Guid, byte[]>> SeedAsync(NpgsqlConnection connection, int count)
    {
        var seeded = new Dictionary<Guid, byte[]>(count);

        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        for (int at = 0; at < count; at++)
        {
            var subject = Guid.NewGuid();
            byte[] dataKey = RandomNumberGenerator.GetBytes(32);

            await connection.ExecuteAsync(
                """
                INSERT INTO identity.subject_keys (subject, format_marker, key_version, wrapped_key)
                VALUES (@subject, 1, 1, @wrapped);
                """,
                new { subject, wrapped = Wrapped(dataKey, Previous) },
                transaction);

            seeded[subject] = dataKey;
        }

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return seeded;
    }

    private async Task<NpgsqlConnection> ResetAsync()
    {
        NpgsqlConnection connection = await database.OpenAsync();

        await connection.ExecuteAsync(
            """
            TRUNCATE identity.key_rotations, identity.subject_keys, identity.signing_keys, identity.send_outbox,
                identity.provider_attempts, identity.preauthentication_sessions;
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
            ["keyEncryptionKeys"] = new JsonObject { ["current"] = current, ["versions"] = held },
            ["fingerprintKeys"] = new JsonObject
            {
                ["current"] = 1,
                ["versions"] = new JsonObject { ["1"] = Invocation.FingerprintKey },
            },
        };
    }
}
