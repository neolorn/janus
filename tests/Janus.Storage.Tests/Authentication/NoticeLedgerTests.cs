using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Storage.Authentication.Sending;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What the notice ledger keeps when asks about one address arrive at once
/// (AUTH-ABUSE-003, CONV-DESIGN-003).
/// </summary>
/// <remarks>
/// One database serves the class, so each test asks about an address of its own.
/// </remarks>
[Trait("kind", "integration")]
public sealed class NoticeLedgerTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Window = TimeSpan.FromHours(24);

    /// <summary>
    /// AUTH-ABUSE-003 AC4, CONV-DESIGN-003 AC6: asks about one address at once are each
    /// judged with the address's notices held, so the address is told once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_003_AC4_AsksAtOnceTellTheAddressOnceAsync()
    {
        string destination = "atonce." + Guid.NewGuid().ToString("N") + "@example.test";

        bool[] told = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ToldOnceAsync(destination)));

        Assert.Equal(1, told.Count(answer => answer));
    }

    // An ask judged as the notice is judged: the address's notices held, then whether
    // one stands in the window.
    private async Task<bool> ToldOnceAsync(string destination)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);
        var ledger = new NoticeLedger(writing, new DataConnections(writing), Deployment.Fingerprints);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        await ledger.HoldAsync(destination, TestContext.Current.CancellationToken);

        bool told = await ledger.FirstAsync(destination, Noon, Window, TestContext.Current.CancellationToken);

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return told;
    }
}
