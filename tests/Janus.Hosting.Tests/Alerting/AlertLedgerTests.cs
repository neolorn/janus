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
    /// OPS-ALERT-002 AC2: two passes that overlap on a key never raised before, each with
    /// a raise of the condition, claim it once between them; the second waits for the
    /// first, then finds the key taken, and neither faults.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_002_AC2_TwoOverlappingPassesDeliverOneAlertAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string key = "background-job-failed:" + Guid.NewGuid().ToString("N");

        await using AsyncServiceScope first = host.Services.CreateAsyncScope();
        await using AsyncServiceScope second = host.Services.CreateAsyncScope();

        IUnitOfWork holding = first.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await holding.BeginAsync(cancellationToken);

        bool claimed = await first.ServiceProvider.GetRequiredService<IAlertLedger>()
            .FirstAsync(key, RaisedAlertId.Of(Noon), Noon, Window, cancellationToken);

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

        Assert.True(await ClaimedAsync(key, RaisedAlertId.Of(Noon), Noon, cancellationToken));
        Assert.False(await ClaimedAsync(
            key,
            RaisedAlertId.Of(Noon),
            Noon + Window - TimeSpan.FromSeconds(1),
            cancellationToken));
        Assert.True(await ClaimedAsync(key, RaisedAlertId.Of(Noon), Noon + Window, cancellationToken));
    }

    /// <summary>
    /// OPS-ALERT-003 AC2 (D-225): a claim its own raise made and no handler answered does
    /// not count against that raise, which claims the key again, while it still folds
    /// another raise; once the handler takes it, it folds its own raise too, and giving
    /// it back then changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_003_AC2_ARaiseIsNotFoldedIntoItsOwnUnansweredClaimAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string key = "breakglass-used:" + Guid.NewGuid().ToString("N");
        var raise = RaisedAlertId.Of(Noon);

        Assert.True(await ClaimedAsync(key, raise, Noon, cancellationToken));
        Assert.True(await ClaimedAsync(key, raise, Noon, cancellationToken));
        Assert.False(await ClaimedAsync(key, RaisedAlertId.Of(Noon), Noon, cancellationToken));

        Assert.True(await WrittenAsync(ledger => ledger.TakenAsync(key, raise, cancellationToken), cancellationToken));

        Assert.False(await ClaimedAsync(key, raise, Noon, cancellationToken));
        Assert.False(await WrittenAsync(ledger => ledger.ForgetAsync(key, raise, cancellationToken), cancellationToken));
        Assert.False(await ClaimedAsync(key, raise, Noon, cancellationToken));
    }

    /// <summary>
    /// OPS-ALERT-003 AC2 (D-225): a claim given back by the raise that made it leaves the
    /// key to the next raise inside the window, and a raise gives back no claim but its
    /// own.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_003_AC2_AClaimIsGivenBackOnlyByItsOwnRaiseAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string key = "breakglass-used:" + Guid.NewGuid().ToString("N");
        var raise = RaisedAlertId.Of(Noon);

        Assert.True(await ClaimedAsync(key, raise, Noon, cancellationToken));
        Assert.False(await WrittenAsync(
            ledger => ledger.ForgetAsync(key, RaisedAlertId.Of(Noon), cancellationToken),
            cancellationToken));
        Assert.True(await WrittenAsync(ledger => ledger.ForgetAsync(key, raise, cancellationToken), cancellationToken));
        Assert.True(await ClaimedAsync(key, RaisedAlertId.Of(Noon), Noon, cancellationToken));
    }

    private static async Task<bool> OverlappingAsync(
        IServiceProvider services,
        string key,
        CancellationToken cancellationToken)
    {
        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);

        bool claimed = await services.GetRequiredService<IAlertLedger>()
            .FirstAsync(key, RaisedAlertId.Of(Noon), Noon, Window, cancellationToken);

        await work.CommitAsync(cancellationToken);

        return claimed;
    }

    private Task<bool> ClaimedAsync(
        string key,
        RaisedAlertId raise,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        WrittenAsync(ledger => ledger.FirstAsync(key, raise, at, Window, cancellationToken), cancellationToken);

    // One write to the ledger, in a unit of work of its own.
    private async Task<bool> WrittenAsync(Func<IAlertLedger, ValueTask<bool>> written, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);

        bool done = await written(scope.ServiceProvider.GetRequiredService<IAlertLedger>());

        await work.CommitAsync(cancellationToken);

        return done;
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
