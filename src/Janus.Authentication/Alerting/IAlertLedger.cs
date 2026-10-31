using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Alerting;

/// <summary>
/// What remembers which conditions have already been raised, so that one sustained
/// attack produces one alert and not one per attempt.
/// </summary>
/// <remarks>Implements OPS-ALERT-002 and CONV-DESIGN-003.</remarks>
internal interface IAlertLedger
{
    /// <summary>
    /// Whether this condition is the first of its window, recording it where it is.
    /// </summary>
    /// <param name="key">The deduplication key.</param>
    /// <param name="at">When it fired.</param>
    /// <param name="window">How long one alert stands for the rest.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it is delivered rather than folded into one already sent.</returns>
    ValueTask<bool> FirstAsync(
        string key,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gives back the claim on a key whose alert the host's handler did not take, so its
    /// next hand-over is the first of its window again, by one statement that changes
    /// nothing where another raise has claimed the key since.
    /// </summary>
    /// <param name="key">The deduplication key.</param>
    /// <param name="at">When the alert whose claim it is fired.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the claim was given back.</returns>
    ValueTask<bool> ForgetAsync(string key, DateTimeOffset at, CancellationToken cancellationToken);
}
