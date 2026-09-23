using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Identity.Audit;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Identity.Audit;
using Janus.Storage.Privacy.Breaches;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The audit trail as its partitioned table carries it (IDN-AUD-001, PRIV-RET-002,
/// PRIV-RET-003, PRIV-RET-004, IDN-PRIN-003).
/// </summary>
/// <remarks>
/// The port implementation is tested against the aggregate it translates, with the real
/// database (D-156). Every instant here is inside a month the migration created, which
/// is the window the sweep keeps open.
/// </remarks>
[Trait("kind", "integration")]
public sealed class AuditStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly AuditAction Suspended = AuditAction.Parse("identity.account.suspended");

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// IDN-AUD-001 AC1: both identity fields are on the record, and an event about a
    /// principal that holds a membership carries its organization.
    /// </summary>
    [Fact]
    public async Task IDN_AUD_001_AC1_BothIdentityFieldsArePopulatedAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Now());
        SubjectId administrator = await _deployment.AccountAsync(Now());
        var organization = new OrganizationId(Guid.CreateVersion7());

        await AppendAsync(AuditRecord.Of(
            NewId(),
            AuditCategory.Security,
            Suspended,
            Now(),
            administrator,
            subject,
            organization));

        AuditRecord read = await OneAsync(subject);

        Assert.Equal(administrator, read.ActingSubject);
        Assert.Equal(subject, read.EffectiveSubject);
        Assert.Equal(organization, read.Organization);
    }

    /// <summary>
    /// IDN-AUD-001 AC1: an event that is not an authorization refusal is refused by
    /// the database without both identities, so the one action that may name nobody
    /// is the only one that can (AUTHZ-CONCEAL-004).
    /// </summary>
    [Fact]
    public async Task IDN_AUD_001_AC1_AnEventNamingNobodyIsRefusedByTheDatabaseAsync()
    {
        PostgresException refused = await Assert.ThrowsAsync<PostgresException>(
            async () => await WriteAsync(Suspended.ToString()));

        Assert.Equal("23514", refused.SqlState);
        Assert.Equal(1, await WriteAsync("authz.access.denied"));
    }

    /// <summary>
    /// IDN-AUD-001: an event on a principal holding no membership carries no
    /// organization, and the absence is the recorded fact.
    /// </summary>
    [Fact]
    public async Task IDN_AUD_001_ACustomersEventCarriesNoOrganizationAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Now());

        await AppendAsync(AuditRecord.Of(
            NewId(),
            AuditCategory.Security,
            Suspended,
            Now(),
            subject,
            subject,
            organization: null));

        Assert.Null((await OneAsync(subject)).Organization);
    }

    /// <summary>
    /// PRIV-RET-003 AC1, AC2: the instant on the row is the one the event occurred at
    /// and not the one it was written at, and it is stored in UTC.
    /// </summary>
    [Fact]
    public async Task PRIV_RET_003_AC1_TheRecordedInstantIsWhenTheEventOccurredAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Now());
        DateTimeOffset occurred = Now().AddHours(-6).ToOffset(TimeSpan.FromHours(2));

        await AppendAsync(AuditRecord.Of(
            NewId(),
            AuditCategory.Security,
            Suspended,
            occurred,
            subject,
            subject,
            organization: null));

        AuditRecord read = await OneAsync(subject);

        // The database keeps microseconds; the tick below one is not the criterion.
        Assert.Equal(
            occurred.ToUniversalTime(),
            read.OccurredAt.ToUniversalTime(),
            TimeSpan.FromMilliseconds(1));
        Assert.Equal(TimeSpan.Zero, read.OccurredAt.Offset);
    }

    /// <summary>
    /// PRIV-RET-004 AC1: the row holds the code of what happened and structured fields,
    /// and no rendered sentence, so the trail means the same thing to two readers.
    /// </summary>
    [Fact]
    public async Task PRIV_RET_004_AC1_TheRowHoldsCodesAndStructuredFieldsAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Now());

        await AppendAsync(AuditRecord.Of(
            NewId(),
            AuditCategory.Security,
            Suspended,
            Now(),
            subject,
            subject,
            organization: null,
            details: Fields(("origin", "self"))));

        AuditRecord read = await OneAsync(subject);

        Assert.Equal(Suspended, read.Action);
        Assert.Equal("self", read.Details["origin"].GetString());
    }

    /// <summary>
    /// PRIV-RET-002 AC1: the application has no way to change a record and none to
    /// remove one. The port offers neither, and the trail it writes is append-only in
    /// the code that reaches it.
    /// </summary>
    [Fact]
    public async Task PRIV_RET_002_AC1_TwoEventsOnOneSubjectBothStandAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Now());

        await AppendAsync(AuditRecord.Of(
            NewId(),
            AuditCategory.Security,
            Suspended,
            Now().AddHours(-2),
            subject,
            subject,
            organization: null));

        await AppendAsync(AuditRecord.Of(
            NewId(),
            AuditCategory.Security,
            AuditAction.Parse("identity.account.reactivated"),
            Now().AddHours(-1),
            subject,
            subject,
            organization: null));

        await using StoreContext reading = database.Context();
        IReadOnlyList<AuditRecord> read = await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, read.Count);
        Assert.Equal("identity.account.reactivated", read[0].Action.ToString());
        Assert.Equal("identity.account.suspended", read[1].Action.ToString());
    }

    /// <summary>
    /// PRIV-RET-002 AC2: an attribute an event has to record is not in the row in
    /// plain. It is held under the effective subject's own key.
    /// </summary>
    [Fact]
    public async Task PRIV_RET_002_AC2_NoAttributeIsInTheRowInPlainAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Now());

        await AppendAsync(AuditRecord.Of(
            NewId(),
            AuditCategory.Security,
            AuditAction.Parse("identity.identifier.added"),
            Now(),
            subject,
            subject,
            organization: null,
            personalDetails: Fields(("added", "ahmed@example.com"))));

        await using NpgsqlConnection connection = await database.OpenAsync();
        (string details, byte[] sealed_) = await connection.QuerySingleAsync<(string, byte[])>(
            "SELECT details, enc_details FROM identity.audit_records WHERE effective_subject = @subject",
            new { subject = subject.Value });

        Assert.DoesNotContain("ahmed@example.com", details, StringComparison.Ordinal);
        Assert.Equal(PersonalDataFormat.Marker, sealed_[0]);
        Assert.Equal(-1, sealed_.AsSpan().IndexOf(Encoding.UTF8.GetBytes("ahmed@example.com")));
    }

    /// <summary>
    /// PRIV-RET-002 AC4, IDN-PRIN-003 AC3: erasing the subject's key leaves the
    /// encrypted attribute unreadable, and the row is untouched: it is still there,
    /// still resolvable and still says what happened and when.
    /// </summary>
    [Fact]
    public async Task PRIV_RET_002_AC4_ErasureLeavesTheAttributeUnreadableAndTheRowIntactAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Now());
        DateTimeOffset occurred = Now();

        await AppendAsync(AuditRecord.Of(
            NewId(),
            AuditCategory.Security,
            AuditAction.Parse("identity.identifier.added"),
            occurred,
            subject,
            subject,
            organization: null,
            personalDetails: Fields(("added", "ahmed@example.com"))));

        await _deployment.EraseAsync(subject);

        await using StoreContext reading = database.Context();

        AuditRecord anonymised = Assert.Single(await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken));

        Assert.Empty(anonymised.PersonalDetails);
        Assert.Equal("identity.identifier.added", anonymised.Action.ToString());

        await using NpgsqlConnection connection = await database.OpenAsync();
        (string action, DateTime at) = await connection.QuerySingleAsync<(string, DateTime)>(
            "SELECT action, occurred_at FROM identity.audit_records WHERE effective_subject = @subject",
            new { subject = subject.Value });

        Assert.Equal("identity.identifier.added", action);
        Assert.Equal(occurred.UtcDateTime, at, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// PRIV-BREACH-002 AC1: the trail is read by subject through the index that
    /// carries the subject, so answering who was affected reads the rows of one
    /// account and never the whole table.
    /// </summary>
    [Fact]
    public async Task PRIV_BREACH_002_AC1_TheReadBySubjectTakesTheIndexAndNotAScanAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Now());

        await AppendAsync(AuditRecord.Of(
            NewId(),
            AuditCategory.Security,
            Suspended,
            Now(),
            subject,
            subject,
            organization: null));

        await using NpgsqlConnection connection = await database.OpenAsync();

        // A trail worth reading by subject has other subjects in it; with a handful of
        // rows every plan is a scan and the question the item asks cannot be put.
        _ = await connection.ExecuteAsync(
            "INSERT INTO identity.audit_records "
                + "(id, category, occurred_at, action, acting_subject, effective_subject, details) "
                + "SELECT gen_random_uuid(), 'security', now(), 'identity.account.read', "
                + "gen_random_uuid(), gen_random_uuid(), '{}'::jsonb "
                + "FROM generate_series(1, 20000)");

        _ = await connection.ExecuteAsync("ANALYZE identity.audit_records");

        IEnumerable<string> plan = await connection.QueryAsync<string>(
            "EXPLAIN SELECT id, action, occurred_at FROM identity.audit_records "
                + "WHERE effective_subject = @subject ORDER BY occurred_at DESC",
            new { subject = subject.Value });

        string leaf = (await connection.QuerySingleAsync<string>(
            "SELECT tableoid::regclass::text FROM identity.audit_records "
                + "WHERE effective_subject = @subject",
            new { subject = subject.Value }))["identity.".Length..];

        // The empty months cost nothing to walk; what the item asks is that the
        // partition holding the subject's rows is read through the index.
        Assert.Contains(
            plan,
            line => line.Contains("Index Scan", StringComparison.Ordinal)
                && line.Contains(leaf, StringComparison.Ordinal));

        Assert.DoesNotContain(
            plan,
            line => line.Contains("Seq Scan on " + leaf, StringComparison.Ordinal));
    }

    /// <summary>
    /// PRIV-BREACH-002 AC2: the read still answers after the subject is erased. The
    /// rows are there, they still say what happened and when, and what was held under
    /// the destroyed key is simply not among them.
    /// </summary>
    [Fact]
    public async Task PRIV_BREACH_002_AC2_TheReadAnswersAfterErasureWithTheRecordsAnonymisedAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Now());
        DateTimeOffset occurred = Now();

        await AppendAsync(AuditRecord.Of(
            NewId(),
            AuditCategory.Security,
            Suspended,
            occurred,
            subject,
            subject,
            organization: null,
            personalDetails: Fields(("reason", "ahmed@example.com"))));

        await AppendAsync(AuditRecord.Of(
            NewId(),
            AuditCategory.Routine,
            AuditAction.Parse("identity.account.read"),
            occurred - TimeSpan.FromMinutes(5),
            subject,
            subject,
            organization: null));

        await _deployment.EraseAsync(subject);

        await using StoreContext reading = database.Context();

        IReadOnlyList<AuditRecord> records = await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, records.Count);
        Assert.All(records, record => Assert.Empty(record.PersonalDetails));

        Assert.Equal(
            [Suspended, AuditAction.Parse("identity.account.read")],
            records.Select(record => record.Action));
    }

    /// <summary>
    /// PRIV-BREACH-002 AC2: the trail the privacy area reads by subject carries each
    /// record's codes and never what it holds under the key, so it answers the same
    /// before and after the subject is erased.
    /// </summary>
    [Fact]
    public async Task PRIV_BREACH_002_AC2_TheTrailReadsTheSameBeforeAndAfterErasureAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Now());

        await AppendAsync(AuditRecord.Of(
            NewId(),
            AuditCategory.Security,
            Suspended,
            Now(),
            subject,
            subject,
            organization: null,
            details: Fields(("trigger", "staff-report")),
            personalDetails: Fields(("reason", "ahmed@example.com"))));

        IReadOnlyList<AuditEntry> before = await TrailAsync(subject);

        await _deployment.EraseAsync(subject);

        IReadOnlyList<AuditEntry> after = await TrailAsync(subject);

        AuditEntry entry = Assert.Single(before);

        Assert.Equal(Suspended, entry.Action);
        Assert.Equal("staff-report", entry.Details["trigger"].GetString());
        Assert.False(entry.Details.ContainsKey("reason"));
        Assert.Equal(
            before.Select(read => (read.Id, read.Action, string.Join(',', read.Details.Keys))),
            after.Select(read => (read.Id, read.Action, string.Join(',', read.Details.Keys))));
    }

    /// <summary>
    /// PRIV-RET-002: the row goes to the partition of its retention category, so a
    /// month of one category is dropped without touching the other.
    /// </summary>
    [Fact]
    public async Task PRIV_RET_002_ARecordGoesToThePartitionOfItsCategoryAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Now());

        await AppendAsync(AuditRecord.Of(
            NewId(),
            AuditCategory.Routine,
            AuditAction.Parse("identity.account.read"),
            Now(),
            subject,
            subject,
            organization: null));

        await using NpgsqlConnection connection = await database.OpenAsync();
        string leaf = await connection.QuerySingleAsync<string>(
            "SELECT tableoid::regclass::text FROM identity.audit_records "
                + "WHERE effective_subject = @subject",
            new { subject = subject.Value });

        Assert.StartsWith("identity.audit_records_routine_", leaf, StringComparison.Ordinal);
    }

    /// <summary>
    /// PRIV-RET-002 AC3: the months the sweep keeps open are there, three per category,
    /// and calling the function again creates none, so a run that overlaps another adds
    /// nothing.
    /// </summary>
    [Fact]
    public async Task PRIV_RET_002_AC3_TheSweepKeepsThreeMonthsOpenPerCategoryAsync()
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        int created = await connection.ExecuteScalarAsync<int>(
            "SELECT identity.audit_ensure_partitions()");

        int months = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM pg_class "
                + "WHERE relkind = 'r' AND relname LIKE 'audit_records_%_20%'");

        Assert.Equal(0, created);
        Assert.Equal(6, months);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC11: an aggregate over the columns that are not encrypted
    /// reads no key and no ciphertext, so it answers the same with a subject's fields
    /// readable and after their key is destroyed.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_005a_AC11_AnAggregateOverPlainColumnsIsUnaffectedAsync()
    {
        SubjectId erased = await _deployment.AccountAsync(Now());
        SubjectId kept = await _deployment.AccountAsync(Now());
        Guid[] subjects = [erased.Value, kept.Value];

        foreach (SubjectId subject in new[] { erased, kept })
        {
            await AppendAsync(AuditRecord.Of(
                NewId(),
                AuditCategory.Security,
                Suspended,
                Now(),
                subject,
                subject,
                organization: null,
                personalDetails: Fields(("reason", "ahmed@example.com"))));

            await AppendAsync(AuditRecord.Of(
                NewId(),
                AuditCategory.Routine,
                AuditAction.Parse("identity.account.read"),
                Now(),
                subject,
                subject,
                organization: null));
        }

        await using NpgsqlConnection connection = await database.OpenAsync();
        IReadOnlyList<(string Category, long Records)> before = await CountedAsync(connection, subjects);

        await _deployment.EraseAsync(erased);

        Assert.Equal([("routine", 2L), ("security", 2L)], before);
        Assert.Equal(before, await CountedAsync(connection, subjects));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static async Task<IReadOnlyList<(string Category, long Records)>> CountedAsync(
        NpgsqlConnection connection,
        Guid[] subjects) =>
        [.. await connection.QueryAsync<(string, long)>(
            "SELECT category, count(*) FROM identity.audit_records "
                + "WHERE effective_subject = ANY(@subjects) GROUP BY category ORDER BY category",
            new { subjects })];

    // One row written straight to the table, naming neither identity: what the
    // constraint admits is read from the database and not from the store.
    private async Task<int> WriteAsync(string action)
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        return await connection.ExecuteAsync(
            """
            INSERT INTO identity.audit_records
                (id, category, occurred_at, action, details)
            VALUES (@id, 'security', @at, @action, '{}'::jsonb);
            """,
            new { id = Guid.CreateVersion7(), at = Now(), action });
    }

    private static AuditRecordId NewId() => new(Guid.CreateVersion7());

    private static DateTimeOffset Now() => DateTimeOffset.UtcNow;

    private static Dictionary<string, JsonElement> Fields(params (string Name, string Value)[] fields)
    {
        var document = new Dictionary<string, JsonElement>(fields.Length, StringComparer.Ordinal);

        foreach ((string name, string value) in fields)
        {
            document[name] = JsonSerializer.SerializeToElement(value);
        }

        return document;
    }

    private AuditStore Store(StoreContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness);

    private async ValueTask<IReadOnlyList<AuditEntry>> TrailAsync(SubjectId subject)
    {
        await using StoreContext reading = database.Context();

        return await new AuditTrailStore(Store(reading)).OfSubjectAsync(
            subject,
            TestContext.Current.CancellationToken);
    }

    private async ValueTask AppendAsync(AuditRecord record)
    {
        await using StoreContext writing = database.Context();
        await Store(writing).AppendAsync(record, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async ValueTask<AuditRecord> OneAsync(SubjectId subject)
    {
        await using StoreContext reading = database.Context();

        return Assert.Single(await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken));
    }
}
