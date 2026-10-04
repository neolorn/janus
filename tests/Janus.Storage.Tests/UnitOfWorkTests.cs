using System;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Privacy.SubjectKeys;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The one transaction an operation runs in (CONV-DESIGN-003).
/// </summary>
/// <remarks>
/// The two writes are rows <c>Janus.Storage</c> owns, so the operation is exercised
/// without any project seeing another's internals (D-154).
/// </remarks>
[Trait("kind", "integration")]
public sealed class UnitOfWorkTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const byte Scheme = 0x01;
    private const int WrappedKeyLength = 40;

    private const string CountAccounts =
        "SELECT count(*) FROM identity.accounts WHERE subject = @subject";

    private const string CountKeys =
        "SELECT count(*) FROM identity.subject_keys WHERE subject = @subject";

    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// CONV-DESIGN-003 AC3: an operation that writes, fails, and would have written
    /// again leaves neither write behind, the commit being the last thing it does and
    /// one it never reaches.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC3_AnOperationThatFailsBetweenTwoWritesLeavesNeitherAsync()
    {
        SubjectId subject = Subjects.New();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await OperationAsync(subject, failing: true));

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(0, await WrittenAsync(connection, subject));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC3: the same operation without the failure leaves both writes,
    /// so what the rollback undid was written in the first place.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC3_TheSameOperationThatSucceedsLeavesBothWritesAsync()
    {
        SubjectId subject = Subjects.New();

        await OperationAsync(subject, failing: false);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(2, await WrittenAsync(connection, subject));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: an operation refused after its unit of work began, having
    /// written and having left a change tracked, leaves no transaction open and nothing
    /// saved, then or by the next commit; the next operation in the same scope begins,
    /// commits, and saves only its own changes.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ARefusedOperationLeavesNothingForTheNextCommitAsync()
    {
        SubjectId refused = Subjects.New();
        SubjectId next = Subjects.New();

        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        await work.BeginAsync(TestContext.Current.CancellationToken);
        context.Accounts.Add(Account(refused));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.SubjectKeys.Add(Key(refused));

        await work.RollbackAsync();

        Assert.Null(context.Database.CurrentTransaction);
        Assert.Empty(context.ChangeTracker.Entries());

        await work.BeginAsync(TestContext.Current.CancellationToken);
        context.Accounts.Add(Account(next));
        await work.CommitAsync(TestContext.Current.CancellationToken);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Null(context.Database.CurrentTransaction);
        Assert.Equal(0, await WrittenAsync(connection, refused));
        Assert.Equal(1, await WrittenAsync(connection, next));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8: an operation that joined another's unit of work and rolls
    /// back leaves nothing of the whole committed: the outer operation's commit commits
    /// nothing and throws a fault.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_AnOuterCommitAfterAnInnerRollbackCommitsNothingAndFaultsAsync()
    {
        SubjectId subject = Subjects.New();

        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        await JoinedAndRolledBackAsync(context, work, subject);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await work.CommitAsync(TestContext.Current.CancellationToken));

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Null(context.Database.CurrentTransaction);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(0, await WrittenAsync(connection, subject));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8: after an inner rollback the outer operation's rollback ends
    /// the unit of work, and the scope's next operation commits its own changes.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_AnOuterRollbackAfterAnInnerRollbackEndsTheUnitOfWorkAsync()
    {
        SubjectId subject = Subjects.New();
        SubjectId next = Subjects.New();

        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        await JoinedAndRolledBackAsync(context, work, subject);
        await work.RollbackAsync();

        Assert.Null(context.Database.CurrentTransaction);

        await work.BeginAsync(TestContext.Current.CancellationToken);
        context.Accounts.Add(Account(next));
        await work.CommitAsync(TestContext.Current.CancellationToken);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(0, await WrittenAsync(connection, subject));
        Assert.Equal(1, await WrittenAsync(connection, next));
    }

    /// <summary>
    /// CONV-DESIGN-002 AC5: what an operation registers on its unit of work runs once
    /// the outermost level has committed and not before, outside any transaction, with
    /// what the operation wrote already there to read, and it may begin and commit a
    /// unit of work of its own.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_002_AC5_WhatIsRegisteredRunsAfterTheOutermostCommitAsync()
    {
        SubjectId subject = Subjects.New();
        SubjectId after = Subjects.New();
        int ran = 0;
        int written = -1;
        bool open = true;

        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        await work.BeginAsync(TestContext.Current.CancellationToken);
        await work.BeginAsync(TestContext.Current.CancellationToken);
        context.Accounts.Add(Account(subject));

        Assert.True(work
            .AfterCommit(async cancellationToken =>
            {
                ran++;
                open = context.Database.CurrentTransaction is not null;

                await using NpgsqlConnection reading = await database.OpenAsync();

                written = await WrittenAsync(reading, subject);

                await work.BeginAsync(cancellationToken);
                context.Accounts.Add(Account(after));
                await work.CommitAsync(cancellationToken);
            })
            .Match(() => true, _ => false));

        await work.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, ran);

        await work.CommitAsync(TestContext.Current.CancellationToken);

        await using NpgsqlConnection connection = await database.OpenAsync();

        Assert.Equal(1, ran);
        Assert.False(open);
        Assert.Equal(1, written);
        Assert.Equal(1, await WrittenAsync(connection, after));
    }

    /// <summary>
    /// CONV-DESIGN-002 AC5, CONV-DESIGN-003: a rollback discards what was registered, at
    /// the outermost level or after a level inside it rolled back, so an operation that
    /// leaves nothing behind runs none of it, then or at the next commit of the scope.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_002_AC5_ARollbackDiscardsWhatWasRegisteredAsync()
    {
        int ran = 0;

        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        await work.BeginAsync(TestContext.Current.CancellationToken);
        Assert.True(work.AfterCommit(_ => Ran()).Match(() => true, _ => false));
        await work.RollbackAsync();

        await work.BeginAsync(TestContext.Current.CancellationToken);
        await work.BeginAsync(TestContext.Current.CancellationToken);
        Assert.True(work.AfterCommit(_ => Ran()).Match(() => true, _ => false));
        await work.RollbackAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await work.CommitAsync(TestContext.Current.CancellationToken));

        await work.BeginAsync(TestContext.Current.CancellationToken);
        await work.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, ran);

        ValueTask Ran()
        {
            ran++;

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// CONV-DESIGN-002: a registration is made on a unit of work in progress; with none,
    /// there is no commit for it to follow, and asking is a fault.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_002_ARegistrationOutsideAUnitOfWorkIsAFaultAsync()
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        Assert.Throws<InvalidOperationException>(() => work.AfterCommit(_ => ValueTask.CompletedTask));
    }

    /// <summary>
    /// CONV-DESIGN-003: the level that opens the transaction is told it is the
    /// outermost, a level that joins it is told it is not, and once the unit of work has
    /// ended the scope's next operation opens the outermost again.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_BeginAnswersWhetherItsLevelIsTheOutermostAsync()
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        bool opened = Outermost(await work.BeginAsync(TestContext.Current.CancellationToken));
        bool joined = Outermost(await work.BeginAsync(TestContext.Current.CancellationToken));
        await work.CommitAsync(TestContext.Current.CancellationToken);
        bool joinedAgain = Outermost(await work.BeginAsync(TestContext.Current.CancellationToken));
        await work.RollbackAsync();
        await work.RollbackAsync();
        bool openedAgain = Outermost(await work.BeginAsync(TestContext.Current.CancellationToken));
        await work.RollbackAsync();

        Assert.True(opened);
        Assert.False(joined);
        Assert.False(joinedAgain);
        Assert.True(openedAgain);
    }

    private static bool Outermost(Result<bool> begun) =>
        begun.Match(
            outermost => outermost,
            error => throw new Xunit.Sdk.XunitException($"The unit of work refused: {error.Code}."));

    private static AccountRecord Account(SubjectId subject) =>
        new()
        {
            Subject = subject,
            CreatedAt = Noon,
            State = AccountState.Active,
        };

    private static SubjectKeyRecord Key(SubjectId subject) =>
        new()
        {
            Id = SubjectKeyId.Of(subject),
            FormatMarker = Scheme,
            KeyVersion = 1,
            WrappedKey = new byte[WrappedKeyLength],
        };

    // The outer operation writes and tracks; the one that joins it writes and rolls
    // back, which ends its own level and marks the whole.
    private static async Task JoinedAndRolledBackAsync(StoreContext context, UnitOfWork work, SubjectId subject)
    {
        await work.BeginAsync(TestContext.Current.CancellationToken);
        context.Accounts.Add(Account(subject));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await work.BeginAsync(TestContext.Current.CancellationToken);
        context.SubjectKeys.Add(Key(subject));
        await work.RollbackAsync();
    }

    private static async Task<int> WrittenAsync(NpgsqlConnection connection, SubjectId subject) =>
        await connection.ExecuteScalarAsync<int>(CountAccounts, new { subject = subject.Value })
            + await connection.ExecuteScalarAsync<int>(CountKeys, new { subject = subject.Value });

    private static Task BetweenAsync(bool failing) =>
        failing
            ? Task.FromException(new InvalidOperationException("The operation failed part way through."))
            : Task.CompletedTask;

    private async Task OperationAsync(SubjectId subject, bool failing)
    {
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);

        await work.BeginAsync(TestContext.Current.CancellationToken);

        context.Accounts.Add(new AccountRecord
        {
            Subject = subject,
            CreatedAt = Noon,
            State = AccountState.Active,
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await BetweenAsync(failing);

        context.SubjectKeys.Add(new SubjectKeyRecord
        {
            Id = SubjectKeyId.Of(subject),
            FormatMarker = Scheme,
            KeyVersion = 1,
            WrappedKey = new byte[WrappedKeyLength],
        });

        await work.CommitAsync(TestContext.Current.CancellationToken);
    }
}
