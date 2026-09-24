using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Hosting.Background;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Background;

/// <summary>
/// The library's scheduled work as a deployment registers it through its one entry
/// point, run over the database (INF-BG-001, OPS-OBS-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class BackgroundJobsTests(HostFixture host) : IClassFixture<HostFixture>
{
    /// <summary>
    /// INF-BG-001 AC1, OPS-OBS-003 AC1: the worker the entry point registers runs every
    /// job with nobody asking, and each one's success is on record, so no cleanup is
    /// left to a person.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_BG_001_AC1_EveryJobRunsWithoutAPersonAsync()
    {
        await using ServiceProvider services = Deployed();

        BackgroundWorker worker = services.GetServices<IHostedService>().OfType<BackgroundWorker>().Single();

        _ = await worker.RunDueAsync(TestContext.Current.CancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        IEnumerable<string> succeeded = await connection.QueryAsync<string>(
            "SELECT name FROM identity.background_jobs WHERE succeeded_at IS NOT NULL ORDER BY name COLLATE \"C\"");

        Assert.Equal(
            BackgroundJobs.All.Select(job => job.Name).Order(StringComparer.Ordinal),
            succeeded);
    }

    // A deployment over the fixture's database, with what a host declares for itself:
    // where the events go, the two transports, its sign-in screen and its client.
    private ServiceProvider Deployed() =>
        new ServiceCollection()
            .AddSingleton<TimeProvider>(new FixedTime(Authorization.Deployment.Noon))
            .AddSingleton<IEvents>(new EventsInMemory())
            .AddSingleton<IMailTransport>(new MailTransportInMemory())
            .AddSingleton<ISmsTransport>(new SmsTransportInMemory())
            .AddSingleton(new AuthenticationAddresses(
                "https://accounts.example.test/signin",
                "https://accounts.example.test"))
            .AddSingleton(new SignOnClient("this-application"))
            .AddJanus(
                host.ConnectionString,
                new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
                new byte[32],
                Encoding.UTF8.GetBytes("the secret this application presents"),
                HostFixture.Declaration(),
                ApplicationKind.Public)
            .BuildServiceProvider();
}
