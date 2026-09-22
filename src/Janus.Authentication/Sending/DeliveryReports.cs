using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// The delivery-report callback, treated as what it is: an unauthenticated request
/// over plain HTTP with its parameters in the query string. The one state it may
/// change is to take a send back out of the buckets it counted against.
/// </summary>
/// <param name="configuration">Where the rate limit and the alert threshold come from.</param>
/// <param name="ledger">Where the send was counted.</param>
/// <param name="callbacks">What counts callbacks per source.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="events">Where the repeated-failure alert goes.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-ABUSE-007, INT-SMS-005 and INT-GEN-003. A forged failure report
/// gains an attacker at most one extra send to a number the restriction already
/// allows; nothing here can mark a phone verified.
/// </remarks>
internal sealed class DeliveryReports(
    IConfigurationStore configuration,
    ISendLedger ledger,
    ICallbackLedger callbacks,
    IUnitOfWork work,
    IEvents events,
    TimeProvider time)
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    /// <summary>
    /// Takes one delivery report.
    /// </summary>
    /// <param name="source">Where the callback came from.</param>
    /// <param name="reference">The correlation reference it carried.</param>
    /// <param name="delivered">What it says became of the message.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where a failed send was released, or
    /// <c>integration.callback.rejected</c> where the callback was rate-limited or
    /// carried a reference no live send answers to.
    /// </returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public async ValueTask<Result> ReportAsync(
        string source,
        string? reference,
        bool delivered,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        Error? failure = null;

        int limit = (await configuration
                .ReadAsync(Settings.IntegrationCallbackRateLimit, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        int threshold = (await configuration
                .ReadAsync(Settings.AlertingCallbackThreshold, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        int made = await callbacks
            .ReceivedAsync(source, now, Minute, cancellationToken)
            .ConfigureAwait(false);

        if (made > limit)
        {
            // Answered before any lookup, so a flood costs the deployment nothing
            // beyond the count it was already keeping (INT-GEN-003).
            return await RejectedAsync(source, now, threshold, cancellationToken).ConfigureAwait(false);
        }

        // A report that a message arrived advances nothing at all: the state it might
        // seem to confirm is proved by the code the person types, never by the
        // gateway saying so (AUTH-ABUSE-007).
        if (delivered)
        {
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            return await RejectedAsync(source, now, threshold, cancellationToken).ConfigureAwait(false);
        }

        bool released = await ledger
            .ReleaseAsync(SendReferences.Of(reference), cancellationToken)
            .ConfigureAwait(false);

        if (!released)
        {
            return await RejectedAsync(source, now, threshold, cancellationToken).ConfigureAwait(false);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private async ValueTask<Result> RejectedAsync(
        string source,
        DateTimeOffset now,
        int threshold,
        CancellationToken cancellationToken)
    {
        int rejected = await callbacks
            .RejectedAsync(source, now, now - Hour, cancellationToken)
            .ConfigureAwait(false);

        if (rejected > threshold)
        {
            await events
                .PublishAsync(
                    Alerts.Of(AlertCondition.CallbackVerificationFailed, source, now),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Failure(Error.From(ErrorCodes.CallbackRejected));
    }
}
