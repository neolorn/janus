using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Background;
using Janus.Authentication.Tests;
using Janus.Core;
using Janus.Core.Configuration;
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
    private readonly LogsInMemory _logs = new();
    private readonly ILoggerFactory _logging;
    private readonly ServiceProvider _services;

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
        services.AddScoped<IUnitOfWork, UnitOfWorkInMemory>();

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

        Assert.Equal(Sweep, await worker.RunDueAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, ran);
        Assert.Equal(Noon, _runs.SucceededAt("counted"));

        _clock.Advance(Sweep - TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromSeconds(1), await worker.RunDueAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, ran);

        _clock.Advance(TimeSpan.FromSeconds(1));

        _ = await worker.RunDueAsync(TestContext.Current.CancellationToken);

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

        Assert.Equal(TimeSpan.FromMinutes(1), await worker.RunDueAsync(TestContext.Current.CancellationToken));

        _clock.Advance(TimeSpan.FromMinutes(1));

        _ = await worker.RunDueAsync(TestContext.Current.CancellationToken);

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

        _ = await first.RunDueAsync(TestContext.Current.CancellationToken);
        _ = await second.RunDueAsync(TestContext.Current.CancellationToken);

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
    /// the type of what it threw and never by its message (CONV-LOG-003), and lapses
    /// like one that failed.
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
        Assert.Contains(_logs.Lines, line => line.Contains("Failure=InvalidOperationException", StringComparison.Ordinal));
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

        _ = await worker.RunDueAsync(TestContext.Current.CancellationToken);

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
            Assert.Matches("^[A-Z]+(-[A-Z]+)+-[0-9]{3}[a-z]?$", job.Principal.Reason);
        });
    }

    private BackgroundWorker Worker(params IReadOnlyList<BackgroundJob> jobs) =>
        new(
            _services.GetRequiredService<IServiceScopeFactory>(),
            jobs,
            _clock,
            _logging.CreateLogger<BackgroundWorker>());

    // Each turn moves the clock on by the sweep interval and runs what is due.
    private async Task TurnsAsync(BackgroundWorker worker, int turns)
    {
        for (int turn = 0; turn < turns; turn++)
        {
            _ = await worker.RunDueAsync(TestContext.Current.CancellationToken);

            _clock.Advance(Sweep);
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
