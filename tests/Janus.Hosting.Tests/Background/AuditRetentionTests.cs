using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Background;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Background;

/// <summary>
/// The audit trail's retention as the worker runs it: the months ahead created, the
/// expired partitions dropped under the maintenance credential and nothing else, and
/// each run recorded as the job's own action (PRIV-RET-002, OPS-MIG-003a, INF-BG-002).
/// </summary>
/// <remarks>
/// The cases share one running database, so each drops a partition of its own and runs
/// at an instant of its own, which is what its record is read back by.
/// </remarks>
[Trait("kind", "integration")]
public sealed class AuditRetentionTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const string Job = "audit-partitions";

    private static readonly DateTimeOffset Noon = Authorization.Deployment.Noon;

    /// <summary>
    /// PRIV-RET-002 AC3: the job is one of the worker's, run daily as a principal that may
    /// purge what is past retention with PRIV-RET-002 as its reason, so no person starts
    /// it; a run drops the partition past its category's retention, and records how many
    /// it dropped and the retentions it held them to.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RET_002_AC3_ExpiredPartitionsAreDroppedOnScheduleWithoutAPersonAsync()
    {
        DateTimeOffset at = Noon.AddDays(1);
        BackgroundJob job = BackgroundJobs.All.Single(candidate => candidate.Name == Job);

        await using NpgsqlConnection connection = await host.OpenAsync();
        await ExpiredAsync(connection, "2019_05");

        await using ServiceProvider services = Deployed(at, host.MaintenanceConnectionString);
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        Result<TimeSpan> interval = await job.IntervalAsync(
            scope.ServiceProvider.GetRequiredService<IConfigurationStore>(),
            TestContext.Current.CancellationToken);

        Result ran = await job.RunAsync(scope.ServiceProvider, TestContext.Current.CancellationToken);

        Assert.Equal(("PRIV-RET-002", true), (job.Principal.Reason, job.Principal.MayRun(SystemOperation.RetentionPurge)));
        Assert.Equal(TimeSpan.FromDays(1), interval.Match(value => value, _ => TimeSpan.Zero));
        Assert.True(ran.Match(() => true, _ => false));
        Assert.False(await StandingAsync(connection, "2019_05"));

        JsonElement recorded = Assert.Single(await RecordedAsync(connection, at));

        Assert.True(recorded.GetProperty("dropped").GetInt32() >= 1);
        Assert.Equal(
            Settings.RetentionAuditSecurity.Default.TotalDays,
            recorded.GetProperty("securityRetentionDays").GetDouble());
        Assert.Equal(
            Settings.RetentionAuditRoutine.Default.TotalDays,
            recorded.GetProperty("routineRetentionDays").GetDouble());
    }

    /// <summary>
    /// PRIV-RET-002 AC5, OPS-MIG-003a: a deployment that hands in any credential but the
    /// maintenance one, here a superuser, which holds the application's rights as it holds
    /// every role's, has its run refused before either function is asked, and nothing is
    /// dropped or recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RET_002_AC5_ARunUnderAnotherCredentialDropsNothingAsync()
    {
        DateTimeOffset at = Noon.AddDays(2);

        await using NpgsqlConnection connection = await host.OpenAsync();
        await ExpiredAsync(connection, "2019_06");

        await using ServiceProvider services = Deployed(at, host.ConnectionString);
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        Result ran = await BackgroundJobs.All
            .Single(job => job.Name == Job)
            .RunAsync(scope.ServiceProvider, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.Denied, ran.Match(() => (ErrorCode?)null, error => error.Code));
        Assert.True(await StandingAsync(connection, "2019_06"));
        Assert.Empty(await RecordedAsync(connection, at));

        await connection.ExecuteAsync("DROP TABLE identity.audit_records_routine_2019_06");
    }

    private static async Task ExpiredAsync(NpgsqlConnection connection, string month) =>
        await connection.ExecuteAsync(
            $"""
            CREATE TABLE identity.audit_records_routine_{month}
                PARTITION OF identity.audit_records_routine
                FOR VALUES FROM ('{month.Replace('_', '-')}-01 00:00:00+00')
                TO (('{month.Replace('_', '-')}-01 00:00:00+00'::timestamptz) + interval '1 month');
            """);

    private static async Task<bool> StandingAsync(NpgsqlConnection connection, string month) =>
        await connection.ExecuteScalarAsync<bool>(
            "SELECT to_regclass('identity.audit_records_routine_' || @month) IS NOT NULL",
            new { month });

    // What each run at the instant recorded.
    private static async Task<IReadOnlyList<JsonElement>> RecordedAsync(NpgsqlConnection connection, DateTimeOffset at)
    {
        IEnumerable<string> recorded = await connection.QueryAsync<string>(
            """
            SELECT details::text FROM identity.audit_records
            WHERE action = 'ops.auditpartitions.maintained' AND occurred_at = @at
            """,
            new { at });

        return [.. recorded.Select(details => JsonDocument.Parse(details).RootElement.Clone())];
    }

    // A deployment over the fixture's database at the case's own instant, handed the
    // maintenance credential the case names.
    private ServiceProvider Deployed(DateTimeOffset at, string maintenance) =>
        new ServiceCollection()
            .AddSingleton<TimeProvider>(new FixedTime(at))
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
                new FingerprintKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
                Encoding.UTF8.GetBytes("the secret this application presents"),
                Encoding.UTF8.GetBytes(maintenance),
                HostFixture.Declaration(),
                ApplicationKind.Public)
            .BuildServiceProvider();
}
