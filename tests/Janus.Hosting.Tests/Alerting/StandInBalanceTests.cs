using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Hosting.Background;
using Janus.Hosting.Bff;
using Janus.Hosting.Sessions;
using Janus.Hosting.Tests.Authorization;
using Janus.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Alerting;

/// <summary>
/// The testing package's SMS transport in a deployment over the database, its balance
/// read by the library's own balance job (LIB-TEST-003, INT-SMS-004).
/// </summary>
/// <param name="host">The database.</param>
[Trait("kind", "integration")]
public sealed class StandInBalanceTests(HostFixture host) : IClassFixture<HostFixture>
{
    /// <summary>
    /// LIB-TEST-003 AC4 (D-208): in a deployment composed with the in-memory SMS
    /// transport, each pass of the worker records the balance the transport reports, and
    /// a pass past twice the poll interval raises no <c>background-job-failed</c> for the
    /// balance job, whose run succeeded each time.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_AC4_TheBalanceJobRecordsTheTransportsBalanceAndRaisesNoFailureAsync()
    {
        DateTimeOffset first = Authorization.Deployment.Noon;
        DateTimeOffset second = first.AddDays(1);

        await PassAsync(first);
        await PassAsync(second);

        await using NpgsqlConnection connection = await host.OpenAsync();

        IEnumerable<decimal> recorded = await connection.QueryAsync<decimal>(
            "SELECT balance FROM identity.sms_balance_readings ORDER BY read_at");
        DateTime? succeeded = await connection.ExecuteScalarAsync<DateTime?>(
            "SELECT succeeded_at FROM identity.background_jobs WHERE name = 'sms-balance'");
        int lapses = await connection.ExecuteScalarAsync<int>(
            """
            SELECT
                (SELECT count(*) FROM identity.raised_alerts
                 WHERE idempotency_key LIKE 'background-job-failed:sms-balance@%')::int
                + (SELECT count(*) FROM identity.alerts
                   WHERE key = 'background-job-failed:sms-balance')::int
            """);

        Assert.Equal([99999999999999.9999m, 99999999999999.9999m], recorded);
        Assert.Equal(second.UtcDateTime, succeeded);
        Assert.Equal(0, lapses);
    }

    // One pass of the worker of a deployment at one instant, settled before it is
    // disposed.
    private async Task PassAsync(DateTimeOffset now)
    {
        await using ServiceProvider services = Deployed(now);

        BackgroundWorker worker = services.GetServices<IHostedService>().OfType<BackgroundWorker>().Single();

        _ = await worker.RunDueAsync(TestContext.Current.CancellationToken);

        await worker.SettledAsync(TestContext.Current.CancellationToken);
    }

    // A deployment over the fixture's database at one instant, with what a host declares
    // for itself, its text transport the testing package's.
    private ServiceProvider Deployed(DateTimeOffset now)
    {
        ServiceProvider deployed = new ServiceCollection()
            .AddSingleton<ISmsTransport>(new InMemorySmsTransport())
            .AddSingleton<IAlertHandler>(new Janus.Testing.InMemoryAlerts())
            .AddSingleton<TimeProvider>(new FixedTime(now))
            .AddSingleton<IMailTransport>(new InMemoryMailTransport())
            .AddSingleton(new AuthenticationAddresses(
                "https://accounts.example.test/signin",
                "https://accounts.example.test"))
            .AddSingleton(Landing.Origins)
            .AddSingleton(new SignOnClient("this-application"))
            .AddSingleton<ISecretSource>(HostFixture.Secrets(host.MaintenanceConnectionString))
            .AddJanus(host.ConnectionString, HostFixture.Declaration(), ApplicationKind.Public)
            .BuildServiceProvider();

        return HostFixture.Started(deployed);
    }
}
