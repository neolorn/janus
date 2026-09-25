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
/// <param name="ranges">What answers whether a source is a datacenter address.</param>
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
    /// <param name="source">The address the registration came from.</param>
    /// <param name="token">The challenge token presented, where one was.</param>
    /// <param name="cancellationToken">Abandons the check.</param>
    /// <returns>
    /// Nothing where the step goes on, or <c>auth.challenge.required</c> where a
    /// signal fired and the deployment can judge a challenge.
    /// </returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public async ValueTask<Result> CheckAsync(
        string source,
        [NeverLogged] string? token,
        CancellationToken cancellationToken)
    {
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

        BotDefenceSignal? fired = await FiredAsync(
            source,
            watched,
            repeated,
            now,
            cancellationToken).ConfigureAwait(false);

        if (fired is not BotDefenceSignal signal)
        {
            return Result.Success();
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (verifier is null)
        {
            // Nothing can judge a challenge here, so the signal is recorded and the
            // customer is not shown a puzzle for no one to mark (AUTH-ABUSE-008).
            await audit
                .SignalledAsync(signal, source, challenged: false, now, cancellationToken)
                .ConfigureAwait(false);

            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }

        await audit
            .SignalledAsync(signal, source, challenged: true, now, cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        if (token is null
            || !await verifier.Passes(token, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(Error.From(ErrorCodes.ChallengeRequired));
        }

        return Result.Success();
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private async ValueTask<BotDefenceSignal?> FiredAsync(
        string source,
        IReadOnlySet<BotDefenceSignal> watched,
        int repeated,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (watched.Contains(BotDefenceSignal.DatacenterRange) && ranges.Contains(source))
        {
            return BotDefenceSignal.DatacenterRange;
        }

        if (!watched.Contains(BotDefenceSignal.RepeatedAttempts))
        {
            return null;
        }

        int started = await sources
            .SinceAsync(source, now - Hour, cancellationToken)
            .ConfigureAwait(false);

        return started > repeated ? BotDefenceSignal.RepeatedAttempts : null;
    }
}
