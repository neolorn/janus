using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Background;

/// <summary>
/// Where each background job's runs are kept: when it was last attempted, when it last
/// succeeded, and when its lapse was last raised.
/// </summary>
/// <remarks>
/// Implements INF-BG-001 and CONV-DESIGN-003. A run and a lapse are each claimed by
/// one conditional write, so two processes of one deployment never run a job twice in
/// its interval and never raise one lapse twice in the deduplication window.
/// </remarks>
internal interface IJobRuns
{
    /// <summary>
    /// Claims a run of a job, which is the job's to take once its last attempt is at
    /// least an interval old. A job never seen before is recorded as of now.
    /// </summary>
    /// <param name="job">The job's name.</param>
    /// <param name="now">The instant of the claim.</param>
    /// <param name="interval">How often the job runs.</param>
    /// <param name="cancellationToken">Abandons the claim.</param>
    /// <returns>Whether this process is the one to run it now.</returns>
    ValueTask<bool> ClaimAsync(string job, DateTimeOffset now, TimeSpan interval, CancellationToken cancellationToken);

    /// <summary>
    /// Records that a run of a job succeeded.
    /// </summary>
    /// <param name="job">The job's name.</param>
    /// <param name="at">When it finished.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask SucceededAsync(string job, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>
    /// Claims the alert of a job that has lapsed: one whose last success, or its first
    /// recording where it never succeeded, is older than twice its interval, and whose
    /// lapse was not raised inside the window.
    /// </summary>
    /// <param name="job">The job's name.</param>
    /// <param name="now">The instant of the claim.</param>
    /// <param name="interval">How often the job runs.</param>
    /// <param name="window">How long one raised lapse stands for the rest.</param>
    /// <param name="cancellationToken">Abandons the claim.</param>
    /// <returns>Whether the job has lapsed and this process is the one to raise it.</returns>
    ValueTask<bool> LapsedAsync(
        string job,
        DateTimeOffset now,
        TimeSpan interval,
        TimeSpan window,
        CancellationToken cancellationToken);
}
