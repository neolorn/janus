using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Records;
using Janus.Core;
using Janus.Identity.Records;
using Janus.Storage.Authentication.Records;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Identity.Records;
using Janus.Storage.Privacy.SubjectKeys;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// The person records a collection reads and writes and staff enter, as the database
/// keeps them (REC-COL-002, REC-COL-003).
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

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

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
            new SubjectKeyStore(context, _deployment.Ring, _deployment.Randomness));
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
