using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Records;
using Janus.Core;
using Janus.Identity.Records;
using Janus.Privacy.Outbox;
using Janus.Storage.Authentication.Records;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Identity.Records;
using Janus.Storage.Privacy.Outbox;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The person records a collection reads and writes, staff enter and a person changes,
/// as the database keeps them (REC-COL-002, REC-COL-003, REC-ACC-002).
/// </summary>
/// <remarks>The port implementations are tested against the real database (D-156).</remarks>
[Trait("kind", "integration")]
public sealed class RecordDirectoryTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private const string Number = "+441632960044";

    private static readonly DateTimeOffset Noon = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// REC-COL-002 AC1: a record a collection creates is of origin collection and holds
    /// the contact it verified, verified then, and the notice and terms versions with
    /// the instant.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_COL_002_AC1_ACreatedRecordHoldsWhatTheCollectionVerifiedAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId subject = Subjects.New();

        await WrittenAsync(Person(subject, organization, created: true) with
        {
            Age = new CollectedAge(AdultAffirmed: true, Group: null, DateOfBirth: null, Noon),
            TermsVersion = "7",
        });

        await using StoreContext reading = database.Context();
        var records = new PersonRecordStore(reading, _deployment.Ring, _deployment.Randomness);

        PersonRecord read = Assert.IsType<PersonRecord>(
            await records.FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(
            (PersonRecordOrigin.Collection, PersonRecordState.Active, organization, "en", true),
            (read.Origin, read.State, read.Organization, read.Language, read.Age?.AdultAffirmed));
        Assert.Equal(
            new RecordContact(IdentifierKind.Phone, Number, Noon),
            Assert.Single(await records.ContactsAsync(subject, TestContext.Current.CancellationToken)));
        Assert.Equal(
            new RecordDocuments("1", "7", Noon),
            Assert.Single(await records.DocumentsAsync(subject, TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// REC-COL-002 AC4: a reference given at a collection is matched later by its folded
    /// form, and a dump of its table holds it in no readable form.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_COL_002_AC4_AReferenceIsMatchedAndHeldInNoReadableFormAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId subject = Subjects.New();
        string reference = "order-" + subject.Value.ToString("N")[..12];

        await WrittenAsync(Person(subject, organization, created: true) with { Reference = reference });

        await using (StoreContext reading = database.Context())
        {
            Assert.Equal(
                [subject],
                await new PersonRecordStore(reading, _deployment.Ring, _deployment.Randomness)
                    .HoldingReferenceAsync(reference, TestContext.Current.CancellationToken));
        }

        await using NpgsqlConnection connection = await database.OpenAsync();

        IEnumerable<byte[]> dumped = await connection.QueryAsync<byte[]>(
            "SELECT fingerprint FROM identity.person_record_references WHERE subject = @subject",
            new { subject = subject.Value });

        Assert.All(dumped, fingerprint => Assert.Equal(-1, fingerprint.AsSpan().IndexOf(Encoding.UTF8.GetBytes(reference))));
    }

    /// <summary>
    /// REC-COL-002: only a record of the organization, active and attached to no account,
    /// is offered for reuse.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_COL_002_OnlyAnActiveUnattachedRecordOfTheOrganizationIsCollectableAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        OrganizationId elsewhere = await _deployment.OrganizationAsync(Noon);
        SubjectId reusable = Subjects.New();
        SubjectId attached = Subjects.New();
        SubjectId restricted = Subjects.New();
        SubjectId other = Subjects.New();

        await WrittenAsync(Person(reusable, organization, created: true));
        await WrittenAsync(Person(attached, organization, created: true));
        await WrittenAsync(Person(restricted, organization, created: true));
        await WrittenAsync(Person(other, elsewhere, created: true));
        await ChangedAsync(attached, record => record.Attach(Subjects.New(), Noon));
        await ChangedAsync(restricted, record => record.Restrict());

        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        await work.BeginAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<CollectableRecord> collectable = await Directory(context)
            .CollectableAsync(organization, IdentifierKind.Phone, Number, TestContext.Current.CancellationToken);

        CollectableRecord found = Assert.Single(collectable);

        Assert.Equal(reusable, found.Subject);
        Assert.Equal(new CollectedContact(IdentifierKind.Phone, Number, Noon), Assert.Single(found.Contacts));

        await work.RollbackAsync();
    }

    /// <summary>
    /// REC-COL-002: a record reused takes the language given and keeps the age answer it
    /// holds; a contact it gains is added beside the ones it held.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_COL_002_AReusedRecordGainsWhatTheCollectionGaveItAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId subject = Subjects.New();

        await WrittenAsync(Person(subject, organization, created: true) with
        {
            Age = new CollectedAge(AdultAffirmed: true, Group: null, DateOfBirth: null, Noon),
        });
        await WrittenAsync(Person(subject, organization, created: false) with
        {
            Language = "ar",
            Contacts =
            [
                new CollectedContact(IdentifierKind.Phone, Number, Noon.AddDays(1)),
                new CollectedContact(IdentifierKind.Email, "guest@example.test", VerifiedAt: null),
            ],
            At = Noon.AddDays(1),
        });

        await using StoreContext reading = database.Context();
        var records = new PersonRecordStore(reading, _deployment.Ring, _deployment.Randomness);

        PersonRecord read = Assert.IsType<PersonRecord>(
            await records.FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(("ar", Noon), (read.Language, read.Age?.AnsweredAt));
        Assert.Equal(
            [IdentifierKind.Email, IdentifierKind.Phone],
            [.. (await records.ContactsAsync(subject, TestContext.Current.CancellationToken))
                .Select(contact => contact.Kind)
                .Order()]);
        Assert.Equal(2, (await records.DocumentsAsync(subject, TestContext.Current.CancellationToken)).Count);
    }

    /// <summary>
    /// REC-COL-002 AC5: two collections with one phone, run at once on two connections in
    /// one organization, end with one record.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_COL_002_AC5_TwoCollectionsOfOnePhoneAtOnceEndWithOneRecordAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);

        bool[] created = await Task.WhenAll(CollectedAsync(organization), CollectedAsync(organization));

        await using NpgsqlConnection connection = await database.OpenAsync();

        long held = await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM identity.person_records WHERE organization = @organization",
            new { organization = organization.Value });

        Assert.Equal(1, held);
        Assert.Equal([false, true], [.. created.Order()]);
    }

    /// <summary>
    /// REC-COL-003 AC1: a record staff entered is of origin staff, holds its contacts
    /// unverified and the age answer the person gave, and records no documents.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_COL_003_AC1_AnEnteredRecordHoldsItsContactsUnverifiedAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId subject = Subjects.New();

        await using (StoreContext writing = database.Context())
        {
            await using var work = new UnitOfWork(writing);

            await work.BeginAsync(TestContext.Current.CancellationToken);
            await Directory(writing).EnterAsync(
                new EnteredPerson(
                    subject,
                    organization,
                    "en",
                    Name: null,
                    new CollectedAge(AdultAffirmed: true, Group: null, DateOfBirth: null, Noon),
                    [new CollectedContact(IdentifierKind.Phone, Number, VerifiedAt: null)],
                    Reference: null,
                    Noon),
                TestContext.Current.CancellationToken);
            await work.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        var records = new PersonRecordStore(reading, _deployment.Ring, _deployment.Randomness);

        PersonRecord read = Assert.IsType<PersonRecord>(
            await records.FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal((PersonRecordOrigin.Staff, true), (read.Origin, read.Age?.AdultAffirmed));
        Assert.Equal(
            new RecordContact(IdentifierKind.Phone, Number, VerifiedAt: null),
            Assert.Single(await records.ContactsAsync(subject, TestContext.Current.CancellationToken)));
        Assert.Empty(await records.DocumentsAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REC-ACC-001 AC2, AC3: a value reaches the collected records it was verified on
    /// lately; one verified earlier only by the reference it holds; a record staff
    /// entered or one attached to an account never.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_ACC_001_AValueReachesOnlyTheRecordsItMayAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId recent = Subjects.New();
        SubjectId older = Subjects.New();
        SubjectId attached = Subjects.New();
        SubjectId entered = Subjects.New();
        string reference = "stay-" + older.Value.ToString("N")[..12];

        await WrittenAsync(Person(recent, organization, created: true));
        await WrittenAsync(Person(older, organization, created: true) with
        {
            Contacts = [new CollectedContact(IdentifierKind.Phone, Number, Noon.AddDays(-30))],
            Reference = reference,
        });
        await WrittenAsync(Person(attached, organization, created: true));
        await ChangedAsync(attached, record => record.Attach(Subjects.New(), Noon));

        await using (StoreContext writing = database.Context())
        {
            await using var work = new UnitOfWork(writing);

            await work.BeginAsync(TestContext.Current.CancellationToken);
            await Directory(writing).EnterAsync(
                new EnteredPerson(
                    entered,
                    organization,
                    "en",
                    Name: null,
                    Age: null,
                    [new CollectedContact(IdentifierKind.Phone, Number, VerifiedAt: null)],
                    Reference: null,
                    Noon),
                TestContext.Current.CancellationToken);
            await work.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        RecordDirectory directory = Directory(reading);
        IReadOnlyList<byte[]> fingerprints = directory.FingerprintsOf(Number);
        SubjectId[] ours = [recent, older, attached, entered];

        IReadOnlyList<ReachedRecord> reached = await directory.ReachedAsync(
            IdentifierKind.Phone, fingerprints, Noon.AddDays(-1), TestContext.Current.CancellationToken);
        IReadOnlyList<ReachedRecord> referenced = await directory.ReferencedAsync(
            IdentifierKind.Phone, fingerprints, reference, TestContext.Current.CancellationToken);
        IReadOnlyList<ReachedRecord> inReach = await directory.InReachAsync(
            ours, IdentifierKind.Phone, fingerprints, TestContext.Current.CancellationToken);

        Assert.True(await directory.ReachableHoldingAsync(IdentifierKind.Phone, fingerprints, TestContext.Current.CancellationToken));
        Assert.Equal([recent], reached.Select(record => record.Subject).Where(ours.Contains));
        Assert.Equal(
            (organization, Noon, PersonRecordState.Active),
            (reached.Single(record => record.Subject == recent).Organization,
                reached.Single(record => record.Subject == recent).CreatedAt,
                reached.Single(record => record.Subject == recent).State));
        Assert.Equal([older], referenced.Select(record => record.Subject));
        Assert.Equal(
            new[] { recent, older }.OrderBy(subject => subject.Value),
            inReach.Select(record => record.Subject).OrderBy(subject => subject.Value));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// REC-ACC-002: a record is read as the person reads it, its contacts with when each
    /// was verified, its age answer without the date of birth, and the notice and terms
    /// versions its collection recorded.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_ACC_002_ARecordIsReadAsThePersonReadsItAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId subject = Subjects.New();

        await WrittenAsync(Person(subject, organization, created: true) with
        {
            Age = new CollectedAge(AdultAffirmed: true, Group: null, new DateOnly(1990, 4, 1), Noon),
            TermsVersion = "7",
        });

        await using StoreContext reading = database.Context();

        ReadRecord read = Assert.IsType<ReadRecord>(
            await Directory(reading).ReadAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(
            (organization, PersonRecordOrigin.Collection, PersonRecordState.Active, "en"),
            (read.Organization, read.Origin, read.State, read.Language));
        Assert.Equal(new CollectedContact(IdentifierKind.Phone, Number, Noon), Assert.Single(read.Contacts));
        Assert.Equal(new RecordAge(AdultAffirmed: true, Group: null, Noon), read.Age);
        Assert.Equal(new RecordDocument("1", Noon), Assert.Single(read.Notices));
        Assert.Equal(new RecordDocument("7", Noon), Assert.Single(read.Terms));
    }

    /// <summary>
    /// REC-ACC-002, REC-REC-002: a record held for a change takes a new name and language,
    /// begins its own deletion holding the restriction it is under, and returns
    /// restricted when the deletion is cancelled.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_ACC_002_ARecordHeldForAChangeIsRectifiedAndItsDeletionCancelledAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId subject = Subjects.New();

        await WrittenAsync(Person(subject, organization, created: true));

        Assert.True(DisplayName.TryParse("Mona Saleh", out DisplayName name));

        RecordStanding? active = await HeldAsync(
            subject,
            directory => directory.RectifyAsync(subject, name, "ar", TestContext.Current.CancellationToken));

        await ChangedAsync(subject, record => record.Restrict());

        RecordStanding? restricted = await HeldAsync(
            subject,
            directory => directory.BeginDeletionAsync(subject, DeletionOrigin.Self, Noon, TestContext.Current.CancellationToken));
        RecordStanding? deleting = await HeldAsync(
            subject,
            directory => directory.CancelDeletionAsync(subject, TestContext.Current.CancellationToken));
        RecordStanding? returned = await HeldAsync(subject, _ => ValueTask.CompletedTask);

        await using StoreContext reading = database.Context();

        ReadRecord read = Assert.IsType<ReadRecord>(
            await Directory(reading).ReadAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(("Mona Saleh", "ar"), (read.Name, read.Language));
        Assert.Equal(
            [PersonRecordState.Active, PersonRecordState.Restricted, PersonRecordState.Deleting, PersonRecordState.Restricted],
            new[] { active, restricted, deleting, returned }.Select(standing => standing!.State));
        Assert.Equal((DeletionOrigin.Self, Noon), (deleting!.DeletingBy, deleting.DeletingSince));
    }

    /// <summary>
    /// REC-ADM-002, PRIV-RIGHT-004: a restricted record held for the change is lifted to
    /// active, and the lift is put on the outbox for the subscribers.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_ADM_002_ALiftedRecordIsActiveAndTheSubscribersAreToldAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId subject = Subjects.New();

        await WrittenAsync(Person(subject, organization, created: true));
        await ChangedAsync(subject, record => record.Restrict());

        RecordStanding? restricted = await HeldAsync(
            subject,
            directory => directory.LiftRestrictionAsync(subject, Noon.AddDays(1), TestContext.Current.CancellationToken));
        RecordStanding? lifted = await HeldAsync(subject, _ => ValueTask.CompletedTask);

        await using StoreContext reading = database.Context();

        DeliveryRecord told = await reading.Outbox
            .SingleAsync(delivery => delivery.Subject == subject, TestContext.Current.CancellationToken);

        Assert.Equal(
            (PersonRecordState.Restricted, PersonRecordState.Active),
            (restricted!.State, lifted!.State));
        Assert.Equal(
            (SubjectEventKind.RestrictionChanged, false, Noon.AddDays(1)),
            (told.Kind, told.Restricted, told.RaisedAt));
    }

    /// <summary>
    /// REC-ACC-002: a record's notice contacts are those verified at a collection, or,
    /// where none was, every contact it holds, with the record's language.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_ACC_002_TheNoticeContactsAreTheVerifiedOnesOrEveryOneAsync()
    {
        const string address = "guest@example.test";

        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId verified = Subjects.New();
        SubjectId unverified = Subjects.New();

        await WrittenAsync(Person(verified, organization, created: true) with
        {
            Contacts =
            [
                new CollectedContact(IdentifierKind.Phone, Number, Noon),
                new CollectedContact(IdentifierKind.Email, address, VerifiedAt: null),
            ],
        });
        await WrittenAsync(Person(unverified, organization, created: true) with
        {
            Language = "ar",
            Contacts = [new CollectedContact(IdentifierKind.Email, address, VerifiedAt: null)],
        });

        await using StoreContext reading = database.Context();
        RecordDirectory directory = Directory(reading);

        RecordNoticeContacts first = Assert.IsType<RecordNoticeContacts>(
            await directory.NoticeContactsAsync(verified, TestContext.Current.CancellationToken));
        RecordNoticeContacts second = Assert.IsType<RecordNoticeContacts>(
            await directory.NoticeContactsAsync(unverified, TestContext.Current.CancellationToken));

        Assert.Equal(("en", Number), (first.Language, Assert.Single(first.Contacts).Value));
        Assert.Equal(("ar", address), (second.Language, Assert.Single(second.Contacts).Value));
        Assert.Null(await directory.NoticeContactsAsync(Subjects.New(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REC-ADM-001 AC2: a finding answers the records the organization holds that hold
    /// the contact, verified or not, or the reference, in the order of their subjects,
    /// and none another organization holds.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_ADM_001_AC2_AFindingAnswersTheRecordsOfTheOrganizationHoldingTheValueAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        OrganizationId elsewhere = await _deployment.OrganizationAsync(Noon);
        SubjectId collected = Subjects.New();
        SubjectId entered = Subjects.New();
        string reference = "order-" + collected.Value.ToString("N")[..12];

        await WrittenAsync(Person(collected, organization, created: true) with { Reference = reference });
        await WrittenAsync(Person(Subjects.New(), elsewhere, created: true) with { Reference = reference });

        await using (StoreContext writing = database.Context())
        {
            await using var work = new UnitOfWork(writing);

            await work.BeginAsync(TestContext.Current.CancellationToken);
            await Directory(writing).EnterAsync(
                new EnteredPerson(
                    entered,
                    organization,
                    "en",
                    Name: null,
                    Age: null,
                    [new CollectedContact(IdentifierKind.Phone, Number, VerifiedAt: null)],
                    Reference: null,
                    Noon),
                TestContext.Current.CancellationToken);
            await work.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        RecordDirectory directory = Directory(reading);

        Assert.Equal(
            [.. new[] { collected, entered }.OrderBy(subject => subject.Value)],
            await directory.HoldingAsync(
                organization,
                IdentifierKind.Phone,
                directory.FingerprintsOf(Number),
                TestContext.Current.CancellationToken));
        Assert.Equal(
            [collected],
            await directory.ReferencingAsync(organization, reference, TestContext.Current.CancellationToken));
        Assert.Empty(await directory.HoldingAsync(
            organization,
            IdentifierKind.Email,
            directory.FingerprintsOf(Number),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REC-ATT-001: an active record attached to no account is attached under its lock,
    /// once, and the account lists it with the instant; a second attachment finds it
    /// attached and attaches nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REC_ATT_001_AnActiveUnattachedRecordIsAttachedOnceAsync()
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId account = await _deployment.AccountAsync(Noon);
        SubjectId elsewhere = await _deployment.AccountAsync(Noon);
        SubjectId subject = Subjects.New();
        OrganizationId? attached = null;
        OrganizationId? again = null;

        await WrittenAsync(Person(subject, organization, created: true));

        _ = await HeldAsync(subject, async directory => attached = await directory.AttachAsync(
            subject,
            account,
            Noon.AddDays(1),
            TestContext.Current.CancellationToken));
        _ = await HeldAsync(subject, async directory => again = await directory.AttachAsync(
            subject,
            elsewhere,
            Noon.AddDays(2),
            TestContext.Current.CancellationToken));

        await using StoreContext reading = database.Context();
        RecordDirectory directory = Directory(reading);

        Assert.Equal(organization, attached);
        Assert.Null(again);
        Assert.Equal(
            [new Attachment(subject, organization, Noon, Noon.AddDays(1), PersonRecordState.Active)],
            await directory.AttachedToAsync(account, TestContext.Current.CancellationToken));
        Assert.Empty(await directory.AttachedToAsync(elsewhere, TestContext.Current.CancellationToken));
    }

    private static CollectedPerson Person(
        SubjectId subject,
        OrganizationId organization,
        bool created) =>
        new(
            subject,
            organization,
            created,
            "en",
            Name: null,
            Age: null,
            [new CollectedContact(IdentifierKind.Phone, Number, Noon)],
            Reference: null,
            "1",
            TermsVersion: null,
            Noon);

    private RecordDirectory Directory(StoreContext context)
    {
        var records = new PersonRecordStore(context, _deployment.Ring, _deployment.Randomness);

        return new RecordDirectory(
            context,
            records,
            new IdentifierStore(context, _deployment.Ring, _deployment.Randomness),
            new SubjectKeyStore(context, _deployment.Ring, _deployment.Randomness),
            new OutboxStore(context, TimeProvider.System));
    }

    private async Task WrittenAsync(CollectedPerson person)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        await work.BeginAsync(TestContext.Current.CancellationToken);
        await Directory(context).WriteAsync(person, TestContext.Current.CancellationToken);
        await work.CommitAsync(TestContext.Current.CancellationToken);
    }

    private async Task ChangedAsync(SubjectId subject, Action<PersonRecord> change)
    {
        await using StoreContext context = database.Context();
        var records = new PersonRecordStore(context, _deployment.Ring, _deployment.Randomness);

        PersonRecord record = (await records.FindBySubjectAsync(subject, TestContext.Current.CancellationToken))!;
        change(record);

        await records.RecordAsync(record, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // A change as the rights make it: the record held under its lock, where it stands
    // read there, and the change written in the same transaction.
    private async Task<RecordStanding?> HeldAsync(SubjectId subject, Func<RecordDirectory, ValueTask> change)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);
        RecordDirectory directory = Directory(context);

        await work.BeginAsync(TestContext.Current.CancellationToken);

        RecordStanding? standing = await directory.HoldAsync(subject, TestContext.Current.CancellationToken);

        await change(directory);
        await work.CommitAsync(TestContext.Current.CancellationToken);

        return standing;
    }

    // The collection's decision as the service makes it: the value locked, the records
    // that may be reused read under it, and a record written where none may.
    private async Task<bool> CollectedAsync(OrganizationId organization)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);
        RecordDirectory directory = Directory(context);

        await work.BeginAsync(TestContext.Current.CancellationToken);
        await directory.LockValuesAsync([(IdentifierKind.Phone, Number)], TestContext.Current.CancellationToken);

        IReadOnlyList<CollectableRecord> collectable = await directory
            .CollectableAsync(organization, IdentifierKind.Phone, Number, TestContext.Current.CancellationToken);

        bool created = collectable.Count is 0;

        await directory.WriteAsync(
            Person(created ? Subjects.New() : collectable[0].Subject, organization, created),
            TestContext.Current.CancellationToken);
        await work.CommitAsync(TestContext.Current.CancellationToken);

        return created;
    }
}
