using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// The additional challenge at registration, shown on an adverse signal and on
/// nothing else. Phone verification already imposes attacker cost; a puzzle in front
/// of every customer is friction without proportionate benefit.
/// </summary>
/// <param name="configuration">Where the signals and their counts come from.</param>
/// <param name="ranges">What answers whether an address is a datacenter address.</param>
/// <param name="sources">What counts registration sessions per source.</param>
/// <param name="audit">Where a signal with no challenge behind it is written down.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="verifier">The deployment challenge verifier, where one is registered.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-ABUSE-008. The library ships no challenge: the verifier is the
/// deployment, and where none is registered the signal is recorded and the step goes
/// on rather than showing something nothing can judge.
/// </remarks>
internal sealed class BotDefence(
    IConfigurationStore configuration,
    IDatacenterRanges ranges,
    IRegistrationSources sources,
    IBotDefenceAudit audit,
    IUnitOfWork work,
    ChallengeVerifier? verifier,
    TimeProvider time)
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    /// <summary>
    /// Whether this registration step goes on, and what it asks for first.
    /// </summary>
    /// <param name="ipAddress">
    /// The whole address the request arrived on, which the datacenter ranges are
    /// matched against.
    /// </param>
    /// <param name="source">
    /// The source of the request, which its registration sessions are counted under
    /// (AUTH-ABUSE-001).
    /// </param>
    /// <param name="token">The challenge token presented, where one was.</param>
    /// <param name="cancellationToken">Abandons the check.</param>
    /// <returns>
    /// Nothing where the step goes on, <c>auth.challenge.required</c> where a signal
    /// fired and the deployment can judge a challenge, or the failure where a
    /// degradation of the range file could not be raised.
    /// </returns>
    /// <exception cref="ArgumentNullException">The address or the source is absent.</exception>
    public async ValueTask<Result> CheckAsync(
        string ipAddress,
        string source,
        [NeverLogged] string? token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ipAddress);
        ArgumentNullException.ThrowIfNull(source);

        Error? failure = null;

        IReadOnlySet<BotDefenceSignal> watched = (await configuration
                .ReadAsync(Settings.AbuseBotDefenceSignals, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlySet<BotDefenceSignal>>(error, ref failure));

        int repeated = (await configuration
                .ReadAsync(Settings.AbuseBotDefenceRepeatedAttempts, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        BotDefenceSignal? fired = (await FiredAsync(
                ipAddress,
                source,
                watched,
                repeated,
                now,
                cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<BotDefenceSignal?>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if (fired is not BotDefenceSignal signal)
        {
            return Result.Success();
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        if (verifier is null)
        {
            // Nothing can judge a challenge here, so the signal is recorded and the
            // customer is not shown a puzzle for no one to mark (AUTH-ABUSE-008).
            await audit
                .SignalledAsync(signal, source, challenged: false, now, cancellationToken)
                .ConfigureAwait(false);

            if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error notCommittedAgain)
            {
                return Result.Failure(notCommittedAgain);
            }

            return Result.Success();
        }

        await audit
            .SignalledAsync(signal, source, challenged: true, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        if (token is null
            || !await verifier.Passes(token, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(Error.From(ErrorCodes.ChallengeRequired));
        }

        return Result.Success();
    }

    /// <summary>
    /// Counts one registration session against the source that started it. It joins
    /// the unit of work that creates the session, so a session that is not created is
    /// not counted.
    /// </summary>
    /// <param name="source">The source the session was started from.</param>
    /// <param name="at">When it started.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of counting it.</returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public async ValueTask StartedAsync(string source, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        await sources.RecordAsync(source, at, cancellationToken).ConfigureAwait(false);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private async ValueTask<Result<BotDefenceSignal?>> FiredAsync(
        string ipAddress,
        string source,
        IReadOnlySet<BotDefenceSignal> watched,
        int repeated,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // AUTH-ABUSE-008: the ranges are asked only while the signal is watched, so a
        // deployment that took it out of the set is told nothing of a file it does not
        // use, and they are asked about the whole address, never the counting source.
        if (watched.Contains(BotDefenceSignal.DatacenterRange))
        {
            Error? unanswered = null;

            bool inside = (await ranges.ContainsAsync(ipAddress, cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Held<bool>(error, ref unanswered));

            if (unanswered is not null)
            {
                return Result.Failure<BotDefenceSignal?>(unanswered);
            }

            if (inside)
            {
                return Result.Success<BotDefenceSignal?>(BotDefenceSignal.DatacenterRange);
            }
        }

        if (!watched.Contains(BotDefenceSignal.RepeatedAttempts))
        {
            return Result.Success<BotDefenceSignal?>(null);
        }

        int started = await sources
            .SinceAsync(source, now - Hour, cancellationToken)
            .ConfigureAwait(false);

        // AUTH-ABUSE-008 AC3: the session this request would create counts with those
        // already created, so the one that makes more than the setting is the one
        // challenged.
        return Result.Success<BotDefenceSignal?>(started + 1 > repeated ? BotDefenceSignal.RepeatedAttempts : null);
    }
}
