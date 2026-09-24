using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Background;

namespace Janus.Hosting.Tests.Background;

/// <summary>
/// The background jobs' runs, held as the table holds them and claimed on the same
/// conditions, so two workers over one instance behave as two processes over one
/// database.
/// </summary>
internal sealed class JobRunsInMemory : IJobRuns
{
    private readonly Dictionary<string, Run> _runs = new(StringComparer.Ordinal);

    /// <summary>
    /// When a job last succeeded, or nothing where it never has or was never seen.
    /// </summary>
    /// <param name="job">The job's name.</param>
    /// <returns>The instant.</returns>
    public DateTimeOffset? SucceededAt(string job) =>
        _runs.TryGetValue(job, out Run? run) ? run.SucceededAt : null;

    /// <inheritdoc/>
    public ValueTask<bool> ClaimAsync(
        string job,
        DateTimeOffset now,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        if (!_runs.TryGetValue(job, out Run? run))
        {
            _runs[job] = new Run(now) { AttemptedAt = now };

            return ValueTask.FromResult(true);
        }

        if (run.AttemptedAt > now - interval)
        {
            return ValueTask.FromResult(false);
        }

        run.AttemptedAt = now;

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask SucceededAsync(string job, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (_runs.TryGetValue(job, out Run? run))
        {
            run.SucceededAt = at;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<bool> LapsedAsync(
        string job,
        DateTimeOffset now,
        TimeSpan interval,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        if (!_runs.TryGetValue(job, out Run? run)
            || (run.SucceededAt ?? run.RecordedAt) >= now - (interval * 2)
            || run.LapseRaisedAt > now - window)
        {
            return ValueTask.FromResult(false);
        }

        run.LapseRaisedAt = now;

        return ValueTask.FromResult(true);
    }

    private sealed class Run(DateTimeOffset recordedAt)
    {
        public DateTimeOffset RecordedAt { get; } = recordedAt;

        public DateTimeOffset AttemptedAt { get; set; }

        public DateTimeOffset? SucceededAt { get; set; }

        public DateTimeOffset? LapseRaisedAt { get; set; }
    }
}
