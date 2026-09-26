using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Alerting;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Sending;

/// <summary>
/// The one path every message the library sends takes: the named restrictions decide
/// it, the catalogue of the deployment words it, a transport carries it, and only
/// what a transport took is counted.
/// </summary>
/// <param name="configuration">Where the restrictions and the languages come from.</param>
/// <param name="ledger">Where what has been sent is counted.</param>
/// <param name="outbox">Where a message undertaken is written until it is carried.</param>
/// <param name="templates">Where the words come from.</param>
/// <param name="mail">What carries a mail.</param>
/// <param name="sms">What carries a text message.</param>
/// <param name="suppliers">The host-registered key suppliers.</param>
/// <param name="signals">What is known about a number a restricted factor goes to.</param>
/// <param name="balance">What the gateway account stands at.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="events">Where the emitted events go.</param>
/// <param name="alerts">Where a spent retry budget's alert goes.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a correlation reference and the retry jitter are drawn from.</param>
/// <remarks>
/// Implements AUTH-ABUSE-004, AUTH-ABUSE-002, AUTH-ABUSE-006, AUTH-FACT-002b,
/// INT-SMS-001, INT-GEN-005, IDN-ATTR-001, CONV-CONTENT-001, D-022 and INF-BG-001. The
/// refusal a restriction produces is the same whether or not the destination belongs to
/// an account: nothing on this path reads the account to decide it. A message no
/// transport took is carried again under <c>outbox.retry.*</c> in the languages still
/// owed, until its budget is spent and <c>degradation</c> is raised.
/// </remarks>
internal sealed class SendingService(
    IConfigurationStore configuration,
    ISendLedger ledger,
    ISendOutbox outbox,
    IMessageTemplates templates,
    IMailTransport mail,
    ISmsTransport sms,
    RestrictionKeySuppliers suppliers,
    PhoneSignals signals,
    SmsBalance balance,
    IUnitOfWork work,
    IEvents events,
    IAlertChannels alerts,
    TimeProvider time,
    RandomNumberGenerator randomness) : INotificationHandler
{
    // A pass never holds more than this many in memory; the rest wait for the next.
    private const int Batch = 100;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public async ValueTask<Result<SendReference>> SendAsync(
        SendRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        DateTimeOffset now = time.GetUtcNow();
        Error? failure = null;

        if (await FlooredAsync(request, cancellationToken).ConfigureAwait(false) is Error floored)
        {
            return Result.Failure<SendReference>(floored);
        }

        SendPlan plan = (await PlanAsync(request, now, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<SendPlan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SendReference>(failure);
        }

        if (plan.RetryAt is DateTimeOffset retryAt)
        {
            return Result.Failure<SendReference>(Exceeded(retryAt));
        }

        Schedule schedule = (await ScheduleAsync(cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<Schedule>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SendReference>(failure);
        }

        // AUTH-FACT-002b: the restricted entries ride a number, so what the deployment
        // knows about the number is considered here, once the restrictions have let the
        // send through and before a transport takes it.
        await signals.ConsiderAsync(request, cancellationToken).ConfigureAwait(false);

        // D-022: the message is written in the transaction that made it necessary, so
        // that one undertaken by an operation which then fails is never sent, and one
        // undertaken by an operation which succeeds survives the process. The
        // publisher leaves it to this path for the first retry delay.
        var delivery = SendDelivery.Of(request, now, schedule.Initial);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        await outbox.AddAsync(delivery, cancellationToken).ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        SendDelivery written = await outbox.FindAsync(delivery.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The message just written has no row.");

        Attempt attempt = await AttemptAsync(written, plan, cancellationToken).ConfigureAwait(false);

        if ((await SettleAsync(written, attempt, schedule, now, cancellationToken).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error) is Error unsettled)
        {
            return Result.Failure<SendReference>(unsettled);
        }

        return attempt.Failure is null
            ? Result.Success(attempt.References[0])
            : Result.Failure<SendReference>(attempt.Failure);
    }

    /// <summary>
    /// Runs one pass of the publisher over the messages no transport has taken in full.
    /// </summary>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many messages are now carried in full, or the failure that stopped the pass.</returns>
    public async ValueTask<Result<int>> RetryAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        IReadOnlyList<SendDelivery> due = await outbox.DueAsync(now, Batch, cancellationToken)
            .ConfigureAwait(false);

        if (due.Count == 0)
        {
            return Result.Success(0);
        }

        Error? failure = null;

        Schedule schedule = (await ScheduleAsync(cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<Schedule>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<int>(failure);
        }

        int carried = 0;

        foreach (SendDelivery delivery in due)
        {
            Attempt attempt = await RetriedAsync(delivery, now, cancellationToken).ConfigureAwait(false);

            if ((await SettleAsync(delivery, attempt, schedule, now, cancellationToken).ConfigureAwait(false))
                .Match(() => (Error?)null, error => error) is Error unsettled)
            {
                return Result.Failure<int>(unsettled);
            }

            if (attempt.Failure is null)
            {
                carried++;
            }
        }

        return Result.Success(carried);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // The message is named by its identifier and what it is for: the destination is
    // personal data and an alert travels to channels that are not the recipient's.
    private static Dictionary<string, JsonElement> Exhausted(SendDelivery delivery) =>
        new(capacity: 4, StringComparer.Ordinal)
        {
            ["delivery"] = JsonSerializer.SerializeToElement(delivery.Id.ToString()),
            ["message"] = JsonSerializer.SerializeToElement(WrittenName.Of(delivery.Requested.Message)),
            ["channel"] = JsonSerializer.SerializeToElement(WrittenName.Of(delivery.Requested.Kind)),
            ["attempts"] = JsonSerializer.SerializeToElement(delivery.Attempts),
        };

    // An alert is exempt from the hard stop: a low balance that silenced the alerting
    // would turn one outage into a blackout (OPS-ALERT-003).
    private async ValueTask<Error?> FlooredAsync(SendRequest request, CancellationToken cancellationToken)
    {
        if (request.Kind is not SendKind.Sms || request.IsAlert)
        {
            return null;
        }

        return (await balance.BelowFloorAsync(cancellationToken).ConfigureAwait(false))
            .Match(below => below ? Error.From(ErrorCodes.SmsBalanceFloor) : null, error => error);
    }

    // AUTH-ABUSE-004: a refused delivery counts against no bucket, so while a
    // transport is down every send is admitted. A retry is judged again as the
    // restrictions now stand, and one they refuse waits as any refused attempt does,
    // so a transport coming back does not carry at once what the restrictions would
    // have held. The gateway floor holds a retry as it holds any text message.
    private async ValueTask<Attempt> RetriedAsync(
        SendDelivery delivery,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await FlooredAsync(delivery.Requested, cancellationToken).ConfigureAwait(false) is Error floored)
        {
            return Attempt.Refused(floored);
        }

        Error? failure = null;

        SendPlan plan = (await PlanAsync(delivery.Requested, now, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<SendPlan>(error, ref failure));

        if (failure is not null)
        {
            return Attempt.Refused(failure);
        }

        return plan.RetryAt is DateTimeOffset retryAt
            ? Attempt.Refused(Exceeded(retryAt))
            : await AttemptAsync(delivery, plan, cancellationToken).ConfigureAwait(false);
    }

    // One attempt carries the languages no transport has taken yet, in the order the
    // deployment declares them, and stops at the first a transport refuses.
    private async ValueTask<Attempt> AttemptAsync(
        SendDelivery delivery,
        SendPlan plan,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        IReadOnlyList<Worded> worded = (await WordedAsync(delivery.Requested, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<Worded>>(error, ref failure));

        if (failure is not null)
        {
            return Attempt.Refused(failure);
        }

        var references = new List<SendReference>(worded.Count);
        var languages = new List<string>(worded.Count);

        foreach (Worded one in worded.Where(one => !delivery.Taken.Contains(one.Language, StringComparer.Ordinal)))
        {
            SendReference reference = (await CarryAsync(delivery.Requested, one.Template, cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => Held<SendReference>(error, ref failure));

            if (failure is not null)
            {
                break;
            }

            references.Add(reference);
            languages.Add(one.Language);
        }

        return new Attempt(plan, references, languages, failure);
    }

    // What an attempt made of a message is recorded in one transaction with what it
    // counts, and a spent budget with its alert or not at all.
    private async ValueTask<Result> SettleAsync(
        SendDelivery delivery,
        Attempt attempt,
        Schedule schedule,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        // IDN-PRIN-003: a message a transport has taken is spent, and what is spent is
        // removed rather than kept as a record of where somebody was written to. A
        // message whose budget is spent goes the same way once its alert is raised:
        // the alert, not the row, is the signal that it was never carried.
        if (attempt.Failure is null)
        {
            await outbox.RemoveAsync(delivery.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            SendDelivery refused = delivery
                .Carried(attempt.Languages)
                .Refused(now, schedule.Initial, schedule.Factor, Jitter());

            if (refused.Attempts < schedule.MaxAttempts)
            {
                await outbox.RecordAsync(refused, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await outbox.RemoveAsync(delivery.Id, cancellationToken).ConfigureAwait(false);

                if ((await alerts
                        .RaiseAsync(
                            Alerts.Of(
                                AlertCondition.Degradation,
                                "send:" + WrittenName.Of(delivery.Requested.Kind),
                                now,
                                Exhausted(refused)),
                            cancellationToken)
                        .ConfigureAwait(false))
                    .Match(() => (Error?)null, error => error) is Error unalerted)
                {
                    return Result.Failure(unalerted);
                }
            }
        }

        // AUTH-ABUSE-004 AC1 counts messages: each one a transport took counts once,
        // under its own reference, so a delivery report releases that one alone. A
        // refused or failed attempt counts against no bucket.
        foreach (SendReference reference in attempt.References)
        {
            await ledger
                .RecordAsync(
                    SendReferences.Of(reference),
                    attempt.Plan.Counted,
                    attempt.Plan.Spent,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    // Full jitter: the delay is a uniform fraction of the computed backoff, so two
    // messages refused together are not carried again together.
    private double Jitter()
    {
        Span<byte> bytes = stackalloc byte[2];

        randomness.GetBytes(bytes);

        return BinaryPrimitives.ReadUInt16LittleEndian(bytes) / (double)ushort.MaxValue;
    }

    private async ValueTask<Result<Schedule>> ScheduleAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan initial = (await configuration
                .ReadAsync(Settings.OutboxRetryInitial, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        decimal factor = (await configuration
                .ReadAsync(Settings.OutboxRetryFactor, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<decimal>(error, ref failure));

        int attempts = (await configuration
                .ReadAsync(Settings.OutboxRetryMaxAttempts, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        return failure is null
            ? Result.Success(new Schedule(initial, factor, attempts))
            : Result.Failure<Schedule>(failure);
    }

    private async ValueTask<Result<SendPlan>> PlanAsync(
        SendRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        IReadOnlyList<Restriction> declared = (await configuration
                .ReadAsync(Settings.Restrictions, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<Restriction>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SendPlan>(failure);
        }

        var keyed = new List<(Restriction Restriction, RestrictionKey Key)>(declared.Count);

        foreach (Restriction restriction in declared.Where(one => Restrictions.Applies(one, request)))
        {
            string? value = (await KeyOfAsync(restriction, request, cancellationToken).ConfigureAwait(false))
                .Match(one => one, error => Held<string?>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<SendPlan>(failure);
            }

            if (value is not null)
            {
                keyed.Add((restriction, new RestrictionKey(restriction.Name, value)));
            }
        }

        // AUTH-ABUSE-004 AC6: what the record is kept for is the restrictions as they
        // now stand, so the sweep reads the declaration and not what a send was written
        // under; shortening an interval reaches the sends already counted.
        TimeSpan longest = declared.Count == 0
            ? TimeSpan.Zero
            : declared.Max(Restrictions.Retain);

        IReadOnlyDictionary<RestrictionKey, SendCounter> counters = await ledger
            .CountersAsync([.. keyed.Select(one => one.Key)], now - longest, cancellationToken)
            .ConfigureAwait(false);

        var counted = new List<SendCount>(keyed.Count);
        var spent = new List<RestrictionKey>();
        var lifts = new List<DateTimeOffset>();

        foreach ((Restriction restriction, RestrictionKey key) in keyed)
        {
            SendCounter counter = counters.TryGetValue(key, out SendCounter? standing)
                ? standing
                : SendCounter.Empty;

            counted.Add(new SendCount(key, Restrictions.Retain(restriction)));

            if (Restrictions.Lift(restriction, counter.Sends, now) is not DateTimeOffset lift)
            {
                continue;
            }

            // Credit support granted is spent one send at a time; once it is gone the
            // key is refused again (AUTH-ABUSE-004).
            if (counter.Credit > 0)
            {
                spent.Add(key);
                continue;
            }

            lifts.Add(lift);
        }

        return Result.Success(new SendPlan(counted, spent, lifts.Count == 0 ? null : lifts.Min()));
    }

    private async ValueTask<Result<string?>> KeyOfAsync(
        Restriction restriction,
        SendRequest request,
        CancellationToken cancellationToken)
    {
        switch (restriction.Key)
        {
            case RestrictionKeyKind.Destination:
                return Result.Success<string?>(request.Destination.Canonical);
            case RestrictionKeyKind.Source:
                return Result.Success<string?>(request.Source);
            case RestrictionKeyKind.Global:
                return Result.Success<string?>(restriction.Name);
            case RestrictionKeyKind.Account:
                // A send with no account behind it has no account key to count under.
                return Result.Success(request.Subject?.ToString());
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

        return Result.Success<string?>(
            await supplier.Key(request.Context, cancellationToken).ConfigureAwait(false));
    }

    private async ValueTask<Result<IReadOnlyList<Worded>>> WordedAsync(
        SendRequest request,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        // IDN-ATTR-001: where no language of the recipient's is known, the message goes
        // out in every language the deployment declares, each as the deployment's own
        // template for it. The restrictions judged the request once; each language
        // carried is a message of its own and counts as one.
        IReadOnlyList<string> languages = request.Language is string named
            ? [named]
            : (await configuration
                    .ReadAsync(Settings.NotificationLanguages, cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IReadOnlyList<Worded>>(failure);
        }

        var written = new List<Worded>(languages.Count);

        foreach (string language in languages)
        {
            // Which key the catalogue could not answer for is the library's to name;
            // the failure the catalogue itself produced says nothing the operator can
            // act on.
            if (templates
                    .Find(request.Message, request.Kind, language)
                    .Match(found => (MessageTemplate?)found, _ => null)
                is not MessageTemplate template)
            {
                return Result.Failure<IReadOnlyList<Worded>>(Undeclared());
            }

            written.Add(new Worded(language, template));
        }

        return written.Count == 0
            ? Result.Failure<IReadOnlyList<Worded>>(Undeclared())
            : Result.Success<IReadOnlyList<Worded>>(written);
    }

    private async ValueTask<Result<SendReference>> CarryAsync(
        SendRequest request,
        MessageTemplate template,
        CancellationToken cancellationToken)
    {
        var reference = SendReference.Draw(randomness);

        Result published = await events
            .PublishAsync(
                new NotificationRequested(
                    time.GetUtcNow(),
                    reference.Value,
                    request.Message,
                    request.Kind)
                {
                    Subject = request.Subject,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            return Result.Failure<SendReference>(unpublished);
        }

        Result carried = await CarriedAsync(request, template, reference, cancellationToken)
            .ConfigureAwait(false);

        return carried.Match(
            () => Result.Success(reference),
            Result.Failure<SendReference>);
    }

    // INT-GEN-005: the payload of an outbound message is built here and nowhere else,
    // so its field set is one thing to read and one thing to test.
    private ValueTask<Result> CarriedAsync(
        SendRequest request,
        MessageTemplate template,
        SendReference reference,
        CancellationToken cancellationToken)
    {
        string body = MessageRendering.Fill(template.Text, request.Values);

        return request.Kind is SendKind.Email
            ? mail.SendAsync(
                new MailMessage(
                    request.Destination.Mail,
                    MessageRendering.Fill(template.Subject ?? string.Empty, request.Values),
                    body,
                    reference.Value),
                cancellationToken)
            : sms.SendAsync(
                new SmsMessage(request.Destination.Phone, body, reference.Value),
                cancellationToken);
    }

    private static Error Exceeded(DateTimeOffset retryAt) =>
        Error.From(
            ErrorCodes.RestrictionExceeded,
            "retryAt",
            JsonSerializer.SerializeToElement(retryAt));

    private static Error Undeclared() =>
        Error.From(
            ErrorCodes.StartupDeclarationMissing,
            "key",
            JsonSerializer.SerializeToElement(Settings.NotificationLanguages.Key.ToString()));

    private sealed record Schedule(TimeSpan Initial, decimal Factor, int MaxAttempts);

    // One language of a message and the deployment's template for it.
    private sealed record Worded(string Language, MessageTemplate Template);

    // What one attempt made of a message: the messages a transport took and in which
    // languages, what they count against, and what stopped the rest.
    private sealed record Attempt(
        SendPlan Plan,
        IReadOnlyList<SendReference> References,
        IReadOnlyList<string> Languages,
        Error? Failure)
    {
        // An attempt stopped before any transport was asked carries nothing, so
        // nothing it would have counted against is known or needed.
        private static readonly SendPlan Unplanned = new([], [], RetryAt: null);

        public static Attempt Refused(Error failure) => new(Unplanned, [], [], failure);
    }
}
