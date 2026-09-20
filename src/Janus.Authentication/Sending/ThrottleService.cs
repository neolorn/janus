using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// The progressive delay in front of every authentication attempt, registration and
/// recovery alike. It answers the same for an identifier an account holds and one it
/// does not, because nothing on this path asks.
/// </summary>
/// <param name="configuration">Where the threshold, the delays and the caps come from.</param>
/// <param name="ledger">Where failures are counted.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="events">Where the sustained-failure alert goes.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-ABUSE-001, AUTH-ABUSE-002 and OPS-ALERT-001. A source arriving
/// with a browser the account already knows is exempt from the account and
/// identifier components, so an attack on an address does not hold its owner out.
/// </remarks>
internal sealed class ThrottleService(
    IConfigurationStore configuration,
    IThrottleLedger ledger,
    IUnitOfWork work,
    IEvents events,
    TimeProvider time)
{
    /// <summary>
    /// How long this attempt waits before it is even looked at.
    /// </summary>
    /// <param name="attempt">Who is attempting what, from where.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The delay, zero where none stands.</returns>
    /// <exception cref="ArgumentNullException">The attempt is absent.</exception>
    public async ValueTask<Result<TimeSpan>> DelayAsync(
        ThrottleAttempt attempt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        Error? failure = null;

        ThrottleTerms terms = (await TermsAsync(cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<ThrottleTerms>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<TimeSpan>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();
        TimeSpan standing = TimeSpan.Zero;

        foreach ((ThrottleScope scope, string key) in Scopes(attempt))
        {
            ThrottleCounter? counted = await ledger
                .FindAsync(scope, key, cancellationToken)
                .ConfigureAwait(false);

            TimeSpan delay = Throttle.Delay(
                Throttle.Standing(counted, now, terms.Decay),
                terms,
                Throttle.Cap(scope, terms));

            if (delay > standing)
            {
                standing = delay;
            }
        }

        return Result.Success(standing);
    }

    /// <summary>
    /// The refusal a standing delay produces, which says how long is left and
    /// nothing about whether the account exists.
    /// </summary>
    /// <param name="delay">What the attempt waits.</param>
    /// <returns>The failure.</returns>
    public static Error Refusal(TimeSpan delay) =>
        Error.From(
            ErrorCodes.Throttled,
            "retryAfter",
            JsonSerializer.SerializeToElement(
                ((int)Math.Ceiling(delay.TotalSeconds)).ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// Counts one failed attempt against every scope it belongs to.
    /// </summary>
    /// <param name="attempt">Who attempted what, from where.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>Nothing, or the failure where the terms cannot be read.</returns>
    /// <exception cref="ArgumentNullException">The attempt is absent.</exception>
    public async ValueTask<Result> FailedAsync(
        ThrottleAttempt attempt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        Error? failure = null;

        ThrottleTerms terms = (await TermsAsync(cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<ThrottleTerms>(error, ref failure));

        int threshold = (await configuration
                .ReadAsync(Settings.AlertingAuthFailuresThreshold, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        foreach ((ThrottleScope scope, string key) in Scopes(attempt))
        {
            ThrottleCounter? counted = await ledger
                .FindAsync(scope, key, cancellationToken)
                .ConfigureAwait(false);

            int standing = Throttle.Standing(counted, now, terms.Decay);

            await ledger
                .FailedAsync(scope, key, standing, now, cancellationToken)
                .ConfigureAwait(false);

            if (scope is ThrottleScope.Account && standing + 1 >= threshold)
            {
                await events
                    .PublishAsync(
                        Alerts.Of(AlertCondition.AuthFailuresSustained, key, now),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Forgets what an attempt accumulated, which a sign-in that succeeded does.
    /// </summary>
    /// <param name="attempt">Who attempted what, from where.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of forgetting it.</returns>
    /// <exception cref="ArgumentNullException">The attempt is absent.</exception>
    public async ValueTask SucceededAsync(ThrottleAttempt attempt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        foreach ((ThrottleScope scope, string key) in Scopes(attempt))
        {
            await ledger.ClearAsync(scope, key, cancellationToken).ConfigureAwait(false);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static IEnumerable<(ThrottleScope Scope, string Key)> Scopes(ThrottleAttempt attempt)
    {
        yield return (ThrottleScope.Source, attempt.Source);

        // A browser the account already knows is not the attack, so the components an
        // attacker can raise from anywhere do not hold it (AUTH-ABUSE-001).
        if (attempt.Recognised)
        {
            yield break;
        }

        if (attempt.Account is SubjectId account)
        {
            yield return (ThrottleScope.Account, account.ToString());
        }

        if (attempt.Identifier is not null)
        {
            yield return (ThrottleScope.Identifier, attempt.Identifier);
        }
    }

    private async ValueTask<Result<ThrottleTerms>> TermsAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        bool enabled = (await configuration
                .ReadAsync(Settings.AbuseThrottleEnabled, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<bool>(error, ref failure));

        int threshold = (await configuration
                .ReadAsync(Settings.AbuseThrottleThreshold, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        TimeSpan initial = (await configuration
                .ReadAsync(Settings.AbuseThrottleDelayInitial, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        decimal factor = (await configuration
                .ReadAsync(Settings.AbuseThrottleDelayFactor, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<decimal>(error, ref failure));

        TimeSpan maximum = (await configuration
                .ReadAsync(Settings.AbuseThrottleDelayMax, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        TimeSpan cap = (await configuration
                .ReadAsync(Settings.AbuseThrottleAccountCap, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        TimeSpan decay = (await configuration
                .ReadAsync(Settings.AbuseThrottleDecay, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        return failure is not null
            ? Result.Failure<ThrottleTerms>(failure)
            : Result.Success(
                new ThrottleTerms(enabled, threshold, initial, factor, maximum, cap, decay));
    }
}
