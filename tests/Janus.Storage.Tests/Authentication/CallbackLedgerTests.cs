using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Storage.Authentication.Callbacks;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What the callback ledger counts when callbacks from one source arrive at once
/// (INT-GEN-003, CONV-DESIGN-003).
/// </summary>
/// <remarks>
/// One database serves the class, so each test counts callbacks from a source of its own.
/// </remarks>
[Trait("kind", "integration")]
public sealed class CallbackLedgerTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    /// <summary>
    /// INT-GEN-003, CONV-DESIGN-003 AC6: callbacks from one source at once are each
    /// counted with the source's callbacks held, so no more are admitted than the limit.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_GEN_003_CallbacksAtOnceAdmitNoMoreThanTheLimitAsync()
    {
        const string source = "192.0.2.81";
        DateTimeOffset at = DateTimeOffset.UtcNow;

        int[] made = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => CountedOnceAsync(source, at)));

        Assert.Equal([1, 2, 3, 4, 5], made.Order());
    }

    // A callback counted as the admission counts one: the source's callbacks held, and
    // the count taken with this one in it.
    private async Task<int> CountedOnceAsync(string source, DateTimeOffset at)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);
        var ledger = new CallbackLedger(writing, new DataConnections(writing), Deployment.Fingerprints);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        await ledger.HoldAsync(source, TestContext.Current.CancellationToken);

        int made = await ledger.ReceivedAsync(source, at, Minute, TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return made;
    }
}
