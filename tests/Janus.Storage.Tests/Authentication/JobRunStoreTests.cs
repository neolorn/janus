using System;
using System.Threading.Tasks;
using Janus.Storage.Authentication.Background;
using Xunit;

namespace Janus.Storage.Tests.Authentication;

/// <summary>
/// What the background jobs' runs are kept as: a run claimed once in its interval, and
/// a job that stopped succeeding raised once in the deduplication window (INF-BG-001).
/// </summary>
/// <remarks>
/// One database serves the class, so each test runs a job of its own name.
/// </remarks>
[Trait("kind", "integration")]
public sealed class JobRunStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    /// <summary>
    /// INF-BG-001 AC1: a run is the first asker's once its interval has passed since
    /// the last attempt, and nobody's before.
    /// </summary>
    [Fact]
    public async Task INF_BG_001_AC1_ARunIsClaimedOnceInItsIntervalAsync()
    {
        const string job = "claimed-once";

        Assert.True(await ClaimedAsync(job, Noon));
        Assert.False(await ClaimedAsync(job, Noon));
        Assert.False(await ClaimedAsync(job, Noon + Interval - TimeSpan.FromSeconds(1)));
        Assert.True(await ClaimedAsync(job, Noon + Interval));
    }

    /// <summary>
    /// INF-BG-001 AC2: a job whose last success is older than twice its interval has
    /// lapsed, and the lapse is raised once in the window rather than on every look.
    /// </summary>
    [Fact]
    public async Task INF_BG_001_AC2_AJobWhoseLastSuccessIsTwoIntervalsOldLapsesOnceAWindowAsync()
    {
        const string job = "lapsed-after-success";

        Assert.True(await ClaimedAsync(job, Noon));
        await SucceededAsync(job, Noon);

        DateTimeOffset stale = Noon + (Interval * 2);

        Assert.False(await LapsedAsync(job, stale));
        Assert.True(await LapsedAsync(job, stale + TimeSpan.FromSeconds(1)));
        Assert.False(await LapsedAsync(job, stale + TimeSpan.FromMinutes(1)));
        Assert.True(await LapsedAsync(job, stale + TimeSpan.FromSeconds(1) + Window));
    }

    /// <summary>
    /// INF-BG-001 AC2: a job that never once succeeded lapses two intervals after it was
    /// first recorded, and one that succeeds again stops lapsing.
    /// </summary>
    [Fact]
    public async Task INF_BG_001_AC2_AJobThatNeverSucceededLapsesFromItsFirstRecordingAsync()
    {
        const string job = "never-succeeded";

        Assert.True(await ClaimedAsync(job, Noon));

        DateTimeOffset lapsed = Noon + (Interval * 2) + TimeSpan.FromSeconds(1);

        Assert.True(await LapsedAsync(job, lapsed));

        await SucceededAsync(job, lapsed + Window - TimeSpan.FromMinutes(1));

        Assert.False(await LapsedAsync(job, lapsed + Window));
    }

    /// <summary>
    /// INF-BG-001 AC2: a job the worker has never asked about has nothing to lapse
    /// from, so no alert is raised for it.
    /// </summary>
    [Fact]
    public async Task INF_BG_001_AC2_AJobNeverRecordedDoesNotLapseAsync() =>
        Assert.False(await LapsedAsync("never-recorded", Noon + Window));

    private async Task<bool> ClaimedAsync(string job, DateTimeOffset at)
    {
        await using StoreContext context = database.Context();

        return await new JobRunStore(new DataConnections(context))
            .ClaimAsync(job, at, Interval, TestContext.Current.CancellationToken);
    }

    private async Task SucceededAsync(string job, DateTimeOffset at)
    {
        await using StoreContext context = database.Context();

        await new JobRunStore(new DataConnections(context))
            .SucceededAsync(job, at, TestContext.Current.CancellationToken);
    }

    private async Task<bool> LapsedAsync(string job, DateTimeOffset at)
    {
        await using StoreContext context = database.Context();

        return await new JobRunStore(new DataConnections(context))
            .LapsedAsync(job, at, Interval, Window, TestContext.Current.CancellationToken);
    }
}
