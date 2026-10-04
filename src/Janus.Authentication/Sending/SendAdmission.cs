using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// The judgement of a send: the gateway floor and every named restriction that applies
/// to it, decided with the counter of each of its keys held in the transaction in
/// progress, and the count it holds from its admission.
/// </summary>
/// <param name="configuration">Where the restrictions come from.</param>
/// <param name="ledger">Where what has been admitted is counted.</param>
/// <param name="suppliers">The host-registered key suppliers.</param>
/// <param name="balance">What the gateway account stands at.</param>
/// <remarks>
/// Implements AUTH-ABUSE-004, AUTH-ABUSE-006, LIB-HOST-001 and CONV-DESIGN-003. The
/// refusal a restriction produces is the same whether or not the destination belongs to
/// an account: nothing here reads the account to decide it. A refusal returns before
/// any count is written; a counter created for the judgement is the only write before
/// it. The caller has begun the unit of work the counters are held in.
/// </remarks>
internal sealed class SendAdmission(
    IConfigurationStore configuration,
    ISendLedger ledger,
    RestrictionKeySuppliers suppliers,
    SmsBalance balance)
{
    /// <summary>
    /// Judges one send of one or several messages, all admitted or none.
    /// </summary>
    /// <param name="message">What is undertaken.</param>
    /// <param name="weight">
    /// How many messages the send is: one, or one per declared language for a text
    /// message owed in every one of them.
    /// </param>
    /// <param name="setAside">
    /// The reference of the send where it is judged again at a retry, whose own count
    /// and credit are released before it is judged, or nothing at its first admission.
    /// </param>
    /// <param name="now">The instant of the judgement.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// What each message counts against and spends, in the order they are admitted, or
    /// the refusal: the gateway floor, or <c>auth.restriction.exceeded</c> with
    /// <c>retryAt</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">The message is absent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The weight is not positive.</exception>
    public async ValueTask<Result<IReadOnlyList<SendPlan>>> JudgeAsync(
        OutboundMessage message,
        int weight,
        SendReference? setAside,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(weight);

        // AUTH-ABUSE-006: an alert is exempt from the hard stop, since a low balance
        // that silenced the alerting would turn one outage into a blackout
        // (OPS-ALERT-003).
        if (message.Kind is SendKind.Sms
            && !message.IsAlert
            && (await balance.BelowFloorAsync(cancellationToken).ConfigureAwait(false))
                .Match(below => below ? Error.From(ErrorCodes.SmsBalanceFloor) : null, error => error)
                is Error floored)
        {
            return Result.Failure<IReadOnlyList<SendPlan>>(floored);
        }

        Error? failure = null;

        IReadOnlyList<Restriction> declared = (await configuration
                .ReadAsync(Settings.Restrictions, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<Restriction>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IReadOnlyList<SendPlan>>(failure);
        }

        var keyed = new List<(Restriction Restriction, RestrictionKey Key)>(declared.Count);
        var supplied = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Restriction restriction in declared.Where(one => Restrictions.Applies(one, message)))
        {
            string? value = (await KeyOfAsync(restriction, message, supplied, cancellationToken).ConfigureAwait(false))
                .Match(one => one, error => Held<string?>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<IReadOnlyList<SendPlan>>(failure);
            }

            if (value is not null)
            {
                keyed.Add((restriction, new RestrictionKey(restriction.Name, restriction.Key, value)));
            }
        }

        // AUTH-ABUSE-004 AC6, AC16: the counters are held from here to the end of the
        // caller's transaction, and what a retried send held is set aside under the
        // same hold. What a record is kept for is the restrictions as they now stand,
        // so shortening an interval reaches the sends already counted.
        IReadOnlyDictionary<RestrictionKey, SendCounter> counters = await ledger
            .HoldAsync(
                [.. keyed.Select(one => one.Key)],
                CounterStaleness.Of(declared, now),
                setAside is SendReference retried ? SendReferences.Of(retried) : null,
                cancellationToken)
            .ConfigureAwait(false);

        var counted = new List<SendCount>(keyed.Count);
        var credited = new Dictionary<RestrictionKey, int>();
        var lifts = new List<DateTimeOffset>();

        foreach ((Restriction restriction, RestrictionKey key) in keyed)
        {
            SendCounter counter = counters.TryGetValue(key, out SendCounter? standing)
                ? standing
                : SendCounter.Empty;

            counted.Add(new SendCount(key, Restrictions.Retain(restriction)));

            if (Restrictions.Lift(restriction, counter.Sends, now, weight) is not DateTimeOffset lift)
            {
                continue;
            }

            // Credit support granted is spent one message at a time, by the messages
            // the buckets have no room for; once it is gone the key is refused again.
            int over = Restrictions.Over(restriction, counter.Sends, now, weight);

            if (counter.Credit >= over)
            {
                credited[key] = over;
                continue;
            }

            lifts.Add(lift);
        }

        if (lifts.Count > 0)
        {
            return Result.Failure<IReadOnlyList<SendPlan>>(Exceeded(lifts.Min()));
        }

        var plans = new List<SendPlan>(weight);

        for (int index = 0; index < weight; index++)
        {
            int rest = weight - index;

            plans.Add(new SendPlan(
                counted,
                [.. credited.Where(one => one.Value >= rest).Select(one => one.Key)]));
        }

        return Result.Success<IReadOnlyList<SendPlan>>(plans);
    }

    /// <summary>
    /// Counts one admitted message from its admission.
    /// </summary>
    /// <param name="reference">The reference it is counted under.</param>
    /// <param name="plan">What it counts against and spends.</param>
    /// <param name="now">The instant of the admission.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of counting it.</returns>
    /// <exception cref="ArgumentNullException">The plan is absent.</exception>
    public ValueTask CountAsync(
        SendReference reference,
        SendPlan plan,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return ledger.RecordAsync(SendReferences.Of(reference), plan.Counted, plan.Spent, now, cancellationToken);
    }

    /// <summary>
    /// Releases the count and the credit of a send that failed for good.
    /// </summary>
    /// <param name="reference">The reference it was counted under.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of releasing it.</returns>
    public async ValueTask ReleaseAsync(SendReference reference, CancellationToken cancellationToken) =>
        _ = await ledger.ReleaseAsync(SendReferences.Of(reference), cancellationToken).ConfigureAwait(false);

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static Error Exceeded(DateTimeOffset retryAt) =>
        Error.From(
            ErrorCodes.RestrictionExceeded,
            "retryAt",
            JsonSerializer.SerializeToElement(retryAt));

    // LIB-HOST-001 AC5: a host's key is asked for once in one judgement of a send,
    // however many of the restrictions that apply count under it, and never where none
    // that applies does.
    private async ValueTask<Result<string?>> KeyOfAsync(
        Restriction restriction,
        OutboundMessage message,
        Dictionary<string, string> supplied,
        CancellationToken cancellationToken)
    {
        switch (restriction.Key)
        {
            case RestrictionKeyKind.Destination:
                return Result.Success<string?>(message.Destination.Canonical);
            case RestrictionKeyKind.Source:
                // A send no request asked for carries no source, and no source
                // restriction counts it (chapter 10 section 5.14).
                return Result.Success(message.Source);
            case RestrictionKeyKind.Global:
                return Result.Success<string?>(restriction.Name);
            case RestrictionKeyKind.Account:
                // A send with no account behind it has no account key to count under.
                return Result.Success(message.Subject?.ToString());
            default:
                break;
        }

        if (restriction.HostKeyName is null
            || !suppliers.TryFind(restriction.HostKeyName, out RestrictionKeySupplier supplier))
        {
            return Result.Failure<string?>(
                Error.From(
                    ErrorCodes.StartupDeclarationMissing,
                    "supplier",
                    JsonSerializer.SerializeToElement(restriction.HostKeyName ?? restriction.Name)));
        }

        if (!supplied.TryGetValue(restriction.HostKeyName, out string? key))
        {
            key = await supplier.Key(message.Context, cancellationToken).ConfigureAwait(false);
            supplied[restriction.HostKeyName] = key;
        }

        return Result.Success<string?>(key);
    }
}
