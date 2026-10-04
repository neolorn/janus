using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// What the prepaid gateway account stands at: read on a schedule, watched for an
/// abnormal drain, and a hard stop under ordinary sends once it reaches the floor.
/// </summary>
/// <param name="configuration">Where the floor, the interval and the factor come from.</param>
/// <param name="sms">What the gateway answers through.</param>
/// <param name="readings">Where the readings are kept.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="alerts">Where the alert goes.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-ABUSE-006, INT-SMS-004 and OPS-ALERT-001. The account is prepaid,
/// so a loop drains money directly and the drain has to be noticed by the system
/// rather than by a person.
/// </remarks>
internal sealed class SmsBalance(
    IConfigurationStore configuration,
    ISmsTransport sms,
    ISmsBalanceLedger readings,
    IUnitOfWork work,
    IAlertChannels alerts,
    TimeProvider time)
{
    private static readonly TimeSpan Baseline = TimeSpan.FromDays(7);

    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private static readonly TimeSpan Reach = TimeSpan.FromHours(24);

    /// <summary>
    /// Reads the balance, keeps it, and raises the alert where the last hour drained
    /// abnormally or the floor comes within a day at the current rate.
    /// </summary>
    /// <param name="context">The system principal the watch runs as.</param>
    /// <param name="cancellationToken">Abandons the poll.</param>
    /// <returns>What was read, or the failure where the gateway did not answer.</returns>
    /// <exception cref="ArgumentException">The context is not a principal that may monitor.</exception>
    public async ValueTask<Result<decimal>> PollAsync(AccessContext context, CancellationToken cancellationToken)
    {
        _ = Monitoring(context);

        return await PolledAsync(cancellationToken).ConfigureAwait(false);
    }

    // INF-BG-002 AC1, IDN-PRIN-001 AC3 (D-166, 304): the watch runs as a named
    // principal that may monitor, and never as nobody.
    private static SystemPrincipal Monitoring(AccessContext context) =>
        context?.Principal is { } principal && principal.MayRun(SystemOperation.Monitoring)
            ? principal
            : throw new ArgumentException(
                "The watch runs as a system principal that may monitor.",
                nameof(context));

    // INT-SMS-004: the poll alone asks the gateway, before its unit of work begins, and
    // no poll succeeds without a balance read.
    private async ValueTask<Result<decimal>> PolledAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        decimal floor = (await configuration
                .ReadAsync(Settings.AbuseSmsBalanceFloor, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<decimal>(error, ref failure));

        decimal factor = (await configuration
                .ReadAsync(Settings.AbuseSmsDrainFactor, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<decimal>(error, ref failure));

        TimeSpan interval = (await configuration
                .ReadAsync(Settings.AbuseSmsPollInterval, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<decimal>(failure);
        }

        decimal balance = (await sms.BalanceAsync(cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<decimal>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<decimal>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<decimal>(notBegun);
        }

        await readings
            .RecordAsync(new BalanceReading(now, balance), Baseline + interval, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<BalanceReading> taken = await readings
            .SinceAsync(now - Baseline, cancellationToken)
            .ConfigureAwait(false);

        decimal recent = Spent(taken, now - Hour);
        decimal mean = Spent(taken, now - Baseline) / (decimal)(Baseline / Hour);

        if (Drained(balance, floor, recent, mean, factor))
        {
            Result published = await alerts
                .RaiseAsync(
                    Alerts.Of(AlertCondition.SmsBalance, null, now, Details(balance, floor, recent)),
                    cancellationToken)
                .ConfigureAwait(false);

            if (published.Match(() => (Error?)null, error => error) is Error unpublished)
            {
                await work.RollbackAsync().ConfigureAwait(false);

                return Result.Failure<decimal>(unpublished);
            }
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<decimal>(notCommitted);
        }

        return Result.Success(balance);
    }

    /// <summary>
    /// Whether ordinary sends are stopped, which they are once the latest balance a
    /// poll recorded stands at the floor. Alert-class sends do not ask (OPS-ALERT-003).
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>Whether the hard stop is in force.</returns>
    /// <remarks>
    /// AUTH-ABUSE-006, INT-SMS-004: a send is judged inside the transaction that
    /// undertakes it, so the gateway is never asked here, however old the reading, and
    /// until a first balance is recorded the floor refuses nothing.
    /// </remarks>
    public async ValueTask<Result<bool>> BelowFloorAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        decimal floor = (await configuration
                .ReadAsync(Settings.AbuseSmsBalanceFloor, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<decimal>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<bool>(failure);
        }

        BalanceReading? latest = await readings.LatestAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(latest is not null && latest.Balance <= floor);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static bool Drained(decimal balance, decimal floor, decimal recent, decimal mean, decimal factor)
    {
        if (balance <= floor)
        {
            return true;
        }

        if (mean > 0 && recent > factor * mean)
        {
            return true;
        }

        return recent > 0 && (balance - floor) / recent <= (decimal)(Reach / Hour);
    }

    private static decimal Spent(IReadOnlyList<BalanceReading> taken, DateTimeOffset from)
    {
        decimal spent = 0;

        for (int index = 1; index < taken.Count; index++)
        {
            if (taken[index].At < from)
            {
                continue;
            }

            // A top-up raises the balance; only what went out is spend.
            decimal fell = taken[index - 1].Balance - taken[index].Balance;

            if (fell > 0)
            {
                spent += fell;
            }
        }

        return spent;
    }

    private static Dictionary<string, JsonElement> Details(
        decimal balance,
        decimal floor,
        decimal recent) =>
        new Dictionary<string, JsonElement>(capacity: 3, StringComparer.Ordinal)
        {
            ["balance"] = JsonSerializer.SerializeToElement(balance),
            ["floor"] = JsonSerializer.SerializeToElement(floor),
            ["spentLastHour"] = JsonSerializer.SerializeToElement(recent),
        };
}
