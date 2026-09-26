using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Background;

namespace Janus.Storage.Authentication.Background;

/// <summary>
/// Where the runs of the background jobs are claimed and recorded.
/// </summary>
/// <param name="connections">The connection and transaction the operation holds.</param>
/// <remarks>
/// Implements INF-BG-001 and CONV-DESIGN-003. Each claim is one statement whose
/// condition the database judges, so of two processes asking together one is answered
/// yes and the other no, with no read before the write.
/// </remarks>
internal sealed class JobRunStore(DataConnections connections) : IJobRuns
{
    private const string Claim =
        """
        INSERT INTO identity.background_jobs (name, recorded_at, attempted_at)
        VALUES (@job, @now, @now)
        ON CONFLICT (name) DO UPDATE SET attempted_at = excluded.attempted_at
        WHERE background_jobs.attempted_at <= @due;
        """;

    private const string Success =
        """
        UPDATE identity.background_jobs SET succeeded_at = @at WHERE name = @job;
        """;

    private const string Lapse =
        """
        UPDATE identity.background_jobs SET lapse_raised_at = @now
        WHERE name = @job
          AND COALESCE(succeeded_at, recorded_at) < @stale
          AND (lapse_raised_at IS NULL OR lapse_raised_at <= @standing);
        """;

    /// <inheritdoc/>
    public async ValueTask<bool> ClaimAsync(
        string job,
        DateTimeOffset now,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        int claimed = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Claim,
                new { job, now, due = now - interval },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return claimed == 1;
    }

    /// <inheritdoc/>
    public async ValueTask SucceededAsync(string job, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        _ = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Success,
                new { job, at },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> LapsedAsync(
        string job,
        DateTimeOffset now,
        TimeSpan interval,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        int raised = await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                Lapse,
                new { job, now, stale = now - (interval * 2), standing = now - window },
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return raised == 1;
    }
}
