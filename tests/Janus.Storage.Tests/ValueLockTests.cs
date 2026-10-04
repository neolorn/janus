using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Identity.Identifiers;
using Janus.Privacy.Erasures;
using Janus.Storage.Authentication.Identifiers;
using Janus.Storage.Authentication.Registration;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Identity.Preferences;
using Janus.Storage.Identity.Profiles;
using Janus.Storage.Privacy.Erasures;
using Janus.Storage.Privacy.SubjectKeys;
using Janus.Storage.Settings;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The lock a value is judged and written under, taken by two operations at once on two
/// connections (CONV-DESIGN-003, REG-SESS-005, REG-IDENT-006, REG-IDENT-009,
/// OPS-SEC-003).
/// </summary>
/// <remarks>
/// Each case lets the first operation take the value's lock and keeps it there until
/// the database itself reports the second waiting on that lock, so the two overlap by
/// construction and not by chance. The second then judges what the first committed.
/// </remarks>
[Trait("kind", "integration")]
public sealed class ValueLockTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    private static readonly byte[] Rotated = Encoding.UTF8.GetBytes("the fingerprint key after the rotation");

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// REG-SESS-005 AC6: a terms step and the verification of another account's add of
    /// the same value, at once on two connections, end with one holder of the value.
    /// Whichever takes the lock first writes it, the other finds it held under the lock
    /// and writes nothing, and neither meets the unique constraint.
    /// </summary>
    /// <param name="termsStepFirst">Whether the terms step takes the lock first.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task REG_SESS_005_AC6_ATermsStepAndAnotherAccountsAddAtOnceLeaveOneHolderAsync(bool termsStepFirst)
    {
        string entered = Fresh("Ahmed");
        string canonical = Canonicalised(entered);
        SubjectId registering = Subjects.New();
        SubjectId adding = await _deployment.AccountAsync(Noon);
        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<bool> TermsStepAsync(TaskCompletionSource? held) =>
            DecidedAsync(
                context => Registration(context).LockValuesAsync(
                    [(IdentifierKind.Email, canonical)],
                    TestContext.Current.CancellationToken),
                context => RegisteredAsync(context, registering, entered, canonical),
                held);

        Task<bool> AddAsync(TaskCompletionSource? held) =>
            DecidedAsync(
                context => Directory(context).LockValuesAsync(
                    [(IdentifierKind.Email, canonical)],
                    TestContext.Current.CancellationToken),
                context => AddedAsync(context, adding, entered, canonical),
                held);

        Task<bool> first = termsStepFirst ? TermsStepAsync(holding) : AddAsync(holding);
        _ = await Task.WhenAny(holding.Task, first);
        Task<bool> second = termsStepFirst ? AddAsync(null) : TermsStepAsync(null);
        bool[] wrote = await Task.WhenAll(first, second);

        Assert.Equal([true, false], wrote);
        Assert.Equal(termsStepFirst ? registering : adding, await OwnerAsync(IdentifierKind.Email, canonical));
        Assert.Equal(1, await HoldersAsync(canonical));
    }

    /// <summary>
    /// REG-IDENT-004 AC7: two accounts that added one value no account holds and verify
    /// it at once on two connections end with one identifier, on the account that takes
    /// the value's lock first. The second judges under the lock, finds the value held
    /// and writes nothing, so it never meets the unique constraint.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_004_AC7_TwoAccountsVerifyingOneValueAtOnceLeaveItOnTheFirstAsync()
    {
        string entered = Fresh("Yusuf");
        string canonical = Canonicalised(entered);
        SubjectId one = await _deployment.AccountAsync(Noon);
        SubjectId other = await _deployment.AccountAsync(Noon);
        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<bool> VerifiedAsync(SubjectId subject, TaskCompletionSource? held) =>
            DecidedAsync(
                context => Directory(context).LockValuesAsync(
                    [(IdentifierKind.Email, canonical)],
                    TestContext.Current.CancellationToken),
                context => AddedAsync(context, subject, entered, canonical),
                held);

        Task<bool> first = VerifiedAsync(one, holding);
        _ = await Task.WhenAny(holding.Task, first);
        Task<bool> second = VerifiedAsync(other, null);
        bool[] wrote = await Task.WhenAll(first, second);

        Assert.Equal([true, false], wrote);
        Assert.Equal(one, await OwnerAsync(IdentifierKind.Email, canonical));
        Assert.Equal(1, await HoldersAsync(canonical));
    }

    /// <summary>
    /// REG-IDENT-006 AC7: another account's add of a value, judged while that value's
    /// removal commits, waits for the removal under the value's lock, finds the value
    /// reserved to the account it was removed from and writes nothing; the undo then
    /// restores the value to that account.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AC7_AnAddJudgedWhileTheRemovalCommitsFindsTheValueReservedAsync()
    {
        string entered = Fresh("Hana");
        string canonical = Canonicalised(entered);
        byte[] undo = RandomNumberGenerator.GetBytes(32);
        SubjectId removing = await _deployment.AccountAsync(Noon);
        SubjectId adding = await _deployment.AccountAsync(Noon);
        IdentifierId given = await HeldBesideAPrimaryAsync(removing, entered);
        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<bool> removal = DecidedAsync(
            context => Directory(context).LockValuesAsync(
                [(IdentifierKind.Email, canonical)],
                TestContext.Current.CancellationToken),
            async context =>
            {
                await Directory(context).GiveUpAsync(
                    removing,
                    given,
                    Noon.AddHours(1),
                    Noon.AddHours(73),
                    undo,
                    TestContext.Current.CancellationToken);

                return true;
            },
            holding);
        _ = await Task.WhenAny(holding.Task, removal);
        Task<bool> add = DecidedAsync(
            context => Directory(context).LockValuesAsync(
                [(IdentifierKind.Email, canonical)],
                TestContext.Current.CancellationToken),
            context => AddedAsync(context, adding, entered, canonical));
        bool[] wrote = await Task.WhenAll(removal, add);

        Assert.Equal([true, false], wrote);
        Assert.Null(await OwnerAsync(IdentifierKind.Email, canonical));

        await using (StoreContext restoring = database.Context())
        {
            IdentifierDirectory directory = Directory(restoring);

            Assert.NotNull(await directory.GivenUpAsync(undo, TestContext.Current.CancellationToken));

            await directory.TakeBackAsync(given, maximum: 5, TestContext.Current.CancellationToken);
            await restoring.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(removing, await OwnerAsync(IdentifierKind.Email, canonical));
        Assert.Equal(1, await HoldersAsync(canonical));
    }

    /// <summary>
    /// REG-IDENT-006 AC8: an account that adds again a value it removed and verifies it
    /// within the window ends the value's reservation, so its undo link answers to no
    /// removal.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AC8_AValueAddedAgainAndVerifiedEndsItsReservationAsync()
    {
        string entered = Fresh("Laila");
        string canonical = Canonicalised(entered);
        byte[] undo = RandomNumberGenerator.GetBytes(32);
        SubjectId subject = await _deployment.AccountAsync(Noon);
        IdentifierId given = await HeldBesideAPrimaryAsync(subject, entered);

        await using (StoreContext removing = database.Context())
        {
            await Directory(removing).GiveUpAsync(
                subject,
                given,
                Noon.AddHours(1),
                Noon.AddHours(73),
                undo,
                TestContext.Current.CancellationToken);
            await removing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(subject, await ReservedToAsync(canonical, Noon.AddHours(2)));

        await using (StoreContext adding = database.Context())
        {
            Assert.True(await AddedAsync(adding, subject, entered, canonical));

            await adding.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Null(await Directory(reading).GivenUpAsync(undo, TestContext.Current.CancellationToken));
        Assert.Null(await ReservedToAsync(canonical, Noon.AddHours(2)));
        Assert.Equal(subject, await OwnerAsync(IdentifierKind.Email, canonical));
    }

    /// <summary>
    /// REG-IDENT-006 AC8: an account that replaces back to a value it replaced ends
    /// that value's reservation, so the first replace's undo link answers to no
    /// removal, and the value the second replace displaced is the one now reserved.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_006_AC8_AReplaceBackToAReplacedValueEndsItsReservationAsync()
    {
        string old = Fresh("Omar");
        string replacement = Fresh("Omar.New");
        byte[] first = RandomNumberGenerator.GetBytes(32);
        byte[] second = RandomNumberGenerator.GetBytes(32);
        SubjectId subject = await _deployment.AccountAsync(Noon);
        IdentifierId id = await HeldPrimaryAsync(subject, old);

        await ReplacedAsync(subject, id, replacement, Noon.AddHours(1), first);
        await ReplacedAsync(subject, id, old, Noon.AddHours(2), second);

        await using StoreContext reading = database.Context();
        IdentifierDirectory directory = Directory(reading);

        Assert.Null(await directory.GivenUpAsync(first, TestContext.Current.CancellationToken));
        Assert.Null(await ReservedToAsync(Canonicalised(old), Noon.AddHours(3)));
        Assert.Equal(subject, await OwnerAsync(IdentifierKind.Email, Canonicalised(old)));
        Assert.NotNull(await directory.GivenUpAsync(second, TestContext.Current.CancellationToken));
        Assert.Equal(subject, await ReservedToAsync(Canonicalised(replacement), Noon.AddHours(3)));
    }

    /// <summary>
    /// REG-IDENT-009 AC5: a choice of a username made while that username's erasure
    /// commits waits for the erasure under the username's lock and finds the name held,
    /// so it is answered as taken and nothing is written.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_009_AC5_AChoiceMadeWhileTheErasureCommitsFindsTheNameHeldAsync()
    {
        Username username = Named("kestrel");
        SubjectId erased = await DeletingAccountAsync();
        SubjectId choosing = await _deployment.AccountAsync(Noon);
        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using (StoreContext writing = database.Context())
        {
            Assert.True(await ChosenAsync(writing, erased, username));

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Task<bool> erasure = DecidedAsync(
            async context => _ = await Eraser(context).EraseAsync(
                erased,
                ErasureReason.ErasureRequest,
                Noon,
                TestContext.Current.CancellationToken),
            _ => Task.FromResult(true),
            holding);
        _ = await Task.WhenAny(holding.Task, erasure);
        Task<bool> choice = DecidedAsync(
            context => Directory(context).LockValuesAsync(
                [(IdentifierKind.Username, username.Value)],
                TestContext.Current.CancellationToken),
            context => ChosenAsync(context, choosing, username));
        bool[] wrote = await Task.WhenAll(erasure, choice);

        Assert.Equal([true, false], wrote);
        Assert.Null(await OwnerAsync(IdentifierKind.Username, username.Value));

        await using StoreContext reading = database.Context();

        Assert.True(await Store(reading).IsHeldAsync(
            username.Value,
            Noon.AddHours(1),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-IDENT-009 AC5: two choices of one free username made at once leave it on
    /// exactly one account. The second judges under the lock, finds the name taken and
    /// writes nothing, so it never meets the unique constraint.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task REG_IDENT_009_AC5_TwoChoicesOfOneFreeNameAtOnceLeaveItOnOneAccountAsync()
    {
        Username username = Named("osprey");
        SubjectId one = await _deployment.AccountAsync(Noon);
        SubjectId other = await _deployment.AccountAsync(Noon);
        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<bool> ChoiceAsync(SubjectId subject, TaskCompletionSource? held) =>
            DecidedAsync(
                context => Directory(context).LockValuesAsync(
                    [(IdentifierKind.Username, username.Value)],
                    TestContext.Current.CancellationToken),
                context => ChosenAsync(context, subject, username),
                held);

        Task<bool> first = ChoiceAsync(one, holding);
        _ = await Task.WhenAny(holding.Task, first);
        Task<bool> second = ChoiceAsync(other, null);
        bool[] wrote = await Task.WhenAll(first, second);

        Assert.Equal([true, false], wrote);
        Assert.Equal(one, await OwnerAsync(IdentifierKind.Username, username.Value));
    }

    /// <summary>
    /// OPS-SEC-003 AC7: a process that holds a fingerprint key version that is not
    /// current finds a value another process fingerprinted under it, and stores every
    /// fingerprint of its own under the version current to it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC7_AVersionHeldAndNotCurrentIsMatchedAndNeverWrittenUnderAsync()
    {
        string ahead = Fresh("Mona");
        string behind = Fresh("Nabil");
        SubjectId one = await _deployment.AccountAsync(Noon);
        SubjectId other = await _deployment.AccountAsync(Noon);

        IdentifierId written = await WriteAsync(one, ahead, Ring(current: 2));
        IdentifierId own = await WriteAsync(other, behind, Ring(current: 1));

        await using StoreContext reading = database.Context();
        IdentifierStore store = Store(reading, Ring(current: 1));
        IdentifierRecord stored = await reading.Identifiers.SingleAsync(
            row => row.Id == written,
            TestContext.Current.CancellationToken);
        IdentifierRecord computed = await reading.Identifiers.SingleAsync(
            row => row.Id == own,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, stored.FingerprintVersion);
        Assert.Equal(
            one,
            await store.FindOwnerAsync(IdentifierKind.Email, Canonicalised(ahead), TestContext.Current.CancellationToken));
        Assert.Equal(1, computed.FingerprintVersion);
        Assert.Equal(
            Fingerprint.Compute(Encoding.UTF8.GetBytes(Canonicalised(behind)), Deployment.FingerprintKey),
            computed.Fingerprint);
        Assert.NotEqual(
            Fingerprint.Compute(Encoding.UTF8.GetBytes(Canonicalised(behind)), Rotated),
            computed.Fingerprint);
    }

    /// <summary>
    /// OPS-SEC-003 AC7, CONV-DESIGN-003: a process on either side of the moment a new
    /// fingerprint key version is made current locks a value under every version it
    /// holds, so the two wait on each other and the second judges what the first wrote.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC7_ProcessesOnEitherSideOfARotationMeetOnOneLockAsync()
    {
        string entered = Fresh("Salma");
        string canonical = Canonicalised(entered);
        SubjectId ahead = await _deployment.AccountAsync(Noon);
        SubjectId behind = await _deployment.AccountAsync(Noon);
        var holding = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<bool> AddAsync(SubjectId subject, int current, TaskCompletionSource? held) =>
            DecidedAsync(
                context => Directory(context, Ring(current)).LockValuesAsync(
                    [(IdentifierKind.Email, canonical)],
                    TestContext.Current.CancellationToken),
                context => AddedAsync(context, subject, entered, canonical, Ring(current)),
                held);

        Task<bool> first = AddAsync(ahead, current: 2, holding);
        _ = await Task.WhenAny(holding.Task, first);
        Task<bool> second = AddAsync(behind, current: 1, null);
        bool[] wrote = await Task.WhenAll(first, second);

        await using StoreContext reading = database.Context();

        Assert.Equal([true, false], wrote);
        Assert.Equal(
            ahead,
            await Store(reading, Ring(current: 1)).FindOwnerAsync(
                IdentifierKind.Email,
                canonical,
                TestContext.Current.CancellationToken));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private static string Fresh(string person) =>
        person + "." + Guid.NewGuid().ToString("N") + "@Example.COM";

    private static string Canonicalised(string entered)
    {
        Assert.True(EmailAddress.TryParse(entered, out EmailAddress address));

        return address.Value;
    }

    private static Username Named(string stem)
    {
        Assert.True(Username.TryParse(stem + Guid.NewGuid().ToString("N")[..8], out Username username));

        return username;
    }

    // One operation: its transaction, the lock on its value, and what it decides under
    // that lock. The one handed a signal holds its lock until the database reports
    // another transaction waiting on a value's lock, and commits only then.
    private async Task<bool> DecidedAsync(
        Func<StoreContext, ValueTask> locked,
        Func<StoreContext, Task<bool>> decided,
        TaskCompletionSource? holding = null)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        Assert.True((await work.BeginAsync(cancellationToken)).Match(() => true, _ => false));

        await locked(context);

        if (holding is not null)
        {
            holding.SetResult();

            await WaitedOnAsync(cancellationToken);
        }

        bool wrote = await decided(context);

        Assert.True((await work.CommitAsync(cancellationToken)).Match(() => true, _ => false));

        return wrote;
    }

    // The other transaction is waiting on an advisory lock this one holds, as the
    // database itself reports it.
    private async Task WaitedOnAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(Bound);

        while (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                   "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted",
                   cancellationToken: bounded.Token)) == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), bounded.Token);
        }
    }

    // The terms step: the value is judged under its lock and the account is written
    // with it only where no account holds it and it is not reserved.
    private async Task<bool> RegisteredAsync(
        StoreContext context,
        SubjectId subject,
        string entered,
        string canonical)
    {
        RegistrationDirectory directory = Registration(context);

        if (await directory.OwnerAsync(IdentifierKind.Email, canonical, TestContext.Current.CancellationToken) is not null
            || await directory.IsReservedAsync(IdentifierKind.Email, canonical, Noon, TestContext.Current.CancellationToken))
        {
            return false;
        }

        await directory.CreateAsync(
            new NewAccount(
                subject,
                Noon,
                Identifiers:
                [
                    new NewIdentifier(
                        IdentifierId.New(TimeProvider.System),
                        IdentifierKind.Email,
                        entered,
                        canonical,
                        Locked: false,
                        VerifiedAt: Noon),
                ],
                DateOfBirth: null,
                AdultAffirmed: true,
                Group: null,
                AnsweredAgeAt: Noon,
                TermsVersion: "1",
                NoticeVersion: "1",
                EmailMaximum: 5,
                PhoneMaximum: 5,
                Language: null),
            TestContext.Current.CancellationToken);

        return true;
    }

    // An add's verification: the value is judged under its lock and written to the
    // account, verified, only where no account holds it and it is not reserved to
    // another account.
    private async Task<bool> AddedAsync(
        StoreContext context,
        SubjectId subject,
        string entered,
        string canonical,
        IKeyRing? ring = null)
    {
        IdentifierDirectory directory = Directory(context, ring);

        if (await directory.OwnerAsync(IdentifierKind.Email, canonical, TestContext.Current.CancellationToken) is not null
            || (await directory.ReservedToAsync(IdentifierKind.Email, canonical, Noon.AddHours(2), TestContext.Current.CancellationToken)
                    is SubjectId reserved
                && reserved != subject))
        {
            return false;
        }

        await directory.TakeOnAsync(
            subject,
            IdentifierId.New(TimeProvider.System),
            IdentifierKind.Email,
            entered,
            canonical,
            Noon.AddHours(2),
            TestContext.Current.CancellationToken);

        return true;
    }

    // A choice of a username: judged taken or held under the name's lock, and written
    // only where it is neither.
    private async Task<bool> ChosenAsync(StoreContext context, SubjectId subject, Username username)
    {
        IdentifierDirectory directory = Directory(context);

        if (await directory.OwnerAsync(IdentifierKind.Username, username.Value, TestContext.Current.CancellationToken) is not null
            || await directory.IsHeldAsync(username.Value, Noon.AddHours(1), TestContext.Current.CancellationToken))
        {
            return false;
        }

        await directory.TakeUsernameAsync(
            subject,
            IdentifierId.New(TimeProvider.System),
            username,
            Noon,
            TestContext.Current.CancellationToken);

        return true;
    }

    private async Task ReplacedAsync(
        SubjectId subject,
        IdentifierId id,
        string entered,
        DateTimeOffset at,
        byte[] undo)
    {
        await using StoreContext context = database.Context();

        await Directory(context).ReplaceAsync(
            subject,
            id,
            entered,
            Canonicalised(entered),
            at,
            at.AddHours(72),
            undo,
            TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // A verified address beside the account's verified primary, so it is free to leave.
    private async Task<IdentifierId> HeldBesideAPrimaryAsync(SubjectId subject, string entered)
    {
        _ = await HeldPrimaryAsync(subject, Fresh("Primary"));

        return await WriteAsync(subject, entered, _deployment.Ring);
    }

    // The first verified address an account takes on is its primary.
    private Task<IdentifierId> HeldPrimaryAsync(SubjectId subject, string entered) =>
        WriteAsync(subject, entered, _deployment.Ring);

    private async Task<IdentifierId> WriteAsync(SubjectId subject, string entered, IKeyRing ring)
    {
        var id = IdentifierId.New(TimeProvider.System);

        await using StoreContext context = database.Context();

        await Directory(context, ring).TakeOnAsync(
            subject,
            id,
            IdentifierKind.Email,
            entered,
            Canonicalised(entered),
            Noon,
            TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return id;
    }

    private async Task<SubjectId> DeletingAccountAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using StoreContext deleting = database.Context();
        AccountRecord record = await deleting.Accounts
            .SingleAsync(row => row.Subject == subject, TestContext.Current.CancellationToken);
        record.State = AccountState.Deleting;
        record.DeletingBy = DeletionOrigin.Self;
        record.DeletingSince = Noon;
        await deleting.SaveChangesAsync(TestContext.Current.CancellationToken);

        return subject;
    }

    private async Task<SubjectId?> OwnerAsync(IdentifierKind kind, string canonical)
    {
        await using StoreContext context = database.Context();

        return await Store(context).FindOwnerAsync(kind, canonical, TestContext.Current.CancellationToken);
    }

    private async Task<SubjectId?> ReservedToAsync(string canonical, DateTimeOffset now)
    {
        await using StoreContext context = database.Context();

        return await Store(context).FindReservedToAsync(
            IdentifierKind.Email,
            canonical,
            now,
            TestContext.Current.CancellationToken);
    }

    private async Task<int> HoldersAsync(string canonical)
    {
        byte[] fingerprint = Fingerprint.Compute(Encoding.UTF8.GetBytes(canonical), Deployment.FingerprintKey);

        await using StoreContext context = database.Context();

        return await context.Identifiers.CountAsync(
            row => row.Fingerprint == fingerprint,
            TestContext.Current.CancellationToken);
    }

    // The key ring of a process during a fingerprint key rotation: both versions held,
    // and the one named current to it.
    private KeyRingInMemory Ring(int current) =>
        new KeyRingInMemory(
            _deployment.Keys,
            new FingerprintKeys(
                current,
                new Dictionary<int, ReadOnlyMemory<byte>>
                {
                    [1] = Deployment.FingerprintKey,
                    [2] = Rotated,
                }));

    private IdentifierStore Store(StoreContext context, IKeyRing? ring = null) =>
        new(context, ring ?? _deployment.Ring, _deployment.Randomness);

    private IdentifierDirectory Directory(StoreContext context, IKeyRing? ring = null) =>
        new(
            Store(context, ring),
            new PreferenceStore(context, ring ?? _deployment.Ring, _deployment.Randomness),
            new PendingVerificationStore(context, ring ?? _deployment.Ring, _deployment.Randomness));

    private RegistrationDirectory Registration(StoreContext context) =>
        new(
            context,
            new AccountStore(context),
            Store(context),
            new ProfileStore(context, _deployment.Ring, _deployment.Randomness),
            new SubjectKeyStore(context, _deployment.Ring, _deployment.Randomness),
            new PreferenceStore(context, _deployment.Ring, _deployment.Randomness));

    private SubjectEraser Eraser(StoreContext context) =>
        new(
            context,
            new SessionStore(context, _deployment.Ring, _deployment.Randomness),
            new ConfigurationStore(context, new DataConnections(context)),
            new DataConnections(context),
            new IdentifierStore(context, _deployment.Ring, _deployment.Randomness));
}
