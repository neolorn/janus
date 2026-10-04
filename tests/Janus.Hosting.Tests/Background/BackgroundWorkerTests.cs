using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Background;
using Janus.Authentication.Tests;
using Janus.Authentication.Tests.Alerting;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Alerting;
using Janus.Hosting.Background;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Janus.Hosting.Tests.Background;

/// <summary>
/// The worker running the scheduled work: each job at its interval with nobody asking,
/// once across the processes of a deployment, as its principal, and a job that stopped
/// succeeding raised rather than passing unnoticed (INF-BG-001, INF-BG-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class BackgroundWorkerTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Sweep = Settings.SweepInterval.Default;

    private readonly FixedClock _clock = new(Noon);
    private readonly ConfigurationInMemory _configuration = new();
    private readonly JobRunsInMemory _runs = new();
    private readonly EventsInMemory _alerts = new();
    private readonly NotificationHandlerInMemory _sent = new();
    private readonly LogsInMemory _logs = new();
    private readonly ILoggerFactory _logging;
    private readonly ServiceProvider _services;
    private readonly Lock _gate = new();
    private readonly List<UnitOfWorkInMemory> _units = [];

    /// <summary>
    /// A deployment's container, with the ports the worker reads over fakes.
    /// </summary>
    public BackgroundWorkerTests()
    {
        _logging = LoggerFactory.Create(logging => logging.AddProvider(_logs));

        var services = new ServiceCollection();

        services.AddSingleton<IConfigurationStore>(_configuration);
        services.AddSingleton<IJobRuns>(_runs);
        services.AddSingleton<IAlertChannels>(_alerts);
        services.AddSingleton<INotificationHandler>(_sent);
        services.AddSingleton<IAlertLedger, AlertLedgerInMemory>();
        services.AddSingleton<IAlertLog, AlertLogInMemory>();
        services.AddScoped<AlertRouter>();
        services.AddScoped<IUnitOfWork>(_ => Begun());

        _services = services.BuildServiceProvider();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        _logging.Dispose();
        _logs.Dispose();
    }

    /// <summary>
    /// INF-BG-001 AC1: a job runs when its interval comes round, with nobody asking,
    /// and not before; the worker says how long it has until then.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_BG_001_AC1_AJobRunsAtItsIntervalWithoutAPersonAsync()
    {
        int ran = 0;
        using BackgroundWorker worker = Worker(Counted("counted", () => ran++));

        Assert.Equal(Sweep, await RoundAsync(worker));
        Assert.Equal(1, ran);
        Assert.Equal(Noon, _runs.SucceededAt("counted"));

        _clock.Advance(Sweep - TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromSeconds(1), await RoundAsync(worker));
        Assert.Equal(1, ran);

        _clock.Advance(TimeSpan.FromSeconds(1));

        _ = await RoundAsync(worker);

        Assert.Equal(2, ran);
    }

    /// <summary>
    /// INF-BG-001 AC1: the interval is the one the deployment configured, read at each
    /// turn, so a shorter one takes effect without a restart.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_BG_001_AC1_TheIntervalIsTheConfiguredOneAsync()
    {
        int ran = 0;
        using BackgroundWorker worker = Worker(Counted("configured", () => ran++));

        _configuration.Set(Settings.SweepInterval, TimeSpan.FromMinutes(1));

        Assert.Equal(TimeSpan.FromMinutes(1), await RoundAsync(worker));

        _clock.Advance(TimeSpan.FromMinutes(1));

        _ = await RoundAsync(worker);

        Assert.Equal(2, ran);
    }

    /// <summary>
    /// INF-BG-001 AC1: two processes of one deployment run a job once between them in
    /// its interval, whichever asks first.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_BG_001_AC1_TwoProcessesRunAJobOnceBetweenThemAsync()
    {
        int ran = 0;
        BackgroundJob job = Counted("shared", () => ran++);

        using BackgroundWorker first = Worker(job);
        using BackgroundWorker second = Worker(job);

        _ = await RoundAsync(first);
        _ = await RoundAsync(second);

        Assert.Equal(1, ran);
    }

    /// <summary>
    /// INF-BG-001 AC2: a job that keeps failing raises <c>background-job-failed</c> once
    /// its last success is older than twice its interval, once in the deduplication
    /// window, named by the job.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_BG_001_AC2_AJobThatKeepsFailingIsRaisedOnceAWindowAsync()
    {
        using BackgroundWorker worker = Worker(Failing("failing"));

        await TurnsAsync(worker, 3);

        Assert.Empty(_alerts.Of<AlertRaised>());

        await TurnsAsync(worker, 1);

        AlertRaised raised = Assert.Single(_alerts.Of<AlertRaised>());

        Assert.Equal(AlertCondition.BackgroundJobFailed, raised.Condition);
        Assert.StartsWith("background-job-failed:failing@", raised.IdempotencyKey, StringComparison.Ordinal);
        Assert.Equal("failing", raised.Details["job"].GetString());

        await TurnsAsync(worker, 10);

        Assert.Single(_alerts.Of<AlertRaised>());

        await TurnsAsync(worker, 2);

        Assert.Equal(2, _alerts.Of<AlertRaised>().Count);
    }

    /// <summary>
    /// INF-BG-001 AC2: a job that throws does not stop the others, is written down by
    /// the full type name and frames of what it threw and never by its message
    /// (CONV-LOG-003), and lapses like one that failed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_BG_001_AC2_AJobThatThrowsIsRaisedAndTheOthersKeepRunningAsync()
    {
        int ran = 0;
        using BackgroundWorker worker = Worker(Throwing("throwing"), Counted("beside", () => ran++));

        await TurnsAsync(worker, 4);

        Assert.Equal(4, ran);
        Assert.Equal("throwing", Assert.Single(_alerts.Of<AlertRaised>()).Details["job"].GetString());
        Assert.Contains(_logs.Lines, line => line.Contains("Failure=System.InvalidOperationException", StringComparison.Ordinal));
        Assert.DoesNotContain(_logs.Lines, line => line.Contains("person@example.test", StringComparison.Ordinal));
    }

    /// <summary>
    /// INF-BG-001 AC2: a job that has never once succeeded lapses two intervals after
    /// the worker first took its turn, and one that succeeds is never raised.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_BG_001_AC2_OnlyTheJobThatStoppedSucceedingIsRaisedAsync()
    {
        using BackgroundWorker worker = Worker(Failing("stopped"), Counted("running", () => { }));

        await TurnsAsync(worker, 4);

        Assert.Equal(
            ["stopped"],
            _alerts.Of<AlertRaised>().Select(raised => raised.Details["job"].GetString()));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a lapse whose alert cannot be raised is refused after its
    /// unit of work began, and rolls it back, so the claim on the lapse is not kept; a
    /// turn that finds no lapse ends its unit of work too.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ALapseThatCannotBeRaisedRollsBackAsync()
    {
        _alerts.Refusal = Error.From(ErrorCodes.SystemFault);

        using BackgroundWorker worker = Worker(Failing("failing"));

        await TurnsAsync(worker, 4);

        UnitOfWorkInMemory[] units;

        lock (_gate)
        {
            units = [.. _units];
        }

        Assert.Empty(_alerts.Of<AlertRaised>());
        Assert.All(units, unit => Assert.False(unit.Open));
        Assert.Equal(1, units.Sum(unit => unit.RolledBack));
        Assert.Equal(units.Sum(unit => unit.Opened) - 1, units.Sum(unit => unit.Committed));
    }

    /// <summary>
    /// OPS-ALERT-001 AC4 and INF-BG-001 AC2: with <c>alert-dispatch</c> stalled, its
    /// lapse is still raised through the channels and is also delivered by the router
    /// straight from the worker, so it reaches the destinations; the lapse of any other
    /// job waits for the carrier.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_001_AC4_TheLapseOfAStalledCarrierReachesTheDestinationsAsync()
    {
        _configuration.Set<IReadOnlyList<string>>(Settings.AlertingEmailDestinations, ["ops@example.test"]);
        _configuration.Set<IReadOnlyList<string>>(Settings.AlertingSmsDestinations, ["+201001234567"]);

        using BackgroundWorker worker = Worker(Failing(AlertDispatch.Job), Failing("stopped"));

        await TurnsAsync(worker, 4);

        SendRequest delivered = Assert.Single(_sent.Mail);

        Assert.Equal(MessageKind.Alert, delivered.Message);
        Assert.Equal("background-job-failed", delivered.Values["condition"]);
        Assert.Equal(AlertDispatch.Job, delivered.Values["job"]);
        Assert.Equal(
            [AlertDispatch.Job, "stopped"],
            _alerts.Of<AlertRaised>().Select(raised => raised.Details["job"].GetString()).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// INF-BG-002 AC1, IDN-PRIN-001 AC1: a job's work is handed its own named principal
    /// and never a person or nobody, and a job stating no reason cannot be built.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_BG_002_AC1_AJobRunsAsItsPrincipalAsync()
    {
        AccessContext? handed = null;

        var job = BackgroundJob.Every(
            "principal",
            "OPS-OBS-003",
            SystemOperation.ExpirySweep,
            Settings.SweepInterval,
            (_, context, _) =>
            {
                handed = context;

                return ValueTask.FromResult(Result.Success());
            });

        using BackgroundWorker worker = Worker(job);

        _ = await RoundAsync(worker);

        Assert.NotNull(handed);
        Assert.Same(job.Principal, handed.Principal);
        Assert.Null(handed.Acting);
        Assert.True(handed.Principal!.MayRun(SystemOperation.ExpirySweep));

        _ = Assert.Throws<ArgumentException>(() => BackgroundJob.Every(
            "unreasoned",
            " ",
            SystemOperation.ExpirySweep,
            Settings.SweepInterval,
            (_, _, _) => ValueTask.FromResult(Result.Success())));
    }

    /// <summary>
    /// INF-BG-001 (D-166, 334): a run in progress holds no other job's turn. The worker
    /// starts it and goes on, keeps no second run of the job in flight however many
    /// rounds pass, judges the job's lapse after its own run, and waits for it when it
    /// stops.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_BG_001_ARunInProgressHoldsNoOtherJobsTurnAsync()
    {
        var release = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        int started = 0;
        int beside = 0;

        var slow = BackgroundJob.Every(
            "slow",
            "OPS-OBS-003",
            SystemOperation.ExpirySweep,
            Settings.SweepInterval,
            async (_, _, cancellationToken) =>
            {
                _ = Interlocked.Increment(ref started);

                return await release.Task.WaitAsync(cancellationToken);
            });

        using BackgroundWorker worker = Worker(slow, Counted("beside", () => beside++));

        _ = await worker.RunDueAsync(TestContext.Current.CancellationToken);

        for (int turn = 0; turn < 3; turn++)
        {
            await BesideEndedAsync(worker);

            Assert.Equal(turn + 1, Volatile.Read(ref beside));

            _clock.Advance(Sweep);
            _ = await worker.RunDueAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, Volatile.Read(ref started));
        Assert.Null(_runs.SucceededAt("slow"));
        Assert.Empty(_alerts.Of<AlertRaised>());

        release.SetResult(Result.Failure(Error.From(ErrorCodes.SystemFault)));

        await worker.SettledAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, Volatile.Read(ref started));
        Assert.Equal(
            ["slow"],
            _alerts.Of<AlertRaised>().Select(raised => raised.Details["job"].GetString()));
    }

    /// <summary>
    /// INF-BG-002, IDN-PRIN-001 AC3: every job the library schedules is a principal of
    /// its own name, restricted to the one operation it is, stating the item that
    /// requires it.
    /// </summary>
    [Fact]
    public void INF_BG_002_EveryScheduledJobIsANamedRestrictedPrincipal()
    {
        Assert.Equal(
            BackgroundJobs.All.Count,
            BackgroundJobs.All.Select(job => job.Name).Distinct(StringComparer.Ordinal).Count());

        Assert.All(BackgroundJobs.All, job =>
        {
            Assert.True(job.Principal.IsDeploymentScoped);
            Assert.Single(job.Principal.Operations);
            Assert.Matches("^[A-Z]+(-[A-Z]+)*-[0-9]{3}[a-z]?$", job.Principal.Reason);
        });
    }

    // Each scope's unit of work, kept so a test reads how every one of them ended.
    private UnitOfWorkInMemory Begun()
    {
        var unit = new UnitOfWorkInMemory();

        lock (_gate)
        {
            _units.Add(unit);
        }

        return unit;
    }

    private BackgroundWorker Worker(params IReadOnlyList<BackgroundJob> jobs) =>
        new(
            _services.GetRequiredService<IServiceScopeFactory>(),
            jobs,
            _clock,
            _logging.CreateLogger<BackgroundWorker>());

    // One round run to its end: the runs it started are awaited, and what comes back is
    // how long the worker then waits.
    private static async Task<TimeSpan> RoundAsync(BackgroundWorker worker)
    {
        _ = await worker.RunDueAsync(TestContext.Current.CancellationToken);

        await worker.SettledAsync(TestContext.Current.CancellationToken);

        return await worker.RunDueAsync(TestContext.Current.CancellationToken);
    }

    // Each turn moves the clock on by the sweep interval and runs what is due.
    private async Task TurnsAsync(BackgroundWorker worker, int turns)
    {
        for (int turn = 0; turn < turns; turn++)
        {
            _ = await RoundAsync(worker);

            _clock.Advance(Sweep);
        }
    }

    // The run of the job beside the slow one has ended, while the slow one is still in
    // flight: the worker knows when its next turn is, and with the clock standing,
    // starts nothing.
    private static async Task BesideEndedAsync(BackgroundWorker worker)
    {
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        patience.CancelAfter(TimeSpan.FromSeconds(10));

        while (await worker.RunDueAsync(patience.Token) != Sweep)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), patience.Token);
        }
    }

    private static BackgroundJob Counted(string name, Action counted) =>
        BackgroundJob.Every(
            name,
            "OPS-OBS-003",
            SystemOperation.ExpirySweep,
            Settings.SweepInterval,
            (_, _, _) =>
            {
                counted();

                return ValueTask.FromResult(Result.Success());
            });

    private static BackgroundJob Failing(string name) =>
        BackgroundJob.Every(
            name,
            "OPS-OBS-003",
            SystemOperation.ExpirySweep,
            Settings.SweepInterval,
            (_, _, _) => ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.SystemFault))));

    private static BackgroundJob Throwing(string name) =>
        BackgroundJob.Every(
            name,
            "OPS-OBS-003",
            SystemOperation.ExpirySweep,
            Settings.SweepInterval,
            (_, _, _) => throw new InvalidOperationException("Could not read person@example.test."));
}
