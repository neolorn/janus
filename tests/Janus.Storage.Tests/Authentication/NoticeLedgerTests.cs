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

    /// <summary>
    /// AUTH-ABUSE-003 AC4: reading whether an address was told marks nothing, so a
    /// notice whose send was refused spends no window; the mark written at an admitted
    /// send suppresses the next notice until the window has passed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_003_AC4_TheWindowIsSpentOnlyByTheMarkAsync()
    {
        string destination = "marked." + Guid.NewGuid().ToString("N") + "@example.test";

        bool refused = await WasToldAsync(destination, Noon, mark: false);
        bool admitted = await WasToldAsync(destination, Noon, mark: true);
        bool inside = await WasToldAsync(destination, Noon + Window - TimeSpan.FromSeconds(1), mark: false);
        bool after = await WasToldAsync(destination, Noon + Window, mark: false);

        Assert.False(refused);
        Assert.False(admitted);
        Assert.True(inside);
        Assert.False(after);
    }

    // One notice judged in a unit of work of its own: whether the address was told
    // inside the window, and the mark where the test stands in for an admitted send.
    private async Task<bool> WasToldAsync(string destination, DateTimeOffset at, bool mark)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);
        var ledger = new NoticeLedger(writing, new DataConnections(writing), Deployment.Fingerprints);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        await ledger.HoldAsync(destination, TestContext.Current.CancellationToken);

        bool told = await ledger.WasToldAsync(destination, at, Window, TestContext.Current.CancellationToken);

        if (mark)
        {
            await ledger.MarkAsync(destination, at, Window, TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return told;
    }

    // An ask judged as the notice is judged: the address's notices held, then whether
    // one stands in the window, and the mark written where none does.
    private async Task<bool> ToldOnceAsync(string destination)
    {
        await using StoreContext writing = database.Context();
        await using var work = new UnitOfWork(writing);
        var ledger = new NoticeLedger(writing, new DataConnections(writing), Deployment.Fingerprints);

        Assert.True((await work.BeginAsync(TestContext.Current.CancellationToken)).Match(_ => true, _ => false));

        await ledger.HoldAsync(destination, TestContext.Current.CancellationToken);

        bool told = !await ledger.WasToldAsync(destination, Noon, Window, TestContext.Current.CancellationToken);

        if (told)
        {
            await ledger.MarkAsync(destination, Noon, Window, TestContext.Current.CancellationToken);
        }

        Assert.True((await work.CommitAsync(TestContext.Current.CancellationToken)).Match(() => true, _ => false));

        return told;
    }
}
