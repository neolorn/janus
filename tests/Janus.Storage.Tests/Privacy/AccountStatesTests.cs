using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Events;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Privacy.Erasures;
using Janus.Privacy.Requests;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Events;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Privacy.Erasures;
using Janus.Storage.Privacy.Requests;
using Janus.Storage.Settings;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// The takedown's transitions over the accounts and sessions they reach
/// (IDN-LIFE-003, AUTH-SESS-010).
/// </summary>
[Trait("kind", "integration")]
public sealed class AccountStatesTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly DeletionWindows Windows = new(TimeSpan.FromDays(30), TimeSpan.FromDays(30));
    private static readonly DateTimeOffset Due = Noon + TimeSpan.FromDays(30);
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// AUTH-SESS-010 AC2: the suspension and the end of every session are one
    /// transaction, so a takedown that does not commit leaves the account active and
    /// its sessions live, and one that does leaves neither.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_010_AC2_TheTakedownEndsSessionsInTheTransactionOfTheSuspensionAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await SessionsAsync(subject, 2);

        await using (StoreContext abandoning = database.Context())
        {
            await using var work = new UnitOfWork(abandoning);

            await work.BeginAsync(TestContext.Current.CancellationToken);

            Assert.True(await States(abandoning).TakeDownAsync(
                subject,
                Noon + TimeSpan.FromHours(1),
                TestContext.Current.CancellationToken));
        }

        Assert.Equal(AccountState.Active, (await StandingAsync(subject)).State);
        Assert.Equal(2, await LiveAsync(subject));

        await using (StoreContext taking = database.Context())
        {
            await using var work = new UnitOfWork(taking);

            await work.BeginAsync(TestContext.Current.CancellationToken);

            Assert.True(await States(taking).TakeDownAsync(
                subject,
                Noon + TimeSpan.FromHours(1),
                TestContext.Current.CancellationToken));

            await work.CommitAsync(TestContext.Current.CancellationToken);
        }

        AccountStanding standing = await StandingAsync(subject);

        Assert.Equal(AccountState.Deleting, standing.State);
        Assert.Equal(DeletionOrigin.Takedown, standing.DeletingBy);
        Assert.Equal(Noon + TimeSpan.FromHours(1), standing.DeletingSince);
        Assert.Equal(0, await LiveAsync(subject));
    }

    /// <summary>
    /// IDN-LIFE-003 AC4: at the trigger no personal field is destroyed and no erasures
    /// row is written, so the subject's key is as it was.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003_AC4_TheTakedownDestroysNothingAndWritesNoErasureAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await TakenDownAsync(subject);

        await using StoreContext reading = database.Context();

        Assert.Equal(
            PersonalDataFormat.Marker,
            (await reading.SubjectKeys.SingleAsync(
                key => key.Id == SubjectKeyId.Of(subject),
                TestContext.Current.CancellationToken)).FormatMarker);
        Assert.False(await reading.Erasures.AnyAsync(
            erasure => erasure.Subject == subject,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-LIFE-003 AC5: the reversal restores the account to active and clears why
    /// it was leaving, and a second reversal finds nothing to reverse.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003_AC5_TheReversalRestoresActiveAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await TakenDownAsync(subject);

        Assert.True(await ReversedAsync(subject));

        AccountStanding standing = await StandingAsync(subject);

        Assert.Equal(AccountState.Active, standing.State);
        Assert.Null(standing.DeletingBy);
        Assert.Null(standing.DeletingSince);
        Assert.False(await ReversedAsync(subject));
    }

    /// <summary>
    /// PRIV-RIGHT-004: a restriction decided while the account is in a takedown's window
    /// is held in the row, once, and the reversal brings the account back restricted.
    /// </summary>
    [Fact]
    public async Task PRIV_RIGHT_004_ARestrictionIsHeldThroughATakedownAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await TakenDownAsync(subject);

        await using (StoreContext restricting = database.Context())
        {
            Assert.True(await States(restricting).RestrictAsync(
                subject,
                TestContext.Current.CancellationToken));
            await restricting.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext again = database.Context())
        {
            Assert.False(await States(again).RestrictAsync(
                subject,
                TestContext.Current.CancellationToken));
        }

        Assert.True(await ReversedAsync(subject));

        Assert.Equal(AccountState.Restricted, (await StandingAsync(subject)).State);
    }

    /// <summary>
    /// IDN-LIFE-003 (D-166): an account already in its own deletion window is taken
    /// down, holding that deletion with the instant it began, a second takedown finds
    /// nothing to take, and the reversal returns the account to its own window.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003_ARunningDeletionIsTakenDownAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        DateTimeOffset began = Noon - TimeSpan.FromDays(2);

        await using (StoreContext deleting = database.Context())
        {
            Assert.True(await States(deleting).BeginDeletionAsync(
                subject,
                DeletionOrigin.Self,
                began,
                TestContext.Current.CancellationToken));
            await deleting.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await TakenDownAsync(subject);

        AccountStanding taken = await StandingAsync(subject);

        Assert.Equal((AccountState.Deleting, DeletionOrigin.Takedown), (taken.State, taken.DeletingBy));
        Assert.Equal((Noon, began), (taken.DeletingSince, taken.DeletionHeldSince));

        await using (StoreContext again = database.Context())
        {
            Assert.False(await States(again).TakeDownAsync(
                subject,
                Noon,
                TestContext.Current.CancellationToken));
        }

        Assert.True(await ReversedAsync(subject));

        AccountStanding reversed = await StandingAsync(subject);

        Assert.Equal(
            (AccountState.Deleting, DeletionOrigin.Self, began, null),
            (reversed.State, reversed.DeletingBy, reversed.DeletingSince, reversed.DeletionHeldSince));
    }

    /// <summary>
    /// IDN-LIFE-013: a takedown reversed on an account an administrator suspended
    /// leaves it suspended by the administrator, so reactivating it still needs
    /// <c>account:manage</c> and no reversal stands in for that.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_013_AReversedTakedownLeavesTheAccountToAnAdministratorAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using (StoreContext suspending = database.Context())
        {
            var store = new AccountStore(suspending);
            Account account = Assert.IsType<Account>(
                await store.FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

            account.Suspend();
            await store.RecordTransitionAsync(account, TestContext.Current.CancellationToken);
            await suspending.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await TakenDownAsync(subject);

        Assert.True(await ReversedAsync(subject));

        await using StoreContext reading = database.Context();

        Account read = Assert.IsType<Account>(
            await new AccountStore(reading).FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal((AccountState.Suspended, SuspensionOrigin.Administrator), (read.State, read.SuspendedBy));
        Assert.Null(read.SuspensionHeld);
        Assert.Null(read.DeletingBy);
    }

    /// <summary>
    /// IDN-LIFE-003: an erasure at the window's end holds the account's row, so a
    /// reversal made in the window's last instant waits for it and then refuses, and
    /// the account stays erased.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003_AReversalAtTheBoundaryWaitsForTheErasureAndRefusesAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await TakenDownAsync(subject);

        await using StoreContext erasing = database.Context();
        await using var erasure = new UnitOfWork(erasing);
        await using StoreContext reversing = database.Context();
        await using var reversal = new UnitOfWork(reversing);

        await erasure.BeginAsync(cancellationToken);
        _ = await Eraser(erasing).EraseAsync(subject, ErasureReason.MinorTakedown, Due, cancellationToken);

        await reversal.BeginAsync(cancellationToken);

        Task<bool> reversed = States(reversing)
            .ReverseTakedownAsync(subject, Due - TimeSpan.FromMilliseconds(1), Windows, cancellationToken)
            .AsTask();

        await BlockedAsync(cancellationToken);

        Assert.False(reversed.IsCompleted);

        await erasure.CommitAsync(cancellationToken);

        Assert.False(await reversed.WaitAsync(Bound, cancellationToken));
        Assert.Equal(AccountState.Deleted, (await StandingAsync(subject)).State);
    }

    /// <summary>
    /// IDN-LIFE-003: a reversal in the window's last instant holds the account's row, so
    /// the erasure at the window's end waits for it and then refuses the account the
    /// reversal returned, and the account stays active.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003_AnErasureAtTheBoundaryWaitsForTheReversalAndRefusesAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await TakenDownAsync(subject);

        await using StoreContext reversing = database.Context();
        await using var reversal = new UnitOfWork(reversing);
        await using StoreContext erasing = database.Context();
        await using var erasure = new UnitOfWork(erasing);

        await reversal.BeginAsync(cancellationToken);

        Assert.True(await States(reversing).ReverseTakedownAsync(
            subject,
            Due - TimeSpan.FromMilliseconds(1),
            Windows,
            cancellationToken));

        await erasure.BeginAsync(cancellationToken);

        Task<Erasure> erased = Eraser(erasing)
            .EraseAsync(subject, ErasureReason.MinorTakedown, Due, cancellationToken)
            .AsTask();

        await BlockedAsync(cancellationToken);

        Assert.False(erased.IsCompleted);

        await reversal.CommitAsync(cancellationToken);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => erased.WaitAsync(Bound, cancellationToken));
        Assert.Equal(AccountState.Active, (await StandingAsync(subject)).State);
    }

    /// <summary>
    /// IDN-LIFE-003 AC4, CONV-DESIGN-002: <c>AccountSuspended</c> is an event row
    /// written in the trigger's transaction, so a trigger that commits carries one row
    /// and one that rolls back carries none and leaves the account as it stood.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003_AC4_TheSuspensionRowCommitsWithTheTriggerAsync()
    {
        SubjectId abandoned = await _deployment.AccountAsync(Noon);
        SubjectId committed = await _deployment.AccountAsync(Noon);

        await using (StoreContext abandoning = database.Context())
        {
            await using var work = new UnitOfWork(abandoning);

            await work.BeginAsync(TestContext.Current.CancellationToken);

            Assert.True(await States(abandoning).TakeDownAsync(
                abandoned,
                Noon,
                TestContext.Current.CancellationToken));
            Assert.True(await SuspendedAsync(abandoning, work, abandoned));
        }

        await using (StoreContext committing = database.Context())
        {
            await using var work = new UnitOfWork(committing);

            await work.BeginAsync(TestContext.Current.CancellationToken);

            Assert.True(await States(committing).TakeDownAsync(
                committed,
                Noon,
                TestContext.Current.CancellationToken));
            Assert.True(await SuspendedAsync(committing, work, committed));

            await work.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        IReadOnlyList<PendingEvent> due = await new PendingEvents(reading).DueAsync(
            Noon.AddDays(1),
            100,
            TestContext.Current.CancellationToken);

        Assert.Equal(committed, Assert.Single(due, pending => pending.Raised is AccountSuspended).Raised.Subject);
        Assert.Equal(AccountState.Active, (await StandingAsync(abandoned)).State);
    }

    /// <summary>
    /// OPS-BOOT-002: the reserved account reads back as the reserved account, a
    /// takedown leaves it standing, and the database holds no second one.
    /// </summary>
    [Fact]
    public async Task OPS_BOOT_002_TheReservedAccountIsNeverTakenDownAsync()
    {
        SubjectId reserved = Subjects.New();

        await using (StoreContext writing = database.Context())
        {
            await new AccountStore(writing).AddAsync(
                Account.CreateEmergency(reserved, Noon),
                TestContext.Current.CancellationToken);
            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext taking = database.Context())
        {
            Assert.False(await States(taking).TakeDownAsync(
                reserved,
                Noon,
                TestContext.Current.CancellationToken));
        }

        await using StoreContext reading = database.Context();

        Account read = Assert.IsType<Account>(
            await new AccountStore(reading).FindBySubjectAsync(reserved, TestContext.Current.CancellationToken));

        Assert.True(read.IsEmergency);
        Assert.Equal(AccountState.Active, read.State);

        await using StoreContext again = database.Context();

        DbUpdateException refusal = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await new AccountStore(again).AddAsync(
                Account.CreateEmergency(Subjects.New(), Noon),
                TestContext.Current.CancellationToken);
            await again.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        Assert.Equal(
            "ux_accounts_emergency",
            Assert.IsType<PostgresException>(refusal.InnerException).ConstraintName);
    }

    private static async Task<bool> SuspendedAsync(StoreContext context, UnitOfWork work, SubjectId subject) =>
        (await new EventOutbox(new PendingEvents(context), work).PublishAsync(
            new AccountSuspended(Noon, subject.ToString(), SuspensionOrigin.Administrator) { Subject = subject },
            TestContext.Current.CancellationToken))
            .Match(() => true, _ => false);

    private AccountStates States(StoreContext context) =>
        new(new AccountStore(context), Sessions(context));

    private SubjectEraser Eraser(StoreContext context) =>
        new(context, Sessions(context), new ConfigurationStore(context, new DataConnections(context)));

    // The second transaction is waiting on the account row the first holds, as the
    // database itself reports it, so the case lets the first commit only then.
    private async Task BlockedAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(Bound);

        while (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                   "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%identity.accounts%FOR UPDATE%'",
                   cancellationToken: bounded.Token)) == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), bounded.Token);
        }
    }

    private async Task<bool> ReversedAsync(SubjectId subject)
    {
        await using StoreContext reversing = database.Context();
        await using var work = new UnitOfWork(reversing);

        await work.BeginAsync(TestContext.Current.CancellationToken);

        bool reversed = await States(reversing).ReverseTakedownAsync(
            subject,
            Noon + TimeSpan.FromHours(1),
            Windows,
            TestContext.Current.CancellationToken);

        await work.CommitAsync(TestContext.Current.CancellationToken);

        return reversed;
    }

    private SessionStore Sessions(StoreContext context) =>
        new(context, _deployment.Ring, _deployment.Randomness);

    private async Task TakenDownAsync(SubjectId subject)
    {
        await using StoreContext taking = database.Context();
        await using var work = new UnitOfWork(taking);

        await work.BeginAsync(TestContext.Current.CancellationToken);

        Assert.True(await States(taking).TakeDownAsync(
            subject,
            Noon,
            TestContext.Current.CancellationToken));

        await work.CommitAsync(TestContext.Current.CancellationToken);
    }

    private async Task<AccountStanding> StandingAsync(SubjectId subject)
    {
        await using StoreContext reading = database.Context();

        return Assert.IsType<AccountStanding>(await States(reading).StandingAsync(
            subject,
            TestContext.Current.CancellationToken));
    }

    private async Task<int> LiveAsync(SubjectId subject)
    {
        await using StoreContext reading = database.Context();

        return (await Sessions(reading).LiveOfAsync(
            subject,
            Noon + TimeSpan.FromHours(2),
            TestContext.Current.CancellationToken)).Count;
    }

    private async Task SessionsAsync(SubjectId subject, int count)
    {
        await using StoreContext writing = database.Context();

        foreach (int _ in Enumerable.Range(0, count))
        {
            await Sessions(writing).AddAsync(
                Session.Begin(
                    SessionId.New(TimeProvider.System),
                    subject,
                    new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
                    new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
                    Noon,
                    TimeSpan.FromDays(1),
                    TimeSpan.FromDays(30),
                    breakGlassReason: null),
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                TestContext.Current.CancellationToken);
        }

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
