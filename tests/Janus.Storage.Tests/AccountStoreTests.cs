using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The account as the <c>accounts</c> row carries it (IDN-ACCT-002, IDN-ACCT-007,
/// IDN-LIFE-003, CONV-DESIGN-003).
/// </summary>
/// <remarks>
/// The port implementation is tested against the aggregate it translates, with the real
/// database (D-156). What the account decides about its transitions is the identity
/// project's own test; what is proved here is that the row carries the decision back
/// unchanged.
/// </remarks>
[Trait("kind", "integration")]
public sealed class AccountStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// An account written through the store reads back as the account that was written,
    /// every field of it.
    /// </summary>
    [Fact]
    public async Task FindBySubjectAsync_AnAccountThatWasWritten_ReadsBackEveryFieldAsync()
    {
        SubjectId subject = Subjects.New();

        await using StoreContext writing = database.Context();
        await AddAsync(writing, Account.Create(subject, Noon));

        await using StoreContext reading = database.Context();
        Account read = Assert.IsType<Account>(
            await new AccountStore(reading).FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(subject, read.Subject);
        Assert.Equal(Noon, read.CreatedAt);
        Assert.Equal(AccountState.Active, read.State);
        Assert.Null(read.SuspendedBy);
        Assert.Null(read.DeletingBy);
        Assert.Null(read.DeletingSince);
    }

    /// <summary>
    /// A subject with no row is nothing to read, not an account in some default state.
    /// </summary>
    [Fact]
    public async Task FindBySubjectAsync_ASubjectWithNoRow_ReadsNothingAsync()
    {
        await using StoreContext context = database.Context();

        Assert.Null(await new AccountStore(context)
            .FindBySubjectAsync(Subjects.New(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A takedown sets three columns at once, and the row carries all three; that the
    /// transition is the one a takedown makes is AccountTests IDN_LIFE_003_AC4.
    /// </summary>
    [Fact]
    public async Task RecordTransitionAsync_ATakedown_CarriesEveryColumnItSetAsync()
    {
        SubjectId subject = Subjects.New();

        await using StoreContext writing = database.Context();
        var account = Account.Create(subject, Noon);
        await AddAsync(writing, account);

        account.Takedown(Noon.AddHours(1));
        var store = new AccountStore(writing);
        await store.RecordTransitionAsync(account, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using StoreContext reading = database.Context();
        Account read = Assert.IsType<Account>(
            await new AccountStore(reading).FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(AccountState.Deleting, read.State);
        Assert.Equal(SuspensionOrigin.Administrator, read.SuspendedBy);
        Assert.Equal(DeletionOrigin.Takedown, read.DeletingBy);
        Assert.Equal(Noon.AddHours(1), read.DeletingSince);
    }

    /// <summary>
    /// A transition the store cancels clears the columns it set, so no origin outlives
    /// the state that gave it meaning.
    /// </summary>
    [Fact]
    public async Task RecordTransitionAsync_ACancelledDeletion_ClearsTheWindowAsync()
    {
        SubjectId subject = Subjects.New();

        await using StoreContext writing = database.Context();
        var account = Account.Create(subject, Noon);
        await AddAsync(writing, account);

        var store = new AccountStore(writing);
        account.RequestDeletion(DeletionOrigin.Self, Noon.AddHours(1));
        await store.RecordTransitionAsync(account, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        account.CancelDeletion();
        await store.RecordTransitionAsync(account, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using StoreContext reading = database.Context();
        Account read = Assert.IsType<Account>(
            await new AccountStore(reading).FindBySubjectAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(AccountState.Active, read.State);
        Assert.Null(read.DeletingBy);
        Assert.Null(read.DeletingSince);
    }

    /// <summary>
    /// A transition of an account that has no row is a fault in the caller, not a write
    /// that quietly creates one.
    /// </summary>
    [Fact]
    public async Task RecordTransitionAsync_AnAccountWithNoRow_ThrowsAsync()
    {
        await using StoreContext context = database.Context();

        var account = Account.Create(Subjects.New(), Noon);
        account.Restrict();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new AccountStore(context)
                .RecordTransitionAsync(account, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-ACCT-002 AC2: a deleted account keeps its row, so the subject it was issued
    /// is never issued again.
    /// </summary>
    [Fact]
    public async Task IDN_ACCT_002_AC2_ADeletedAccountsSubjectIsNotIssuedAgainAsync()
    {
        SubjectId subject = Subjects.New();

        await using StoreContext writing = database.Context();
        var account = Account.Create(subject, Noon);
        await AddAsync(writing, account);

        account.RequestDeletion(DeletionOrigin.Self, Noon.AddHours(1));
        account.MarkErased();

        var store = new AccountStore(writing);
        await store.RecordTransitionAsync(account, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using StoreContext again = database.Context();

        DbUpdateException refusal = await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await AddAsync(again, Account.Create(subject, Noon.AddDays(1))));

        Assert.Equal(
            "pk_accounts",
            Assert.IsType<PostgresException>(refusal.InnerException).ConstraintName);
    }

    /// <summary>
    /// IDN-LIFE-014: the read the deletion sweep runs on carries the accounts whose
    /// window began by an instant, oldest first, and no account in another state.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_014_DeletingSinceAsync_CarriesTheWindowsThatBeganByAnInstantAsync()
    {
        var first = Account.Create(Subjects.New(), Noon);
        var second = Account.Create(Subjects.New(), Noon);
        var later = Account.Create(Subjects.New(), Noon);
        var standing = Account.Create(Subjects.New(), Noon);

        first.Takedown(Noon.AddHours(1));
        second.RequestDeletion(DeletionOrigin.Self, Noon);
        later.RequestDeletion(DeletionOrigin.Self, Noon.AddHours(3));

        await using StoreContext writing = database.Context();

        foreach (Account account in new[] { first, second, later, standing })
        {
            await AddAsync(writing, account);
        }

        await using StoreContext reading = database.Context();

        IReadOnlyList<Account> elapsed = await new AccountStore(reading)
            .DeletingSinceAsync(Noon.AddHours(2), TestContext.Current.CancellationToken);

        IReadOnlyList<Account> mine =
        [
            .. elapsed.Where(account =>
                account.Subject == first.Subject
                || account.Subject == second.Subject
                || account.Subject == later.Subject
                || account.Subject == standing.Subject),
        ];

        Assert.Equal<IEnumerable<SubjectId>>(
            [second.Subject, first.Subject],
            [.. mine.Select(account => account.Subject)]);

        Assert.Equal(DeletionOrigin.Takedown, mine[1].DeletingBy);
        Assert.Equal(Noon.AddHours(1), mine[1].DeletingSince);
    }

    private static async Task AddAsync(StoreContext context, Account account)
    {
        await new AccountStore(context).AddAsync(account, TestContext.Current.CancellationToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
