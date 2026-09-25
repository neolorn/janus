using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Bootstrap;
using Janus.Authentication.Registration;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Background;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Authorization;
using Janus.Storage.Tests;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Background;

/// <summary>
/// The automated restore test as the worker runs it: a backup of the running instance
/// restored into a throwaway container, the canary proved there with the keys the
/// deployment runs on, and every run recorded and anything short of a pass raised
/// (DR-007, DR-008).
/// </summary>
/// <remarks>
/// The cases share one running database, so each seeds a canary of its own and runs at
/// an instant of its own, which is what its record and its alert are read back by.
/// </remarks>
[Trait("kind", "integration")]
public sealed class RestoreTestTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const string Job = "restore-test";

    private static readonly DateTimeOffset Noon = Authorization.Deployment.Noon;

    private static readonly KeyEncryptionKeys Live = KeyEncryptionKeysOf(0x01);

    private static readonly FingerprintKeys Fingerprinted = FingerprintKeysOf(0x02);

    /// <summary>
    /// DR-007 AC1: the test is one of the worker's jobs, run as a principal that may
    /// monitor with DR-007 as its reason, at <c>backup.restoretest.interval</c>, so no
    /// person starts it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_007_AC1_TheTestRunsAtItsIntervalWithoutAPersonAsync()
    {
        BackgroundJob job = BackgroundJobs.All.Single(candidate => candidate.Name == Job);

        await using ServiceProvider services = Deployed(Noon, instance: null, Live, Fingerprinted);
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        Result<TimeSpan> interval = await job.IntervalAsync(
            scope.ServiceProvider.GetRequiredService<IConfigurationStore>(),
            TestContext.Current.CancellationToken);

        Assert.Equal(("DR-007", true), (job.Principal.Reason, job.Principal.MayRun(SystemOperation.Monitoring)));
        Assert.Equal(TimeSpan.FromDays(93), interval.Match(value => value, _ => TimeSpan.Zero));
    }

    /// <summary>
    /// DR-007 AC4: a backup of the running instance, restored into a container of its
    /// own, yields the canary's field decrypted under the live key-encryption key and its
    /// account found by its verified email under the live fingerprint key; the run is
    /// recorded as passed within the objective and nothing is raised.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_007_AC4_TheRestoredCanaryDecryptsAndItsAccountIsFoundAsync()
    {
        DateTimeOffset at = Noon.AddDays(1);

        _ = await CanaryAsync(at);

        await using var restore = new ContainerRestore(await host.BackupAsync(), Database());

        Assert.Equal(Result.Success(), await RunAsync(at, restore, Live, Fingerprinted));

        JsonElement recorded = await RecordedAsync(at);

        Assert.Equal(
            ("passed", 28800d, false),
            (recorded.GetProperty("outcome").GetString(),
                recorded.GetProperty("objectiveSeconds").GetDouble(),
                recorded.GetProperty("outlived").GetBoolean()));
        Assert.InRange(recorded.GetProperty("elapsedSeconds").GetDouble(), double.Epsilon, 28800d);
        Assert.Empty(await RaisedAsync(at));
    }

    /// <summary>
    /// DR-007 AC2, AC3: the time a run takes is recorded against the objective, and a
    /// restore still running when the objective passes is abandoned, recorded as an
    /// overrun with the time it had taken, and raised with the same.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_007_AC2_TheMeasuredTimeIsRecordedAgainstTheObjectiveAsync()
    {
        DateTimeOffset at = Noon.AddDays(2);
        var stalled = new StalledRestore();

        _ = await CanaryAsync(at);

        await using (NpgsqlConnection connection = await host.OpenAsync())
        {
            await ConfiguredAsync(connection, Settings.BackupRestoreTestObjective.Key, "PT1S");
        }

        try
        {
            Assert.Equal(Result.Success(), await RunAsync(at, stalled, Live, Fingerprinted));
        }
        finally
        {
            await using NpgsqlConnection connection = await host.OpenAsync();

            await connection.ExecuteAsync(
                "DELETE FROM identity.settings WHERE key = @Key",
                new { Key = Settings.BackupRestoreTestObjective.Key.ToString() });
        }

        JsonElement recorded = await RecordedAsync(at);

        Assert.Equal(
            ("overrun", 1d, false, 1),
            (recorded.GetProperty("outcome").GetString(),
                recorded.GetProperty("objectiveSeconds").GetDouble(),
                recorded.GetProperty("outlived").GetBoolean(),
                stalled.TornDown));
        Assert.True(recorded.GetProperty("elapsedSeconds").GetDouble() >= 1d);
        Assert.Equal([recorded.GetRawText()], (await RaisedAsync(at)).Select(raised => raised.GetRawText()));
    }

    /// <summary>
    /// DR-007 AC3: a backup the key-encryption key the deployment runs on cannot open is
    /// a failed test, recorded and raised as undecrypted.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_007_AC3_ABackupTheLiveKeyCannotOpenIsRaisedAsync()
    {
        DateTimeOffset at = Noon.AddDays(3);

        _ = await CanaryAsync(at);

        await using var restore = new ContainerRestore(await host.BackupAsync(), Database());

        Assert.Equal(Result.Success(), await RunAsync(at, restore, KeyEncryptionKeysOf(0x03), Fingerprinted));

        Assert.Equal("undecrypted", (await RecordedAsync(at)).GetProperty("outcome").GetString());
        Assert.Equal(
            ["undecrypted"],
            (await RaisedAsync(at)).Select(raised => raised.GetProperty("outcome").GetString()));
    }

    /// <summary>
    /// DR-007 AC3: a backup whose fields open but whose identifiers the fingerprint key
    /// the deployment runs on cannot find, so nobody could sign in, is a failed test,
    /// recorded and raised as unresolved.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_007_AC3_ABackupWhoseAccountsTheLiveFingerprintKeyCannotFindIsRaisedAsync()
    {
        DateTimeOffset at = Noon.AddDays(4);

        _ = await CanaryAsync(at);

        await using var restore = new ContainerRestore(await host.BackupAsync(), Database());

        Assert.Equal(Result.Success(), await RunAsync(at, restore, Live, FingerprintKeysOf(0x04)));

        Assert.Equal("unresolved", (await RecordedAsync(at)).GetProperty("outcome").GetString());
        Assert.Equal(
            ["unresolved"],
            (await RaisedAsync(at)).Select(raised => raised.GetProperty("outcome").GetString()));
    }

    /// <summary>
    /// DR-007 AC3: a deployment that registered nothing to restore with, or whose restore
    /// failed, has not tested its backup, and every run is recorded and raised as
    /// unrestored.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_007_AC3_ARunThatRestoresNothingIsRaisedAsync()
    {
        DateTimeOffset unregistered = Noon.AddDays(5);
        DateTimeOffset refused = Noon.AddDays(6);

        Assert.Equal(Result.Success(), await RunAsync(unregistered, instance: null, Live, Fingerprinted));
        Assert.Equal(Result.Success(), await RunAsync(refused, new RefusedRestore(throws: false), Live, Fingerprinted));

        foreach (DateTimeOffset at in new[] { unregistered, refused })
        {
            Assert.Equal("unrestored", (await RecordedAsync(at)).GetProperty("outcome").GetString());
            Assert.Equal(
                ["unrestored"],
                (await RaisedAsync(at)).Select(raised => raised.GetProperty("outcome").GetString()));
        }
    }

    /// <summary>
    /// DR-008 AC1: the test reads the restored instance and never the running database:
    /// a canary whose field the running database no longer holds still passes from the
    /// backup taken before, and what the running database holds is as it was.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_008_AC1_TheTestReadsTheRestoredInstanceAndLeavesTheRunningOneAsync()
    {
        DateTimeOffset at = Noon.AddDays(7);

        SubjectId canary = await CanaryAsync(at);

        await using var restore = new ContainerRestore(await host.BackupAsync(), Database());
        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync(
            "DELETE FROM identity.profiles WHERE subject = @subject",
            new { subject = canary.Value });

        string before = await HeldAsync(connection);

        Assert.Equal(Result.Success(), await RunAsync(at, restore, Live, Fingerprinted));

        Assert.Equal("passed", (await RecordedAsync(at)).GetProperty("outcome").GetString());
        Assert.Equal(before, await HeldAsync(connection));
        Assert.NotEqual(
            new NpgsqlConnectionStringBuilder(host.ConnectionString).Port,
            new NpgsqlConnectionStringBuilder(restore.Restored).Port);
    }

    /// <summary>
    /// DR-008 AC2: the instance is torn down once the test is over, so it cannot be
    /// reached afterwards; and a teardown that fails or throws is an instance that may
    /// have outlived its test, which is recorded and raised.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_008_AC2_TheInstanceDoesNotOutliveTheTestAsync()
    {
        DateTimeOffset at = Noon.AddDays(8);
        DateTimeOffset failed = Noon.AddDays(9);
        DateTimeOffset thrown = Noon.AddDays(10);
        var failing = new RefusedRestore(throws: false);
        var throwing = new RefusedRestore(throws: true);

        _ = await CanaryAsync(at);

        await using var restore = new ContainerRestore(await host.BackupAsync(), Database());

        Assert.Equal(Result.Success(), await RunAsync(at, restore, Live, Fingerprinted));
        Assert.Equal(Result.Success(), await RunAsync(failed, failing, Live, Fingerprinted));
        Assert.Equal(Result.Success(), await RunAsync(thrown, throwing, Live, Fingerprinted));

        Assert.Equal((1, 1, 1), (restore.TornDown, failing.TornDown, throwing.TornDown));
        Assert.False((await RecordedAsync(at)).GetProperty("outlived").GetBoolean());
        await Assert.ThrowsAnyAsync<NpgsqlException>(async () =>
        {
            await using var reached = new NpgsqlConnection(restore.Restored);
            await reached.OpenAsync(TestContext.Current.CancellationToken);
        });

        foreach (DateTimeOffset outlived in new[] { failed, thrown })
        {
            Assert.True((await RecordedAsync(outlived)).GetProperty("outlived").GetBoolean());
            Assert.Equal(
                [true],
                (await RaisedAsync(outlived)).Select(raised => raised.GetProperty("outlived").GetBoolean()));
        }
    }

    private static KeyEncryptionKeys KeyEncryptionKeysOf(byte fill) =>
        new(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = Enumerable.Repeat(fill, 32).ToArray() });

    private static FingerprintKeys FingerprintKeysOf(byte fill) =>
        new(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = Enumerable.Repeat(fill, 32).ToArray() });

    private static async Task ConfiguredAsync(NpgsqlConnection connection, ConfigurationKey key, string value) =>
        await connection.ExecuteAsync(
            """
            INSERT INTO identity.settings (key, value) VALUES (@Key, @Value)
            ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value
            """,
            new { Key = key.ToString(), Value = value });

    // What the test reads from, as the running database holds it: every account, every
    // wrapped key, every profile and every identifier.
    private static async Task<string> HeldAsync(NpgsqlConnection connection) =>
        await connection.ExecuteScalarAsync<string>(
            """
            SELECT concat_ws(
                ':',
                (SELECT md5(coalesce(string_agg(row_to_json(held)::text, ',' ORDER BY row_to_json(held)::text), '')) FROM identity.accounts held),
                (SELECT md5(coalesce(string_agg(row_to_json(held)::text, ',' ORDER BY row_to_json(held)::text), '')) FROM identity.subject_keys held),
                (SELECT md5(coalesce(string_agg(row_to_json(held)::text, ',' ORDER BY row_to_json(held)::text), '')) FROM identity.profiles held),
                (SELECT md5(coalesce(string_agg(row_to_json(held)::text, ',' ORDER BY row_to_json(held)::text), '')) FROM identity.identifiers held))
            """) ?? string.Empty;

    // A canary as bootstrap seeds one, under an address of its own, since the cases share
    // the running database; the setting is pointed at it.
    private async Task<SubjectId> CanaryAsync(DateTimeOffset at)
    {
        var canary = new SubjectId(Guid.NewGuid());
        string address = $"canary-{canary.Value:N}@restore-test.invalid";

        Assert.True(EmailAddress.TryParse(address, out EmailAddress parsed));
        Assert.True(DisplayName.TryParse("Restore canary", out DisplayName name));

        await using (ServiceProvider services = Deployed(at, instance: null, Live, Fingerprinted))
        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDeploymentSeed>().CreateAccountAsync(
                canary,
                [new NewIdentifier(IdentifierId.New(new FixedTime(at)), IdentifierKind.Email, address, parsed.Value, Locked: false, at)],
                name,
                adultAffirmed: null,
                dateOfBirth: null,
                group: null,
                at,
                TestContext.Current.CancellationToken);
        }

        await using NpgsqlConnection connection = await host.OpenAsync();

        await ConfiguredAsync(
            connection,
            Settings.BackupRestoreTestCanary.Key,
            Settings.BackupRestoreTestCanary.Write(canary.ToString()));

        return canary;
    }

    private string Database() => new NpgsqlConnectionStringBuilder(host.ConnectionString).Database
        ?? throw new InvalidOperationException("The fixture names no database.");

    // A deployment over the fixture's database at the case's own instant, with what a
    // host declares for itself and, where the case has one, what restores its backups.
    private ServiceProvider Deployed(
        DateTimeOffset at,
        IRestoreTestInstance? instance,
        KeyEncryptionKeys keys,
        FingerprintKeys fingerprints)
    {
        IServiceCollection services = new ServiceCollection()
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
                keys,
                fingerprints,
                Encoding.UTF8.GetBytes("the secret this application presents"),
                Encoding.UTF8.GetBytes(host.MaintenanceConnectionString),
                HostFixture.Declaration(),
                ApplicationKind.Public);

        if (instance is not null)
        {
            services.AddSingleton(instance);
        }

        return services.BuildServiceProvider();
    }

    // What the run's record carries.
    private async Task<JsonElement> RecordedAsync(DateTimeOffset at)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        string details = await connection.QuerySingleAsync<string>(
            """
            SELECT details::text FROM identity.audit_records
            WHERE action = 'ops.restoretest.completed' AND occurred_at = @at
            """,
            new { at });

        return JsonDocument.Parse(details).RootElement.Clone();
    }

    // What each alert the run raised carries.
    private async Task<IReadOnlyList<JsonElement>> RaisedAsync(DateTimeOffset at)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        IEnumerable<string> raised = await connection.QueryAsync<string>(
            """
            SELECT details::text FROM identity.raised_alerts
            WHERE condition = 'restore-test-failed' AND raised_at = @at
            """,
            new { at });

        return [.. raised.Select(details => JsonDocument.Parse(details).RootElement.Clone())];
    }

    // One run of the job as the worker takes it.
    private async Task<Result> RunAsync(
        DateTimeOffset at,
        IRestoreTestInstance? instance,
        KeyEncryptionKeys keys,
        FingerprintKeys fingerprints)
    {
        await using ServiceProvider services = Deployed(at, instance, keys, fingerprints);
        await using AsyncServiceScope scope = services.CreateAsyncScope();

        return await BackgroundJobs.All
            .Single(job => job.Name == Job)
            .RunAsync(scope.ServiceProvider, TestContext.Current.CancellationToken);
    }
}
