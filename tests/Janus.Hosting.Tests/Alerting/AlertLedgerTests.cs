using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Hosting.Tests.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Alerting;

/// <summary>
/// The deduplication ledger over the library's own database with two connections: a
/// key is claimed by one conditional statement, so two overlapping passes deliver one
/// alert (OPS-ALERT-002, X3 of D-166, 290).
/// </summary>
[Trait("kind", "integration")]
public sealed class AlertLedgerTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    // How long the case waits for the second pass to reach the first's row before it
    // fails rather than hangs.
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    /// <summary>
    /// OPS-ALERT-002: two passes that overlap on a key never raised before claim it
    /// once between them; the second waits for the first, then finds the key taken, and
    /// neither faults.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_002_TwoOverlappingPassesDeliverOneAlertAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string key = "background-job-failed:" + Guid.NewGuid().ToString("N");

        await using AsyncServiceScope first = host.Services.CreateAsyncScope();
        await using AsyncServiceScope second = host.Services.CreateAsyncScope();

        IUnitOfWork holding = first.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await holding.BeginAsync(cancellationToken);

        bool claimed = await first.ServiceProvider.GetRequiredService<IAlertLedger>()
            .FirstAsync(key, Noon, Window, cancellationToken);

        Task<bool> overlapping = OverlappingAsync(second.ServiceProvider, key, cancellationToken);

        await WaitingOrDoneAsync(overlapping, cancellationToken);

        await holding.CommitAsync(cancellationToken);

        bool claimedAgain = await overlapping.WaitAsync(Bound, cancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        Assert.True(claimed);
        Assert.False(claimedAgain);
        Assert.Equal(
            1,
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(*)::int FROM identity.alerts WHERE key = @Key",
                new { Key = key }));
    }

    /// <summary>
    /// OPS-ALERT-002: once the window a claim stands for has passed, the key is claimed
    /// again, and within it not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_002_AKeyIsClaimedAgainOnlyAfterItsWindowAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string key = "background-job-failed:" + Guid.NewGuid().ToString("N");

        Assert.True(await ClaimedAsync(key, Noon, cancellationToken));
        Assert.False(await ClaimedAsync(key, Noon + Window - TimeSpan.FromSeconds(1), cancellationToken));
        Assert.True(await ClaimedAsync(key, Noon + Window, cancellationToken));
    }

    private static async Task<bool> OverlappingAsync(
        IServiceProvider services,
        string key,
        CancellationToken cancellationToken)
    {
        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);

        bool claimed = await services.GetRequiredService<IAlertLedger>()
            .FirstAsync(key, Noon, Window, cancellationToken);

        await work.CommitAsync(cancellationToken);

        return claimed;
    }

    private async Task<bool> ClaimedAsync(string key, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);

        bool claimed = await scope.ServiceProvider.GetRequiredService<IAlertLedger>()
            .FirstAsync(key, at, Window, cancellationToken);

        await work.CommitAsync(cancellationToken);

        return claimed;
    }

    // The second pass is waiting on the row the first holds, as the database itself
    // reports it, or has already answered without waiting; either way the case can
    // commit the first.
    private async Task WaitingOrDoneAsync(Task overlapping, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(Bound);

        while (!overlapping.IsCompleted
               && await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                   "SELECT count(*) FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE '%identity.alerts%'",
                   cancellationToken: bounded.Token)) == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), bounded.Token);
        }
    }
}
