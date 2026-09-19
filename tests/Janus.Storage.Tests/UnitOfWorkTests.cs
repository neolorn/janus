using System;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Settings;
using Microsoft.EntityFrameworkCore;
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
    private const string CountAccounts =
        "SELECT count(*) FROM janus.accounts WHERE subject = @subject";

    private const string CountSettings = "SELECT count(*) FROM janus.settings WHERE key = @key";

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

        Assert.Equal(0, await CountAsync(connection, subject));
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

        Assert.Equal(2, await CountAsync(connection, subject));
    }

    private static async Task<int> CountAsync(NpgsqlConnection connection, SubjectId subject) =>
        await connection.ExecuteScalarAsync<int>(CountAccounts, new { subject = subject.Value })
            + await connection.ExecuteScalarAsync<int>(CountSettings, new { key = Key(subject) });

    // A key of the deployment's own, so that two runs of these tests never collide.
    private static string Key(SubjectId subject) => "account.deletion.grace." + subject;

    private static Task BetweenAsync(bool failing) =>
        failing
            ? Task.FromException(new InvalidOperationException("The operation failed part way through."))
            : Task.CompletedTask;

    private async Task OperationAsync(SubjectId subject, bool failing)
    {
        await using JanusDbContext context = database.Context();
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

        context.Settings.Add(new SettingRecord
        {
            Key = ConfigurationKey.Parse(Key(subject)),
            Value = "P45D",
        });

        await work.CommitAsync(TestContext.Current.CancellationToken);
    }
}
