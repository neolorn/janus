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

        await using JanusDbContext reading = database.Context();
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
            "SELECT details, enc_details FROM janus.audit_records WHERE effective_subject = @subject",
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

        await using JanusDbContext reading = database.Context();

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await Store(reading).FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

        await using NpgsqlConnection connection = await database.OpenAsync();
        (string action, DateTime at) = await connection.QuerySingleAsync<(string, DateTime)>(
            "SELECT action, occurred_at FROM janus.audit_records WHERE effective_subject = @subject",
            new { subject = subject.Value });

        Assert.Equal("identity.identifier.added", action);
        Assert.Equal(occurred.UtcDateTime, at, TimeSpan.FromSeconds(1));
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
            "SELECT tableoid::regclass::text FROM janus.audit_records "
                + "WHERE effective_subject = @subject",
            new { subject = subject.Value });

        Assert.StartsWith("janus.audit_records_routine_", leaf, StringComparison.Ordinal);
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
            "SELECT janus.audit_ensure_partitions()");

        int months = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM pg_class "
                + "WHERE relkind = 'r' AND relname LIKE 'audit_records_%_20%'");

        Assert.Equal(0, created);
        Assert.Equal(6, months);
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

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

    private AuditStore Store(JanusDbContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness);

    private async ValueTask AppendAsync(AuditRecord record)
    {
        await using JanusDbContext writing = database.Context();
        await Store(writing).AppendAsync(record, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async ValueTask<AuditRecord> OneAsync(SubjectId subject)
    {
        await using JanusDbContext reading = database.Context();

        return Assert.Single(await Store(reading).FindBySubjectAsync(
            subject,
            TestContext.Current.CancellationToken));
    }
}
