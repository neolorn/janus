using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Storage.Authentication.Sending;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What the throttle ledger keeps when failures against one scope arrive at once
/// (AUTH-ABUSE-001, CONV-DESIGN-003).
/// </summary>
/// <remarks>
/// One database serves the class, so each test counts against a source of its own.
/// </remarks>
[Trait("kind", "integration")]
public sealed class ThrottleLedgerTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// AUTH-ABUSE-001 AC1, CONV-DESIGN-003 AC6: failures against one scope at once are
    /// each counted from what stands with the scope's counter held, so every one of them
    /// is counted, the first included.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_001_AC1_FailuresAtOnceAreEachCountedAsync()
    {
        const string source = "192.0.2.71";

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => FailedOnceAsync(source)));

        await using StoreContext reading = database.Context();

        Assert.Equal(
            5,
            (await Ledger(reading).FindAsync(ThrottleScope.Source, source, TestContext.Current.CancellationToken))?.Failures);
    }

    private static ThrottleLedger Ledger(StoreContext context) =>
        new(context, new DataConnections(context), Deployment.Fingerprints);

    // A failure counted as the throttle counts one: the scope's counter held, what stands
    // read, and one more written.
    private async Task FailedOnceAsync(string source)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);
        ThrottleLedger ledger = Ledger(writing);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        await ledger.HoldAsync(ThrottleScope.Source, source, TestContext.Current.CancellationToken);

        ThrottleCounter? standing = await ledger.FindAsync(ThrottleScope.Source, source, TestContext.Current.CancellationToken);

        await ledger.FailedAsync(
            ThrottleScope.Source,
            source,
            standing?.Failures ?? 0,
            Noon,
            TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));
    }
}
