using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Callbacks;

/// <summary>
/// What every inbound callback passes, whoever defined it: the count per source that
/// answers a flood before any lookup, the count of rejections that raises the alert,
/// and the claim on a provider's event that carries a repeated delivery once.
/// </summary>
/// <param name="configuration">Where the rate limit and the alert threshold come from.</param>
/// <param name="callbacks">What counts callbacks per source.</param>
/// <param name="events">What holds the claimed provider events.</param>
/// <param name="alerts">Where the repeated-failure alert goes.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements INT-GEN-003, BFF-MACH-002, BFF-MACH-003 and OPS-ALERT-001. The caller
/// owns the transaction each step runs in, so a count is kept whatever the callback's
/// fate.
/// </remarks>
internal sealed class CallbackAdmission(
    IConfigurationStore configuration,
    ICallbackLedger callbacks,
    ICallbackEvents events,
    IAlertChannels alerts,
    TimeProvider time)
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    /// <summary>
    /// Counts one callback from a source and admits it where the source is within
    /// <c>integration.callback.ratelimit</c> for the minute.
    /// </summary>
    /// <param name="source">Where the callback came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where it is admitted, or <c>integration.callback.rejected</c> carrying
    /// <c>retryAt</c>, the end of the window, where it is not.
    /// </returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public async ValueTask<Result> AdmitAsync(string source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        Result<int> limit = await configuration
            .ReadAsync(Settings.IntegrationCallbackRateLimit, cancellationToken)
            .ConfigureAwait(false);

        return await limit
            .Match<ValueTask<Result>>(
                async admitted =>
                {
                    DateTimeOffset now = time.GetUtcNow();

                    int made = await callbacks
                        .ReceivedAsync(source, now, Minute, cancellationToken)
                        .ConfigureAwait(false);

                    if (made <= admitted)
                    {
                        return Result.Success();
                    }

                    // A fixed window, so the source is admitted again at its boundary.
                    DateTimeOffset closes = new(
                        now.UtcTicks - (now.UtcTicks % Minute.Ticks) + Minute.Ticks,
                        TimeSpan.Zero);

                    return Result.Failure(Error.From(
                        ErrorCodes.CallbackRejected,
                        "retryAt",
                        JsonSerializer.SerializeToElement(closes)));
                },
                error => ValueTask.FromResult(Result.Failure(error)))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Counts one rejected callback from a source, and raises
    /// <c>callback-verification-failed</c> once the source's rejections in the hour pass
    /// <c>alerting.callback.threshold</c>.
    /// </summary>
    /// <param name="source">Where the callback came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// <c>integration.callback.rejected</c>, or the failure that kept the count or the
    /// alert from being made.
    /// </returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public async ValueTask<Result> RejectAsync(string source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        Result<int> threshold = await configuration
            .ReadAsync(Settings.AlertingCallbackThreshold, cancellationToken)
            .ConfigureAwait(false);

        return await threshold
            .Match<ValueTask<Result>>(
                async allowed =>
                {
                    DateTimeOffset now = time.GetUtcNow();

                    int rejected = await callbacks
                        .RejectedAsync(source, now, now - Hour, cancellationToken)
                        .ConfigureAwait(false);

                    if (rejected > allowed)
                    {
                        Result published = await alerts
                            .RaiseAsync(
                                Alerts.Of(AlertCondition.CallbackVerificationFailed, source, now),
                                cancellationToken)
                            .ConfigureAwait(false);

                        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
                        {
                            return Result.Failure(unpublished);
                        }
                    }

                    return Result.Failure(Error.From(ErrorCodes.CallbackRejected));
                },
                error => ValueTask.FromResult(Result.Failure(error)))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Claims a provider's event for a callback, so it is carried once however many
    /// times the provider delivers it.
    /// </summary>
    /// <param name="callback">The callback's name.</param>
    /// <param name="identifier">The provider's event identifier.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether this delivery is the one to carry.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public ValueTask<bool> ClaimAsync(string callback, string identifier, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(identifier);

        return events.ClaimAsync(callback, Hashed(identifier), time.GetUtcNow(), cancellationToken);
    }

    /// <summary>
    /// Gives a claim back where the delivery holding it was not carried through, so the
    /// provider's next delivery is.
    /// </summary>
    /// <param name="callback">The callback's name.</param>
    /// <param name="identifier">The provider's event identifier.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of giving it back.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public ValueTask ReleaseAsync(string callback, string identifier, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(identifier);

        return events.ReleaseAsync(callback, Hashed(identifier), cancellationToken);
    }

    private static byte[] Hashed(string identifier) => SHA256.HashData(Encoding.UTF8.GetBytes(identifier));
}
