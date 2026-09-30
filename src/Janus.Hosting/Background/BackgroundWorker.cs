using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Background;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Alerting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Background;

/// <summary>
/// What runs the library's scheduled work: each job at its interval, as its principal,
/// in a scope of its own, and a job that has stopped succeeding raised as an alert.
/// </summary>
/// <param name="scopes">Where each run's scope comes from.</param>
/// <param name="jobs">The jobs to run.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="log">The host's logger.</param>
/// <remarks>
/// Implements INF-BG-001, INF-BG-002 and OPS-OBS-003. Every process of a deployment
/// runs the worker and the database decides which one takes each run, so no person
/// has to start anything and no two processes run one job at once. A failure is
/// judged by the last success rather than by the failing run, so a job that stopped
/// being attempted at all is noticed as surely as one that fails.
/// </remarks>
internal sealed class BackgroundWorker(
    IServiceScopeFactory scopes,
    IReadOnlyList<BackgroundJob> jobs,
    TimeProvider time,
    ILogger<BackgroundWorker> log) : BackgroundService
{
    private const string Fault = "fault";

    private readonly Dictionary<string, DateTimeOffset> _due = new(StringComparer.Ordinal);

    /// <summary>
    /// Runs every job whose time has come, and says how long until the next one's has.
    /// </summary>
    /// <param name="cancellationToken">Abandons the round.</param>
    /// <returns>How long to wait before the next round.</returns>
    public async ValueTask<TimeSpan> RunDueAsync(CancellationToken cancellationToken)
    {
        foreach (BackgroundJob job in jobs)
        {
            DateTimeOffset now = time.GetUtcNow();

            if (_due.TryGetValue(job.Name, out DateTimeOffset due) && due > now)
            {
                continue;
            }

            _due[job.Name] = now + await ConsiderAsync(job, now, cancellationToken).ConfigureAwait(false);
        }

        if (_due.Count == 0)
        {
            return Timeout.InfiniteTimeSpan;
        }

        TimeSpan wait = _due.Values.Min() - time.GetUtcNow();

        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan wait = await RunDueAsync(stoppingToken).ConfigureAwait(false);

            await Task.Delay(wait, time, stoppingToken).ConfigureAwait(false);
        }
    }

    // INF-BG-001: one job's fault is not the worker's, so it becomes the job's failure,
    // is written down, and the other jobs keep their turns; the job's lapse is what
    // raises it. A cancellation is the worker's own only when the worker is stopping;
    // one a job's own timeout threw is that job's fault like any other. It is kept by
    // its fault log entry, never by a message, which can carry a value (CONV-LOG-003).
    private static async ValueTask<Result> ContainedAsync(
        Func<ValueTask<Result>> step,
        CancellationToken cancellationToken)
    {
        try
        {
            return await step().ConfigureAwait(false);
        }
        catch (Exception fault)
            when (fault is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(Error.From(
                ErrorCodes.SystemFault,
                Fault,
                JsonSerializer.SerializeToElement(FaultLog.Of(fault))));
        }
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // What a failure is written down as: the fault log entry of what was thrown where
    // it was a fault, and its code otherwise.
    private static string Described(Error error) =>
        error.Details.TryGetValue(Fault, out JsonElement fault)
            ? fault.GetString() ?? string.Empty
            : error.Code.ToString();

    // The job is named by its principal, which is the deployment's arrangement and
    // nobody's data.
    private static Dictionary<string, JsonElement> Lapse(BackgroundJob job) =>
        new(capacity: 2, StringComparer.Ordinal)
        {
            ["job"] = JsonSerializer.SerializeToElement(job.Name),
            ["reason"] = JsonSerializer.SerializeToElement(job.Principal.Reason),
        };

    // One job's turn: its run claimed and taken where this process won the claim, and
    // its lapse raised where it has lapsed, whatever the run did. What comes back is
    // how long until its next turn.
    private async ValueTask<TimeSpan> ConsiderAsync(
        BackgroundJob job,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await RunAsync(job, now, cancellationToken).ConfigureAwait(false) is not TimeSpan interval)
        {
            return job.Fallback;
        }

        await RaiseLapseAsync(job, interval, cancellationToken).ConfigureAwait(false);

        return interval;
    }

    // The interval comes back whenever it was read, so a run that failed or faulted
    // still has its lapse looked at.
    private async ValueTask<TimeSpan?> RunAsync(
        BackgroundJob job,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();

        IServiceProvider services = scope.ServiceProvider;
        TimeSpan? interval = null;

        Result taken = await ContainedAsync(
                async () =>
                {
                    Error? failure = null;

                    TimeSpan read = (await job
                            .IntervalAsync(services.GetRequiredService<IConfigurationStore>(), cancellationToken)
                            .ConfigureAwait(false))
                        .Match(value => value, error => Held<TimeSpan>(error, ref failure));

                    if (failure is not null)
                    {
                        return Result.Failure(failure);
                    }

                    interval = read;

                    IJobRuns runs = services.GetRequiredService<IJobRuns>();

                    if (!await runs.ClaimAsync(job.Name, now, read, cancellationToken).ConfigureAwait(false))
                    {
                        return Result.Success();
                    }

                    failure = (await job.RunAsync(services, cancellationToken).ConfigureAwait(false))
                        .Match(() => (Error?)null, error => error);

                    if (failure is not null)
                    {
                        return Result.Failure(failure);
                    }

                    await runs.SucceededAsync(job.Name, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);

                    return Result.Success();
                },
                cancellationToken)
            .ConfigureAwait(false);

        taken.Switch(() => { }, error => BackgroundLog.Failed(log, job.Name, Described(error)));

        return interval;
    }

    private async ValueTask RaiseLapseAsync(BackgroundJob job, TimeSpan interval, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();

        IServiceProvider services = scope.ServiceProvider;

        Result raised = await ContainedAsync(
                async () =>
                {
                    Error? failure = null;

                    TimeSpan window = (await services.GetRequiredService<IConfigurationStore>()
                            .ReadAsync(Settings.AlertingDedupeWindow, cancellationToken)
                            .ConfigureAwait(false))
                        .Match(value => value, error => Held<TimeSpan>(error, ref failure));

                    if (failure is not null)
                    {
                        return Result.Failure(failure);
                    }

                    IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();
                    DateTimeOffset now = time.GetUtcNow();

                    // The lapse is claimed and raised in one transaction, so a raise that
                    // fails leaves the lapse to be claimed again rather than marked as told.
                    if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
                        .Match<Error?>(() => null, error => error) is Error notBegun)
                    {
                        return Result.Failure(notBegun);
                    }

                    if (!await services.GetRequiredService<IJobRuns>()
                            .LapsedAsync(job.Name, now, interval, window, cancellationToken)
                            .ConfigureAwait(false))
                    {
                        return Result.Success();
                    }

                    AlertRaised lapse = Alerts.Of(AlertCondition.BackgroundJobFailed, job.Name, now, Lapse(job));

                    failure = (await services.GetRequiredService<IAlertChannels>()
                            .RaiseAsync(lapse, cancellationToken)
                            .ConfigureAwait(false))
                        .Match(() => (Error?)null, error => error);

                    // OPS-ALERT-001 AC4 and INF-BG-001 AC2 (D-166, 290): the carrier cannot
                    // carry the news of its own stall, so its lapse is also delivered by the
                    // router here; the row it raised is folded into this delivery by the
                    // deduplication ledger once the carrier runs again.
                    if (failure is null && job.Name == AlertDispatch.Job)
                    {
                        failure = (await services.GetRequiredService<AlertRouter>()
                                .RaiseAsync(lapse, cancellationToken)
                                .ConfigureAwait(false))
                            .Match(_ => (Error?)null, error => error);
                    }

                    if (failure is not null)
                    {
                        return Result.Failure(failure);
                    }

                    if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                        .Match<Error?>(() => null, error => error) is Error notCommitted)
                    {
                        return Result.Failure(notCommitted);
                    }

                    return Result.Success();
                },
                cancellationToken)
            .ConfigureAwait(false);

        raised.Switch(() => { }, error => BackgroundLog.LapseUnraised(log, job.Name, Described(error)));
    }
}
