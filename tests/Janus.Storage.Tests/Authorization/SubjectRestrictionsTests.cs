using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Identity.Accounts;
using Janus.Storage.Authorization.Gate;
using Janus.Storage.Identity.Accounts;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authorization;

/// <summary>
/// The hold the gate judges a restriction under, over two connections
/// (AUTHZ-GATE-006, CONV-DESIGN-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class SubjectRestrictionsTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// AUTHZ-GATE-006: outside a transaction nothing can be held, so the port answers
    /// nothing and the gate reads the state as it did.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_OutsideATransactionNothingIsHeldAsync()
    {
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using StoreContext context = database.Context();

        Assert.Null(await new SubjectRestrictions(context).HoldAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC3, CONV-DESIGN-003 AC6: a restriction begun while an admitted
    /// action holds the row waits for that action to commit.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ARestrictionWaitsForTheActionThatHoldsTheRowAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using StoreContext acting = database.Context();
        await using var action = new UnitOfWork(acting);
        await using StoreContext restricting = database.Context();
        await using var restriction = new UnitOfWork(restricting);

        await action.BeginAsync(cancellationToken);

        Assert.False(await new SubjectRestrictions(acting).HoldAsync(subject, cancellationToken));

        await restriction.BeginAsync(cancellationToken);

        Task restricted = RestrictAsync(new AccountStore(restricting), subject, cancellationToken);

        await BlockedAsync("FOR UPDATE", cancellationToken);

        Assert.False(restricted.IsCompleted);

        await action.CommitAsync(cancellationToken);
        await restricted.WaitAsync(Bound, cancellationToken);
        await restriction.CommitAsync(cancellationToken);

        Assert.Equal(AccountState.Restricted, await StateAsync(subject));
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC3, CONV-DESIGN-003 AC6: a restriction under way is waited for,
    /// and the action that waited reads the restriction it committed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_AnActionWaitsForARestrictionUnderWayAndReadsItAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using StoreContext acting = database.Context();
        await using var action = new UnitOfWork(acting);
        await using StoreContext restricting = database.Context();
        await using var restriction = new UnitOfWork(restricting);

        await restriction.BeginAsync(cancellationToken);
        await RestrictAsync(new AccountStore(restricting), subject, cancellationToken);

        await action.BeginAsync(cancellationToken);

        Task<bool?> judged = new SubjectRestrictions(acting).HoldAsync(subject, cancellationToken).AsTask();

        await BlockedAsync("FOR SHARE", cancellationToken);

        Assert.False(judged.IsCompleted);

        await restriction.CommitAsync(cancellationToken);

        Assert.True(await judged.WaitAsync(Bound, cancellationToken));

        await action.RollbackAsync();
    }

    /// <summary>
    /// AUTHZ-GATE-006: the hold is shared, so two actions of one account at once do not
    /// wait for each other.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_TwoActionsOfOneAccountHoldTheRowTogetherAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using StoreContext first = database.Context();
        await using var one = new UnitOfWork(first);
        await using StoreContext second = database.Context();
        await using var other = new UnitOfWork(second);

        await one.BeginAsync(cancellationToken);
        await other.BeginAsync(cancellationToken);

        Assert.False(await new SubjectRestrictions(first).HoldAsync(subject, cancellationToken));
        Assert.False(await new SubjectRestrictions(second)
            .HoldAsync(subject, cancellationToken)
            .AsTask()
            .WaitAsync(Bound, cancellationToken));

        await one.CommitAsync(cancellationToken);
        await other.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// AUTHZ-GATE-006: an operation that locks the account's row itself takes it for a
    /// change first, and the gate then reads it in the same transaction without
    /// waiting, the tracked account left as the operation holds it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_ARowTheOperationLockedItselfIsJudgedWithoutWaitingAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SubjectId subject = await _deployment.AccountAsync(Noon);

        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        await work.BeginAsync(cancellationToken);

        Account held = Assert.IsType<Account>(await new AccountStore(context).HoldAsync(subject, cancellationToken));

        Assert.False(await new SubjectRestrictions(context)
            .HoldAsync(subject, cancellationToken)
            .AsTask()
            .WaitAsync(Bound, cancellationToken));
        Assert.Equal(AccountState.Active, held.State);

        await work.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// AUTHZ-GATE-006: a subject the library holds no account for has no row to hold
    /// and is not restricted.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_ASubjectWithNoAccountIsNotRestrictedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        await work.BeginAsync(cancellationToken);

        Assert.False(await new SubjectRestrictions(context).HoldAsync(Subjects.New(), cancellationToken));

        await work.RollbackAsync();
    }

    // The restriction as the transition writes it: the row taken for a change, then
    // the state, saved when its unit of work commits.
    private static async Task RestrictAsync(
        AccountStore store,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        Account account = Assert.IsType<Account>(await store.HoldAsync(subject, cancellationToken));

        account.Restrict();

        await store.RecordTransitionAsync(account, cancellationToken);
    }

    private async Task<AccountState> StateAsync(SubjectId subject)
    {
        await using StoreContext context = database.Context();

        return Assert.IsType<Account>(
            await new AccountStore(context).FindBySubjectAsync(subject, TestContext.Current.CancellationToken)).State;
    }

    // The second transaction is waiting on the account row the first holds, as the
    // database itself reports it, so the case lets the first commit only then.
    private async Task BlockedAsync(string strength, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await database.OpenAsync();

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(Bound);

        while (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                   "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%identity.accounts%' || @strength || '%'",
                   new { strength },
                   cancellationToken: bounded.Token)) == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), bounded.Token);
        }
    }
}
