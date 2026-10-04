using System;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Storage.Authentication.Sending;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What the balance readings answer the floor with (AUTH-ABUSE-006, INT-SMS-004).
/// </summary>
/// <remarks>
/// One database serves the class and its one test, which reads the table from empty.
/// </remarks>
[Trait("kind", "integration")]
public sealed class SmsBalanceLedgerTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Retained = TimeSpan.FromDays(8);

    /// <summary>
    /// AUTH-ABUSE-006 AC3: the floor is judged on the latest balance a poll recorded,
    /// however old and in whatever order the readings were written, and before a first
    /// balance is recorded there is none to judge on.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_006_AC3_TheLatestRecordedBalanceIsReadHoweverOldAsync()
    {
        BalanceReading? before = await LatestAsync();

        await RecordedAsync(new BalanceReading(Noon - TimeSpan.FromDays(2), 40m));
        await RecordedAsync(new BalanceReading(Noon - TimeSpan.FromDays(3), 500m));

        BalanceReading? latest = await LatestAsync();

        Assert.Null(before);
        Assert.Equal(new BalanceReading(Noon - TimeSpan.FromDays(2), 40m), latest);
    }

    private async Task<BalanceReading?> LatestAsync()
    {
        await using StoreContext reading = database.Context();

        return await new SmsBalanceLedger(reading).LatestAsync(TestContext.Current.CancellationToken);
    }

    private async Task RecordedAsync(BalanceReading reading)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        await new SmsBalanceLedger(writing).RecordAsync(reading, Retained, TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));
    }
}
