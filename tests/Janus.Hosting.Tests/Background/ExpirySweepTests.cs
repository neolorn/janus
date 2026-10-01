using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Callbacks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Hosting.Background;
using Janus.Hosting.Tests.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Background;

/// <summary>
/// The expiry sweep as a deployment registers it, run over the database
/// (PRIV-RET-005, OPS-OBS-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class ExpirySweepTests(HostFixture host) : IClassFixture<HostFixture>
{
    /// <summary>
    /// PRIV-RET-005 AC2: a send counter whose newest time decides nothing under the
    /// restrictions now declared is gone after a pass of the sweep, with no other send
    /// to the key it names; the destination's record and a source's record alike.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RET_005_AC2_ARecordIsGoneWithoutAnotherSendAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // A time no other case writes, so the rows this case counted are found by it.
        DateTimeOffset sent = Authorization.Deployment.Noon.AddSeconds(17);

        await using (ServiceProvider seeding = BackgroundJobsTests.Deployed(host, sent))
        {
            await using AsyncServiceScope scope = seeding.CreateAsyncScope();
            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await work.BeginAsync(cancellationToken);
            await scope.ServiceProvider.GetRequiredService<ISendLedger>().RecordAsync(
                RandomNumberGenerator.GetBytes(32),
                [
                    new SendCount(
                        new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, "+201001234570"),
                        TimeSpan.FromDays(1)),
                    new SendCount(
                        new RestrictionKey("sms.source", RestrictionKeyKind.Source, "198.51.100.17"),
                        TimeSpan.FromDays(1)),
                ],
                [],
                sent,
                cancellationToken);
            await work.CommitAsync(cancellationToken);
        }

        Assert.Equal((1, 1), await CountedAsync(sent));

        await using ServiceProvider services = BackgroundJobsTests.Deployed(host, sent.AddDays(40));
        await using AsyncServiceScope sweeping = services.CreateAsyncScope();

        BackgroundJob sweep = BackgroundJobs.All.Single(job => job.Name == "expiry-sweep");

        Result ran = await sweep.RunAsync(sweeping.ServiceProvider, cancellationToken);

        Assert.True(ran.Match(() => true, _ => false));

        Assert.Equal((0, 0), await CountedAsync(sent));
    }

    /// <summary>
    /// OPS-SEC-003 AC6 (D-166, 318): a pass of the sweep removes each abuse count its own
    /// check no longer reads, a throttle counter decayed to nothing, a settled send, and a
    /// registration start, a notice and a callback outside their windows, and keeps each
    /// one that still counts.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_SEC_003_AC6_ThePassRemovesEveryAbuseCountItsCheckNoLongerReadsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Times no other case writes, so the rows this case counted are found by it.
        DateTimeOffset now = Authorization.Deployment.Noon.AddDays(60).AddSeconds(23);
        DateTimeOffset stale = now.AddDays(-3);

        // The standing lines first, since a ledger forgets what is older than its window
        // as it writes, and the stale lines are older than these.
        await CountAsync(now, "198.51.100.31", cancellationToken);
        await CountAsync(stale, "198.51.100.32", cancellationToken);

        int[] counted = [.. await AbuseCountsAsync(now), .. await AbuseCountsAsync(stale)];

        Assert.Equal([1, 1, 1, 1, 1, 1, 1, 1, 1, 1], counted);

        await using ServiceProvider services = BackgroundJobsTests.Deployed(host, now);
        await using AsyncServiceScope sweeping = services.CreateAsyncScope();

        Result ran = await BackgroundJobs.All.Single(job => job.Name == "expiry-sweep")
            .RunAsync(sweeping.ServiceProvider, cancellationToken);

        int[] swept = [.. await AbuseCountsAsync(now), .. await AbuseCountsAsync(stale)];

        Assert.True(ran.Match(() => true, _ => false));
        Assert.Equal([1, 1, 1, 1, 1, 0, 0, 0, 0, 0], swept);
    }

    // One line of each abuse count at an instant, each against a key of its own.
    private async Task CountAsync(DateTimeOffset at, string source, CancellationToken cancellationToken)
    {
        await using ServiceProvider seeding = BackgroundJobsTests.Deployed(host, at);
        await using AsyncServiceScope scope = seeding.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);
        await services.GetRequiredService<IThrottleLedger>()
            .FailedAsync(ThrottleScope.Source, source, standing: 0, at, cancellationToken);
        await services.GetRequiredService<ISendLedger>().RecordAsync(
            RandomNumberGenerator.GetBytes(32),
            [new SendCount(new RestrictionKey("sms.source", RestrictionKeyKind.Source, source), TimeSpan.FromHours(1))],
            [],
            at,
            cancellationToken);
        await services.GetRequiredService<IRegistrationSources>().RecordAsync(source, at, cancellationToken);
        _ = await services.GetRequiredService<INoticeLedger>()
            .FirstAsync(source + "@example.test", at, TimeSpan.FromHours(1), cancellationToken);
        _ = await services.GetRequiredService<ICallbackLedger>()
            .ReceivedAsync(source, at, TimeSpan.FromMinutes(1), cancellationToken);
        await work.CommitAsync(cancellationToken);
    }

    // The throttle counters, sends, registration starts, notices and callbacks at an
    // instant.
    private async Task<int[]> AbuseCountsAsync(DateTimeOffset at)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return
        [
            await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM identity.throttle_counters WHERE at = @at", new { at }),
            await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM identity.sends WHERE sent_at = @at", new { at }),
            await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM identity.registration_sources WHERE at = @at", new { at }),
            await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM identity.nonexistence_notices WHERE at = @at", new { at }),
            await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM identity.callbacks WHERE at = @at", new { at }),
        ];
    }

    // The destination records and the other keys' records holding a time.
    private async Task<(int Destinations, int Keys)> CountedAsync(DateTimeOffset sent)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return (
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM identity.send_counters WHERE @sent = ANY(sent_at)",
                new { sent }),
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM identity.send_key_counters WHERE @sent = ANY(sent_at)",
                new { sent }));
    }
}
