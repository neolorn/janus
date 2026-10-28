using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Events;
using Janus.Authentication.Records;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Identity.Preferences;
using Janus.Identity.Records;
using Janus.Privacy.Erasures;
using Janus.Privacy.Outbox;
using Janus.Storage.Authentication.Accounts;
using Janus.Storage.Authentication.Events;
using Janus.Storage.Authentication.Records;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Identity.Preferences;
using Janus.Storage.Identity.Profiles;
using Janus.Storage.Identity.Records;
using Janus.Storage.Privacy.Outbox;
using Janus.Storage.Privacy.Requests;
using Janus.Storage.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// The records attached to an account, following each transition of the account in its
/// own transaction (REC-ATT-002).
/// </summary>
[Trait("kind", "integration")]
public sealed class AttachedRecordsTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly DeletionWindows Windows = new(TimeSpan.FromDays(30), TimeSpan.FromDays(30), TimeSpan.FromDays(30));

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// REC-ATT-002 AC1: restricting an account restricts each record attached to it in the
    /// same transaction, and <c>RestrictionChanged</c> names each; a transaction that does
    /// not commit restricts none, and a record attached to another account is left alone.
    /// </summary>
    [Fact]
    public async Task REC_ATT_002_AC1_RestrictingAnAccountRestrictsEachRecordAttachedToItAsync()
    {
        SubjectId account = await _deployment.AccountAsync(Noon);
        SubjectId other = await _deployment.AccountAsync(Noon);
        SubjectId first = await AttachedAsync(account);
        SubjectId second = await AttachedAsync(account);
        SubjectId elsewhere = await AttachedAsync(other);

        await using (StoreContext abandoning = database.Context())
        {
            await using var work = new UnitOfWork(abandoning);

            await work.BeginAsync(TestContext.Current.CancellationToken);

            Assert.True(await States(abandoning, work).RestrictAsync(account, Noon, TestContext.Current.CancellationToken));
        }

        Assert.Equal(
            [PersonRecordState.Active, PersonRecordState.Active],
            [await StateAsync(first), await StateAsync(second)]);

        await using (StoreContext restricting = database.Context())
        {
            await using var work = new UnitOfWork(restricting);

            await work.BeginAsync(TestContext.Current.CancellationToken);

            Assert.True(await States(restricting, work).RestrictAsync(account, Noon, TestContext.Current.CancellationToken));

            await work.CommitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(
            [PersonRecordState.Restricted, PersonRecordState.Restricted, PersonRecordState.Active],
            [await StateAsync(first), await StateAsync(second), await StateAsync(elsewhere)]);
        Assert.Equal(
            new[] { first, second }.OrderBy(subject => subject.Value).Select(subject => (subject, SubjectEventKind.RestrictionChanged, true)),
            await ToldAsync(first, second, elsewhere));
    }

    /// <summary>
    /// REC-ATT-002: lifting the account's restriction lifts each record's, and
    /// <c>RestrictionChanged</c> names each.
    /// </summary>
    [Fact]
    public async Task REC_ATT_002_LiftingAnAccountsRestrictionLiftsEachRecordsAsync()
    {
        SubjectId account = await _deployment.AccountAsync(Noon);
        SubjectId attached = await AttachedAsync(account);

        await InWorkAsync((context, work) => States(context, work).RestrictAsync(account, Noon, TestContext.Current.CancellationToken).AsTask());
        await InWorkAsync(async (context, work) =>
        {
            await Directory(context, work).LiftRestrictionAsync(account, Noon.AddDays(1), TestContext.Current.CancellationToken);

            return true;
        });

        Assert.Equal(PersonRecordState.Active, await StateAsync(attached));
        Assert.Equal(
            [(attached, SubjectEventKind.RestrictionChanged, true), (attached, SubjectEventKind.RestrictionChanged, false)],
            await ToldAsync(attached));
    }

    /// <summary>
    /// REC-ATT-002: an account's deletion window takes each record attached to it into
    /// <c>deleting</c> with the account's origin, its <c>RecordDeletionRequested</c>
    /// carrying the account's window; the record's own sweep never reads it, and the
    /// account's cancellation returns each, announced by <c>RecordDeletionCancelled</c>.
    /// </summary>
    [Fact]
    public async Task REC_ATT_002_AnAccountsDeletionWindowAndItsCancellationReachEachRecordAsync()
    {
        SubjectId account = await _deployment.AccountAsync(Noon);
        SubjectId attached = await AttachedAsync(account);

        await InWorkAsync(async (context, work) =>
        {
            await Directory(context, work).BeginDeletionAsync(account, Noon, TestContext.Current.CancellationToken);

            return true;
        });

        await using (StoreContext reading = database.Context())
        {
            PersonRecord deleting = (await Records(reading).FindBySubjectAsync(attached, TestContext.Current.CancellationToken))!;

            Assert.Equal(
                (PersonRecordState.Deleting, DeletionOrigin.Self, Noon),
                (deleting.State, deleting.DeletingBy, deleting.DeletingSince));
            Assert.DoesNotContain(
                await Records(reading).DeletingSinceAsync(Noon.AddYears(1), TestContext.Current.CancellationToken),
                record => record.Subject == attached);
        }

        Assert.Equal(
            Noon + Janus.Core.Configuration.Settings.AccountDeletionGrace.Default,
            DateTimeOffset.Parse(
                Assert.Single(await EventsAsync("RecordDeletionRequested", attached, "ErasesAt")),
                CultureInfo.InvariantCulture));

        await InWorkAsync(async (context, work) =>
        {
            await Directory(context, work).CancelDeletionAsync(account, Noon.AddDays(1), TestContext.Current.CancellationToken);

            return true;
        });

        Assert.Equal(PersonRecordState.Active, await StateAsync(attached));
        Assert.Single(await EventsAsync("RecordDeletionCancelled", attached, "IdempotencyKey"));
    }

    /// <summary>
    /// REC-ATT-002 AC4: a takedown of an account takes each record attached to it into
    /// <c>deleting</c> by <c>takedown</c>, each with its <c>TakedownExecuted</c>, and its
    /// reversal returns each to what it held.
    /// </summary>
    [Fact]
    public async Task REC_ATT_002_AC4_ATakedownTakesEachRecordDownAndItsReversalReturnsEachAsync()
    {
        SubjectId account = await _deployment.AccountAsync(Noon);
        SubjectId active = await AttachedAsync(account);
        SubjectId restricted = await AttachedAsync(account);

        await using (StoreContext changing = database.Context())
        {
            PersonRecordStore records = Records(changing);
            PersonRecord held = (await records.FindBySubjectAsync(restricted, TestContext.Current.CancellationToken))!;

            held.Restrict();

            await records.RecordAsync(held, TestContext.Current.CancellationToken);
            await changing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await InWorkAsync((context, work) => States(context, work).TakeDownAsync(account, Noon, TestContext.Current.CancellationToken).AsTask());

        await using (StoreContext reading = database.Context())
        {
            foreach (SubjectId subject in new[] { active, restricted })
            {
                PersonRecord taken = (await Records(reading).FindBySubjectAsync(subject, TestContext.Current.CancellationToken))!;

                Assert.Equal(
                    (PersonRecordState.Deleting, DeletionOrigin.Takedown, Noon),
                    (taken.State, taken.DeletingBy, taken.DeletingSince));
            }
        }

        Assert.Equal(
            new[] { active, restricted }.OrderBy(subject => subject.Value).Select(subject => (subject, SubjectEventKind.TakedownExecuted, false)),
            await ToldAsync(active, restricted));

        await InWorkAsync((context, work) => States(context, work)
            .ReverseTakedownAsync(account, Noon.AddHours(1), Windows, TestContext.Current.CancellationToken)
            .AsTask());

        Assert.Equal(
            [PersonRecordState.Active, PersonRecordState.Restricted],
            [await StateAsync(active), await StateAsync(restricted)]);
        Assert.Single(await EventsAsync("TakedownReversed", active, "IdempotencyKey"));
    }

    /// <summary>
    /// REC-ATT-002 AC2: the records an account's erasure erases with it are those attached
    /// to it and not yet erased.
    /// </summary>
    [Fact]
    public async Task REC_ATT_002_AC2_TheAccountsErasureReadsEachRecordStillAttachedAsync()
    {
        SubjectId account = await _deployment.AccountAsync(Noon);
        SubjectId attached = await AttachedAsync(account);
        SubjectId erased = await AttachedAsync(account);

        await using (StoreContext changing = database.Context())
        {
            PersonRecordStore records = Records(changing);
            PersonRecord held = (await records.FindBySubjectAsync(erased, TestContext.Current.CancellationToken))!;

            held.MarkErased();

            await records.RecordAsync(held, TestContext.Current.CancellationToken);
            await changing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();
        await using var work = new UnitOfWork(reading);

        Assert.Equal([attached], await States(reading, work).AttachedAsync(account, TestContext.Current.CancellationToken));
    }

    private async Task InWorkAsync(Func<StoreContext, UnitOfWork, Task<bool>> transition)
    {
        await using StoreContext changing = database.Context();
        await using var work = new UnitOfWork(changing);

        await work.BeginAsync(TestContext.Current.CancellationToken);

        Assert.True(await transition(changing, work));

        await work.CommitAsync(TestContext.Current.CancellationToken);
    }

    private PersonRecordStore Records(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness);

    private AttachedRecords Attached(StoreContext context, UnitOfWork work) =>
        _deployment.Attached(context, new EventOutbox(new PendingEvents(context), work), new FixedTime(Noon));

    private AccountStates States(StoreContext context, UnitOfWork work) =>
        new(
            new AccountStore(context),
            Records(context),
            new SessionStore(context, _deployment.Ring, _deployment.Randomness),
            new EventOutbox(new PendingEvents(context), work),
            Attached(context, work));

    private AccountDirectory Directory(StoreContext context, UnitOfWork work) => new(
        context,
        new AccountStore(context),
        new ProfileStore(context, _deployment.Ring, _deployment.Randomness),
        new ProfilePhotoStore(context, _deployment.Ring, _deployment.Randomness),
        new SubjectKeyStore(context, _deployment.Ring, _deployment.Randomness),
        new PreferenceStore(context, _deployment.Ring, _deployment.Randomness),
        PreferenceDeclarations.None,
        new OutboxStore(context, new FixedTime(Noon)),
        Attached(context, work));

    private async Task<PersonRecordState> StateAsync(SubjectId record)
    {
        await using StoreContext reading = database.Context();

        return (await Records(reading).FindBySubjectAsync(record, TestContext.Current.CancellationToken))!.State;
    }

    // What the outbox told the subscribers of these subjects, earliest first, then by subject.
    private async Task<IReadOnlyList<(SubjectId Subject, SubjectEventKind Kind, bool Restricted)>> ToldAsync(
        params SubjectId[] subjects)
    {
        await using StoreContext reading = database.Context();

        return
        [
            .. (await reading.Outbox
                    .Where(delivery => subjects.Contains(delivery.Subject))
                    .ToListAsync(TestContext.Current.CancellationToken))
                .OrderBy(delivery => delivery.RaisedAt)
                .ThenBy(delivery => delivery.Subject.Value)
                .Select(delivery => (delivery.Subject, delivery.Kind, delivery.Restricted)),
        ];
    }

    // One field of each event of a kind raised for a subject.
    private async Task<IReadOnlyList<string>> EventsAsync(string kind, SubjectId subject, string field)
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        return
        [
            .. await connection.QueryAsync<string>(
                """
                SELECT payload->>@field FROM identity.events
                WHERE kind = @kind AND payload->'Subject'->>'Value' = @subject
                """,
                new { field, kind, subject = subject.Value.ToString() }),
        ];
    }

    // A person record a collection wrote, attached to an account.
    private async Task<SubjectId> AttachedAsync(SubjectId account)
    {
        OrganizationId organization = await _deployment.OrganizationAsync(Noon);
        SubjectId subject = Subjects.New();

        await using (StoreContext writing = database.Context())
        {
            await using var work = new UnitOfWork(writing);

            await work.BeginAsync(TestContext.Current.CancellationToken);
            await new RecordDirectory(
                    writing,
                    Records(writing),
                    new IdentifierStore(writing, _deployment.Ring, _deployment.Randomness),
                    new SubjectKeyStore(writing, _deployment.Ring, _deployment.Randomness))
                .WriteAsync(
                    new CollectedPerson(
                        subject,
                        organization,
                        Created: true,
                        "en",
                        Name: null,
                        Age: null,
                        [new CollectedContact(IdentifierKind.Phone, "+441632960046", Noon)],
                        Reference: null,
                        "1",
                        TermsVersion: null,
                        Noon),
                    TestContext.Current.CancellationToken);
            await work.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext attaching = database.Context();
        PersonRecordStore records = Records(attaching);
        PersonRecord record = (await records.FindBySubjectAsync(subject, TestContext.Current.CancellationToken))!;

        record.Attach(account, Noon);

        await records.RecordAsync(record, TestContext.Current.CancellationToken);
        await attaching.SaveChangesAsync(TestContext.Current.CancellationToken);

        return subject;
    }
}
