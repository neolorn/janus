using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Alerting;

/// <summary>
/// What remembers which conditions have already been raised, so that one sustained
/// attack produces one alert and not one per attempt.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-002, OPS-ALERT-003 and CONV-DESIGN-003. A claim names the raise
/// it was made for and stands unanswered until the host's handler takes that raise's
/// alert, so a raise whose hand-over never answered is not folded into itself, and one
/// already taken is.
/// </remarks>
internal interface IAlertLedger
{
    /// <summary>
    /// Whether this condition is the first of its window, recording its claim where it
    /// is: no other raise's claim stands inside the window, and no claim of its own was
    /// taken. A claim of its own that was never answered does not count against it.
    /// </summary>
    /// <param name="key">The deduplication key.</param>
    /// <param name="raise">The raise the claim is made for.</param>
    /// <param name="at">When it fired.</param>
    /// <param name="window">How long one alert stands for the rest.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it is delivered rather than folded into one already sent.</returns>
    ValueTask<bool> FirstAsync(
        string key,
        RaisedAlertId raise,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records that the host's handler took the alert a raise claimed the key for, so a
    /// later hand-over of the same raise, and any other raise inside the window, is
    /// folded into it. It is recorded whatever an earlier, abandoned hand-over of the same
    /// raise gave back meanwhile, and over a claim another raise made since that no
    /// handler has answered; another raise's claim that was taken stands.
    /// </summary>
    /// <param name="key">The deduplication key.</param>
    /// <param name="raise">The raise whose claim it is.</param>
    /// <param name="at">When it fired.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it was recorded.</returns>
    ValueTask<bool> TakenAsync(string key, RaisedAlertId raise, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>
    /// Gives back the claim on a key whose alert the host's handler did not take, so its
    /// next hand-over is the first of its window again, by one statement that changes
    /// nothing where another raise has claimed the key since or the raise's own claim
    /// was taken.
    /// </summary>
    /// <param name="key">The deduplication key.</param>
    /// <param name="raise">The raise whose claim it is.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether the claim was given back.</returns>
    ValueTask<bool> ForgetAsync(string key, RaisedAlertId raise, CancellationToken cancellationToken);
}
