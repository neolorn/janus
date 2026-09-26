using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Storage.Tests;
using Npgsql;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// What the <c>replay-erasures</c> command does to a database restored to a point before
/// the erasures its ledger records: every one the restore took away is carried out
/// again, however old, and a second replay changes nothing more, a restore from a real
/// backup included (DR-016 AC3, DR-006a AC1).
/// </summary>
/// <remarks>
/// The cases share one database, so each seeds subjects of its own and reads back only
/// those.
/// </remarks>
[Trait("kind", "integration")]
public sealed class ErasureReplayTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string Command = "replay-erasures";

    private readonly string _ledger = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    /// <inheritdoc/>
    public void Dispose() => File.Delete(_ledger);

    /// <summary>
    /// DR-016 AC3, DR-006a AC1: every line of the ledger is replayed, the oldest as much
    /// as the newest; an account the restore brought back live is erased at the instant
    /// and for the reason its line records, with its host told again and the replay
    /// audited under its own principal; a repeated line and an account the restored
    /// database never held change nothing; and a second replay of the whole ledger
    /// carries out nothing more.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_016_AC3_TheWholeLedgerIsReplayedAndASecondReplayChangesNothingAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        Guid requested = await AccountAsync(connection, "active");
        Guid takenDown = await AccountAsync(connection, "suspended");
        var unknown = Guid.NewGuid();

        await WrittenAsync(
            $"2024-02-29T23:59:59Z {requested:D} erasure-request",
            $"2026-08-14T09:22:05Z {unknown:D} erasure-request",
            $"2026-09-19T16:04:00Z {takenDown:D} minor-takedown",
            $"2024-02-29T23:59:59Z {requested:D} erasure-request");

        Invocation first = await Invocation.PipedAsync([Command, _ledger], Invocation.Keys(Application()));
        Invocation second = await Invocation.PipedAsync([Command, _ledger], Invocation.Keys(Application()));

        Assert.Equal((0, string.Empty), (first.ExitCode, first.Error));
        Assert.Equal("""{"reapplied":2,"standing":1,"absent":1}""", first.Output.Trim());
        Assert.Equal((0, string.Empty), (second.ExitCode, second.Error));
        Assert.Equal("""{"reapplied":0,"standing":3,"absent":1}""", second.Output.Trim());

        Assert.Equal(
            ("deleted", "oob-request", new DateTime(2024, 2, 29, 23, 59, 59, DateTimeKind.Utc), (short)0),
            await ErasedAsync(connection, requested));
        Assert.Equal(
            ("deleted", "takedown", new DateTime(2026, 9, 19, 16, 4, 0, DateTimeKind.Utc), (short)0),
            await ErasedAsync(connection, takenDown));
        Assert.Equal(
            ["erasure-request", "minor-takedown"],
            await connection.QueryAsync<string>(
                "SELECT reason FROM identity.erasures WHERE subject = ANY(@subjects) ORDER BY requested_at",
                new { subjects = new[] { requested, takenDown } }));
        Assert.Equal(
            [
                ("erasure-requested", "erasure-request", new DateTime(2024, 2, 29, 23, 59, 59, DateTimeKind.Utc)),
                ("erasure-requested", "minor-takedown", new DateTime(2026, 9, 19, 16, 4, 0, DateTimeKind.Utc)),
            ],
            await connection.QueryAsync<(string, string, DateTime)>(
                "SELECT kind, reason, raised_at FROM identity.outbox WHERE subject = ANY(@subjects) ORDER BY raised_at",
                new { subjects = new[] { requested, takenDown } }));
        Assert.Equal(
            [("replay-erasures", "DR-016"), ("replay-erasures", "DR-016")],
            await connection.QueryAsync<(string, string)>(
                """
                SELECT principal, principal_reason FROM identity.audit_records
                WHERE action = 'privacy.erasure.executed' AND effective_subject = ANY(@subjects)
                """,
                new { subjects = new[] { requested, takenDown } }));
        Assert.Equal(
            0,
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(*)::int FROM identity.accounts WHERE subject = @unknown",
                new { unknown }));
    }

    /// <summary>
    /// DR-006a AC1: a backup taken before an erasure, restored into a new instance, brings
    /// the account back live with the key the erasure destroyed; the replay of the ledger
    /// against the restored database erases it again and destroys that key there too.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_006a_AC1_AnErasureTheRestoreTookBackIsCarriedOutAgainAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        Guid subject = await AccountAsync(connection, "active");
        byte[] live = await WrappedAsync(connection, subject);

        await using var restore = new ContainerRestore(
            await database.BackupAsync(),
            new NpgsqlConnectionStringBuilder(database.ConnectionString).Database!);

        await WrittenAsync($"2026-09-20T08:00:00Z {subject:D} erasure-request");

        Invocation erased = await Invocation.PipedAsync([Command, _ledger], Invocation.Keys(Application()));

        string restored = (await restore.RestoreAsync(TestContext.Current.CancellationToken)).Match(
            reached => reached,
            error => throw new InvalidOperationException(error.Code.ToString()));

        await using var reached = new NpgsqlConnection(restored);
        await reached.OpenAsync(TestContext.Current.CancellationToken);

        byte[] brought = await WrappedAsync(reached, subject);

        Invocation replayed = await Invocation.PipedAsync([Command, _ledger], Invocation.Keys(Application(restored)));

        Assert.Equal("""{"reapplied":1,"standing":0,"absent":0}""", erased.Output.Trim());
        Assert.Equal(live, brought);
        Assert.Equal((0, string.Empty), (replayed.ExitCode, replayed.Error));
        Assert.Equal("""{"reapplied":1,"standing":0,"absent":0}""", replayed.Output.Trim());
        Assert.Equal(
            ("deleted", "oob-request", new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc), (short)0),
            await ErasedAsync(reached, subject));
        Assert.NotEqual(live, await WrappedAsync(reached, subject));
    }

    /// <summary>
    /// DR-016 AC3: a ledger holding one line in another form is refused whole, by the
    /// line's number, before anything is written, so no replay runs over half a ledger.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_016_AC3_ALedgerWithALineInAnotherFormIsRefusedWholeAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        Guid subject = await AccountAsync(connection, "active");

        await WrittenAsync(
            $"2026-08-14T09:22:05Z {subject:D} erasure-request",
            $"2026-08-14T09:22Z  {subject:D}  erasure-request");

        Invocation refused = await Invocation.PipedAsync([Command, _ledger], Invocation.Keys(Application()));

        Assert.Equal(1, refused.ExitCode);
        Assert.Empty(refused.Output);
        Assert.Equal("""{"code":"api.request.malformed","details":{"member":"ledger","line":2}}""", refused.Error.Trim());
        Assert.Equal(
            "active",
            await connection.ExecuteScalarAsync<string>(
                "SELECT state FROM identity.accounts WHERE subject = @subject",
                new { subject }));
    }

    /// <summary>
    /// DR-016 AC3: a ledger that cannot be read, because it is not where the command was
    /// told or is not UTF-8 (a byte order mark naming another encoding included), is
    /// refused as the ledger and nothing is written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_016_AC3_AnUnreadableLedgerIsRefusedAsync()
    {
        Invocation missing = await Invocation.PipedAsync([Command, _ledger], Invocation.Keys(Application()));

        await File.WriteAllBytesAsync(_ledger, [0xFF, 0xFE, 0x20], TestContext.Current.CancellationToken);

        Invocation undecoded = await Invocation.PipedAsync([Command, _ledger], Invocation.Keys(Application()));
        Invocation unnamed = await Invocation.PipedAsync([Command], Invocation.Keys(Application()));

        Assert.All(
            [missing, undecoded, unnamed],
            refused =>
            {
                Assert.Equal(1, refused.ExitCode);
                Assert.Equal("""{"code":"api.request.malformed","details":{"member":"ledger"}}""", refused.Error.Trim());
            });
    }

    // An account as a restore to a point before its erasure brings it back, with the data
    // key its fields are held under.
    private static async Task<Guid> AccountAsync(NpgsqlConnection connection, string state)
    {
        var subject = Guid.NewGuid();

        await connection.ExecuteAsync(
            """
            INSERT INTO identity.accounts (subject, created_at, state, suspended_by)
            VALUES (@subject, now(), @state, CASE WHEN @state = 'suspended' THEN 'administrator' END);
            INSERT INTO identity.subject_keys (subject, format_marker, key_version, wrapped_key)
            VALUES (@subject, 1, 1, @wrapped);
            """,
            new { subject, state, wrapped = RandomNumberGenerator.GetBytes(40) });

        return subject;
    }

    // The account's state and deletion as the replay left them, and the wrapped key's
    // format marker, which is 0 once the key is destroyed.
    private static async Task<(string State, string DeletingBy, DateTime DeletingSince, short Marker)> ErasedAsync(
        NpgsqlConnection connection,
        Guid subject) =>
        await connection.QuerySingleAsync<(string, string, DateTime, short)>(
            """
            SELECT account.state, account.deleting_by, account.deleting_since, key.format_marker::smallint
            FROM identity.accounts account
            JOIN identity.subject_keys key ON key.subject = account.subject
            WHERE account.subject = @subject
            """,
            new { subject });

    private async Task WrittenAsync(params string[] lines) =>
        await File.WriteAllTextAsync(
            _ledger,
            string.Join('\n', lines) + "\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            TestContext.Current.CancellationToken);

    // The application's own credential, which holds every right the erasure writes with.
    private static string Application(string connectionString)
    {
        var connection = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Options = "-c role=identity_app",
        };

        return connection.ConnectionString;
    }

    // The subject's data key as it stands wrapped.
    private static async Task<byte[]> WrappedAsync(NpgsqlConnection connection, Guid subject) =>
        await connection.QuerySingleAsync<byte[]>(
            "SELECT wrapped_key FROM identity.subject_keys WHERE subject = @subject",
            new { subject });

    private string Application() => Application(database.ConnectionString);
}
