using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Privacy.Requests;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Privacy.Requests;
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
                key => key.Subject == subject,
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

        await using (StoreContext reversing = database.Context())
        {
            Assert.True(await States(reversing).ReverseTakedownAsync(
                subject,
                TestContext.Current.CancellationToken));
            await reversing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        AccountStanding standing = await StandingAsync(subject);

        Assert.Equal(AccountState.Active, standing.State);
        Assert.Null(standing.DeletingBy);
        Assert.Null(standing.DeletingSince);

        await using StoreContext again = database.Context();

        Assert.False(await States(again).ReverseTakedownAsync(
            subject,
            TestContext.Current.CancellationToken));
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
            Assert.True(await States(again).ReverseTakedownAsync(
                subject,
                TestContext.Current.CancellationToken));
            await again.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(AccountState.Restricted, (await StandingAsync(subject)).State);
    }

    /// <summary>
    /// IDN-LIFE-003: an account already in its own deletion window is not taken down,
    /// and the refusal writes nothing.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_003_AnAccountInItsOwnWindowIsNotTakenDownAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using (StoreContext deleting = database.Context())
        {
            Assert.True(await States(deleting).BeginDeletionAsync(
                subject,
                DeletionOrigin.Self,
                Noon,
                TestContext.Current.CancellationToken));
            await deleting.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (StoreContext taking = database.Context())
        {
            Assert.False(await States(taking).TakeDownAsync(
                subject,
                Noon,
                TestContext.Current.CancellationToken));
        }

        Assert.Equal(DeletionOrigin.Self, (await StandingAsync(subject)).DeletingBy);
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

    private AccountStates States(StoreContext context) =>
        new(new AccountStore(context), Sessions(context));

    private SessionStore Sessions(StoreContext context) =>
        new(context, _deployment.Keys, _deployment.Randomness);

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
                    satisfiesEveryGate: false),
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                OpaqueToken.Draw(_deployment.Randomness).Fingerprint(),
                TestContext.Current.CancellationToken);
        }

        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
