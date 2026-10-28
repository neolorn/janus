using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Identity.Records;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Identity.Records;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// A person record as its four tables carry it (REC-REC-001, REC-REC-002).
/// </summary>
/// <remarks>
/// The port implementation is tested against the aggregate it translates, with the real
/// database (D-156).
/// </remarks>
[Trait("kind", "integration")]
public sealed class PersonRecordStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string Email = "record.holder@example.com";
    private const string Phone = "+201007776655";
    private const string Reference = "order-7781-qx";

    private static readonly DateTimeOffset Noon = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Born = new(1990, 5, 17);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// REC-REC-001 AC1: a record's subject is drawn from the one space subject
    /// identifiers share, so no record takes an account's, and an erased record's is
    /// never issued again.
    /// </summary>
    [Fact]
    public async Task REC_REC_001_AC1_ARecordsSubjectIsNeverAnAccountsAndNeverReissuedAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        SubjectId record = await RecordAsync(organization, _ => { });

        await _deployment.EraseAsync(record);

        DbUpdateException taken = await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await RecordAsync(organization, _ => { }, account));
        DbUpdateException reissued = await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await RecordAsync(organization, _ => { }, record));

        Assert.Equal("pk_subject_keys", Assert.IsType<PostgresException>(taken.InnerException).ConstraintName);
        Assert.Equal("pk_subject_keys", Assert.IsType<PostgresException>(reissued.InnerException).ConstraintName);
    }

    /// <summary>
    /// REC-REC-001: a record reads back as it was written: its organization, origin,
    /// language, name, age answer with the date retained, and its attachment.
    /// </summary>
    [Fact]
    public async Task REC_REC_001_ARecordReadsBackAsItWasWrittenAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);

        SubjectId subject = await RecordAsync(organization, record =>
        {
            record.Rename(Named("Layla Hassan"));
            record.AnswerAge(new RecordAgeAnswer(true, null, Noon, Born));
        });

        await ChangeAsync(subject, record => record.Attach(account, Noon.AddHours(1)));

        PersonRecord read = await ReadAsync(subject);

        Assert.Equal(organization, read.Organization);
        Assert.Equal(PersonRecordOrigin.Collection, read.Origin);
        Assert.Equal(PersonRecordState.Active, read.State);
        Assert.Equal("ar", read.Language);
        Assert.Equal("Layla Hassan", read.Name?.Value);
        Assert.Equal(new RecordAgeAnswer(true, null, Noon, Born), read.Age);
        Assert.Equal(account, read.AttachedTo);
        Assert.Equal(Noon.AddHours(1), read.AttachedAt);
    }

    /// <summary>
    /// REC-REC-002: a transition the record makes is carried onto its row and read back,
    /// with the state it holds through its window.
    /// </summary>
    [Fact]
    public async Task REC_REC_002_ATransitionIsCarriedOntoTheRowAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId subject = await RecordAsync(organization, record => record.Restrict());

        await ChangeAsync(subject, record => record.Takedown(Noon));

        PersonRecord read = await ReadAsync(subject);

        Assert.Equal(PersonRecordState.Deleting, read.State);
        Assert.Equal(DeletionOrigin.Takedown, read.DeletingBy);
        Assert.Equal(Noon, read.DeletingSince);
        Assert.True(read.RestrictionHeld);

        await using StoreContext context = database.Context();

        IReadOnlyList<PersonRecord> deleting = await Store(context)
            .DeletingSinceAsync(Noon, TestContext.Current.CancellationToken);

        Assert.Contains(deleting, held => held.Subject == subject);
    }

    /// <summary>
    /// REC-REC-002 AC1: every record has a state, and the column takes none outside the
    /// states the chapter names.
    /// </summary>
    [Fact]
    public async Task REC_REC_002_AC1_EveryRecordHasAStateAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId subject = await RecordAsync(organization, _ => { });

        await using NpgsqlConnection connection = await database.OpenAsync();

        PostgresException missing = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync(
                "UPDATE identity.person_records SET state = NULL WHERE subject = @subject",
                new { subject = subject.Value }));
        PostgresException suspended = await Assert.ThrowsAsync<PostgresException>(async () =>
            await connection.ExecuteAsync(
                "UPDATE identity.person_records SET state = 'suspended' WHERE subject = @subject",
                new { subject = subject.Value }));

        Assert.Equal(PostgresErrorCodes.NotNullViolation, missing.SqlState);
        Assert.Equal("ck_person_records_state", suspended.ConstraintName);
    }

    /// <summary>
    /// REC-REC-001 AC4: a record holds at most one email and one phone, a second of a
    /// kind replacing the first, and its contacts are found by their fingerprint.
    /// </summary>
    [Fact]
    public async Task REC_REC_001_AC4_ARecordHoldsAtMostOneEmailAndOnePhoneAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId subject = await RecordAsync(organization, _ => { });

        await ContactAsync(subject, new RecordContact(IdentifierKind.Email, "first@example.com", null));
        await ContactAsync(subject, new RecordContact(IdentifierKind.Email, Email, Noon));
        await ContactAsync(subject, new RecordContact(IdentifierKind.Phone, Phone, null));

        await using StoreContext context = database.Context();
        PersonRecordStore store = Store(context);

        IReadOnlyList<RecordContact> contacts = await store.ContactsAsync(subject, TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new RecordContact(IdentifierKind.Email, Email, Noon),
                new RecordContact(IdentifierKind.Phone, Phone, null),
            ],
            contacts);
        Assert.Equal(
            [subject],
            await store.HoldingContactAsync(IdentifierKind.Email, Email, TestContext.Current.CancellationToken));
        Assert.Empty(await store.HoldingContactAsync(IdentifierKind.Email, "first@example.com", TestContext.Current.CancellationToken));
        Assert.Empty(await store.HoldingContactAsync(IdentifierKind.Phone, Email, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REC-REC-001 AC4, REC-COL-002 AC4: a dump holds no form of a contact's value or of
    /// a reference; the reference is held as a fingerprint alone and still matched.
    /// </summary>
    [Fact]
    public async Task REC_REC_001_AC4_ADumpHoldsNoFormOfAContactOrAReferenceAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId subject = await RecordAsync(organization, record => record.Rename(Named("Omar Nabil")));

        await ContactAsync(subject, new RecordContact(IdentifierKind.Email, Email, Noon));
        await ContactAsync(subject, new RecordContact(IdentifierKind.Phone, Phone, Noon));

        await using (StoreContext context = database.Context())
        {
            await Store(context).AddReferenceAsync(subject, Reference, Noon, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext context = database.Context())
        {
            Assert.Equal(
                [subject],
                await Store(context).HoldingReferenceAsync(Reference, TestContext.Current.CancellationToken));
        }

        Assert.Empty(await HoldingAsync(Email, Phone, Reference, "Omar Nabil"));
    }

    /// <summary>
    /// REC-REC-003: a deleted record's key is gone, so its name, date and contacts are
    /// answered as absent and its state, organization and instants remain.
    /// </summary>
    [Fact]
    public async Task REC_REC_003_ADeletedRecordAnswersNoPersonalFieldAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId subject = await RecordAsync(organization, record =>
        {
            record.Rename(Named("Sara Adel"));
            record.AnswerAge(new RecordAgeAnswer(null, AgeGroup.Adult, Noon, Born));
        });

        await ContactAsync(subject, new RecordContact(IdentifierKind.Phone, Phone, Noon));
        await ChangeAsync(subject, record => record.MarkErased());
        await _deployment.EraseAsync(subject);

        PersonRecord read = await ReadAsync(subject);

        await using StoreContext context = database.Context();

        Assert.Equal(PersonRecordState.Deleted, read.State);
        Assert.Equal(organization, read.Organization);
        Assert.Null(read.Name);
        Assert.Null(read.Age!.DateOfBirth);
        Assert.Empty(await Store(context).ContactsAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REC-COL-002: every presentation and acceptance a collection recorded stays, with
    /// its instant.
    /// </summary>
    [Fact]
    public async Task REC_COL_002_EveryPresentationAndAcceptanceIsKeptAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId subject = await RecordAsync(organization, _ => { });

        await using (StoreContext context = database.Context())
        {
            PersonRecordStore store = Store(context);

            await store.AddDocumentsAsync(subject, new RecordDocuments("2026-01", "7", Noon), TestContext.Current.CancellationToken);
            await store.AddDocumentsAsync(subject, new RecordDocuments("2026-02", null, Noon.AddDays(1)), TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Equal(
            [new RecordDocuments("2026-01", "7", Noon), new RecordDocuments("2026-02", null, Noon.AddDays(1))],
            await Store(reading).DocumentsAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A record is held only inside the operation's transaction, as an account is.
    /// </summary>
    [Fact]
    public async Task HoldAsync_OutsideATransaction_ThrowsAsync()
    {
        await using StoreContext context = database.Context();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store(context).HoldAsync(Subjects.New(), TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static DisplayName Named(string entered)
    {
        Assert.True(DisplayName.TryParse(entered, out DisplayName name));

        return name;
    }

    private PersonRecordStore Store(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness);

    private async Task<SubjectId> RecordAsync(
        OrganizationId organization,
        Action<PersonRecord> filled,
        SubjectId? issued = null)
    {
        SubjectId subject = issued ?? Subjects.New();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(_deployment.Randomness);

        try
        {
            await using StoreContext context = database.Context();

            context.SubjectKeys.Add(new SubjectKeyRecord
            {
                Id = SubjectKeyId.Of(subject),
                FormatMarker = PersonalDataFormat.Marker,
                KeyVersion = _deployment.Keys.CurrentVersion,
                WrappedKey = PersonalFieldCipher.Wrap(dataKey, _deployment.Keys.Current.Span),
            });

            // The key row is the record's parent by a constraint the model does not
            // know, so it is written before the record names it.
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var record = PersonRecord.Create(subject, organization, PersonRecordOrigin.Collection, Noon, "ar");
            filled(record);

            await Store(context).AddAsync(record, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }

        return subject;
    }

    private async Task ChangeAsync(SubjectId subject, Action<PersonRecord> change)
    {
        await using StoreContext context = database.Context();
        PersonRecordStore store = Store(context);

        PersonRecord record = (await store.FindBySubjectAsync(subject, TestContext.Current.CancellationToken))!;
        change(record);

        await store.RecordAsync(record, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task ContactAsync(SubjectId subject, RecordContact contact)
    {
        await using StoreContext context = database.Context();

        await Store(context).SetContactAsync(subject, contact, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<PersonRecord> ReadAsync(SubjectId subject)
    {
        await using StoreContext context = database.Context();

        return (await Store(context).FindBySubjectAsync(subject, TestContext.Current.CancellationToken))!;
    }

    // Every column of the schema that could hold text, read as text, searched for each
    // value in the form it was given and in its UTF-8 bytes.
    private async Task<List<string>> HoldingAsync(params string[] values)
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        List<(string Table, string Column, string Type)> columns =
            [.. await connection.QueryAsync<(string, string, string)>(
                "SELECT c.table_name, c.column_name, c.data_type "
                    + "FROM information_schema.columns c "
                    + "JOIN information_schema.tables t "
                    + "ON t.table_schema = c.table_schema AND t.table_name = c.table_name "
                    + "WHERE c.table_schema = 'identity' AND t.table_type = 'BASE TABLE' "
                    + "AND c.data_type IN ('text', 'character varying', 'bytea', 'jsonb')")];

        string[] sought = [.. values.Select(value => "%" + value + "%")];
        var holding = new List<string>();

        foreach ((string table, string column, string type) in columns)
        {
            string read = type is "bytea"
                ? "encode(\"" + column + "\", 'escape')"
                : "\"" + column + "\"::text";

            long found = await connection.ExecuteScalarAsync<long>(
                "SELECT count(*) FROM identity.\"" + table + "\" WHERE " + read + " LIKE ANY(@sought)",
                new { sought });

            if (found > 0)
            {
                holding.Add(table + "." + column);
            }
        }

        Assert.Contains(columns, column => column.Table == "person_record_contacts");

        return holding;
    }
}
