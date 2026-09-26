using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Storage.Authorization.Gate;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authorization;

/// <summary>
/// What the counts of records each person was given are kept as: one count a person a
/// day that reports add to, and a daily mean recomputed over the window before today
/// (OPS-ALERT-005, D-153).
/// </summary>
/// <remarks>
/// One database serves the class and its tests run one at a time, so each test counts
/// for people of its own and has asserted before another's recount forgets anything.
/// </remarks>
[Trait("kind", "integration")]
public sealed class ReadVolumeStoreTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateOnly Today = new(2026, 9, 24);

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    /// <summary>
    /// OPS-ALERT-005, D-153: reports on one day add to one count, and each is told the
    /// count once it is added; another day or another person is counted apart.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_005_ReportsOnOneDayAddToOneCountAsync()
    {
        var clerk = SubjectId.New(_randomness);
        var manager = SubjectId.New(_randomness);

        Assert.Equal(300, await AddedAsync(clerk, Today, 300));
        Assert.Equal(700, await AddedAsync(clerk, Today, 400));
        Assert.Equal(5, await AddedAsync(clerk, Today.AddDays(-1), 5));
        Assert.Equal(9, await AddedAsync(manager, Today, 9));
    }

    /// <summary>
    /// OPS-ALERT-005, D-071, D-153: the mean is the sum of the days of the window before
    /// today over the window's length, today's count is not in it, a count older than
    /// the window is forgotten, and a person who read nothing inside it has no mean.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_005_TheMeanIsRecomputedOverTheWindowBeforeTodayAsync()
    {
        var clerk = SubjectId.New(_randomness);
        var departed = SubjectId.New(_randomness);

        _ = await AddedAsync(clerk, Today.AddDays(-30), 1500);
        _ = await AddedAsync(clerk, Today.AddDays(-1), 1500);
        _ = await AddedAsync(clerk, Today, 90_000);
        _ = await AddedAsync(departed, Today.AddDays(-31), 4000);

        Assert.True(await RebaselinedAsync() >= 1);

        Assert.Equal(100m, await MeanAsync(clerk));
        Assert.Equal(0m, await MeanAsync(departed));

        await using StoreContext context = database.Context();

        List<ReadVolumeRecord> held = await context.ReadVolume
            .Where(read => read.Actor == clerk || read.Actor == departed)
            .OrderBy(read => read.Day)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [Today.AddDays(-30), Today.AddDays(-1), Today],
            held.Select(read => read.Day));
        Assert.All(held, read => Assert.Equal(clerk, read.Actor));
    }

    /// <summary>
    /// OPS-ALERT-005, D-153: a recount replaces every mean, so a person whose reads have
    /// all left the window no longer has one.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_005_ARecountReplacesEveryMeanAsync()
    {
        var clerk = SubjectId.New(_randomness);

        _ = await AddedAsync(clerk, Today.AddDays(-2), 60);

        Assert.True(await RebaselinedAsync(days: 30) >= 1);
        Assert.Equal(2m, await MeanAsync(clerk));

        _ = await RebaselinedAsync(days: 1);

        Assert.Equal(0m, await MeanAsync(clerk));
    }

    private async Task<long> AddedAsync(SubjectId actor, DateOnly day, int records)
    {
        await using StoreContext context = database.Context();

        return await new ReadVolumeStore(new DataConnections(context))
            .AddAsync(actor, day, records, TestContext.Current.CancellationToken);
    }

    private async Task<decimal> MeanAsync(SubjectId actor)
    {
        await using StoreContext context = database.Context();

        return await new ReadVolumeStore(new DataConnections(context))
            .BaselineAsync(actor, TestContext.Current.CancellationToken);
    }

    private async Task<int> RebaselinedAsync(int days = 30)
    {
        await using StoreContext context = database.Context();

        return await new ReadVolumeStore(new DataConnections(context))
            .RebaselineAsync(Today, days, TestContext.Current.CancellationToken);
    }
}
