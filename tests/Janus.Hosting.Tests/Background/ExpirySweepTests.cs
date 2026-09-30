using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
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
