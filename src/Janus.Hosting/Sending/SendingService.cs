using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a correlation reference is drawn from.</param>
/// <remarks>
/// Implements AUTH-ABUSE-004, AUTH-ABUSE-002, AUTH-ABUSE-006, AUTH-FACT-002b,
/// INT-SMS-001, INT-GEN-005, IDN-ATTR-001, CONV-CONTENT-001 and D-022. The refusal a restriction
/// produces is the same whether or not the destination belongs to an account: nothing
/// on this path reads the account to decide it.
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
    TimeProvider time,
    RandomNumberGenerator randomness) : INotificationHandler
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public async ValueTask<Result<SendReference>> SendAsync(
        SendRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        DateTimeOffset now = time.GetUtcNow();
        Error? failure = null;

        // An alert is exempt from the hard stop: a low balance that silenced the
        // alerting would turn one outage into a blackout (OPS-ALERT-003).
        if (request.Kind is SendKind.Sms && !request.IsAlert)
        {
            bool below = (await balance.BelowFloorAsync(cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Held<bool>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<SendReference>(failure);
            }

            if (below)
            {
                return Result.Failure<SendReference>(Error.From(ErrorCodes.SmsBalanceFloor));
            }
        }

        SendPlan plan = (await PlanAsync(request, now, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<SendPlan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SendReference>(failure);
        }

        if (plan.RetryAt is DateTimeOffset retryAt)
        {
            return Result.Failure<SendReference>(
                Error.From(
                    ErrorCodes.RestrictionExceeded,
                    "retryAt",
                    JsonSerializer.SerializeToElement(retryAt)));
        }

        // AUTH-FACT-002b: the restricted entries ride a number, so what the deployment
        // knows about the number is considered here, once the restrictions have let the
        // send through and before a transport takes it.
        await signals.ConsiderAsync(request, cancellationToken).ConfigureAwait(false);

        // D-022: the message is written in the transaction that made it necessary, so
        // that one undertaken by an operation which then fails is never sent, and one
        // undertaken by an operation which succeeds survives the process.
        var delivery = SendDelivery.Of(request, now);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        await outbox.AddAsync(delivery, cancellationToken).ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        SendDelivery written = await outbox.FindAsync(delivery.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The message just written has no row.");

        IReadOnlyList<MessageTemplate> worded = (await WordedAsync(written.Requested, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<MessageTemplate>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SendReference>(failure);
        }

        var taken = new List<SendReference>(worded.Count);

        foreach (MessageTemplate template in worded)
        {
            SendReference reference = (await CarryAsync(written.Requested, template, cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => Held<SendReference>(error, ref failure));

            if (failure is not null)
            {
                break;
            }

            taken.Add(reference);
        }

        if (failure is not null && taken.Count == 0)
        {
            // A transport that would not take it is an attempt to retry, never a
            // reason to count the send (AUTH-ABUSE-004). The row stays as it was
            // recorded, which is what the publisher retries from.
            return Result.Failure<SendReference>(failure);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        // IDN-PRIN-003: a message a transport has taken is spent, and what is spent is
        // removed rather than kept as a record of where somebody was written to. Where
        // a transport refused one of its languages the row is not yet spent.
        if (failure is null)
        {
            await outbox.RemoveAsync(delivery.Id, cancellationToken).ConfigureAwait(false);
        }

        // AUTH-ABUSE-004 AC1 counts messages: each one a transport took counts once,
        // under its own reference, so a delivery report releases that one alone.
        foreach (SendReference reference in taken)
        {
            await ledger
                .RecordAsync(SendReferences.Of(reference), plan.Counted, plan.Spent, now, cancellationToken)
                .ConfigureAwait(false);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return failure is null
            ? Result.Success(taken[0])
            : Result.Failure<SendReference>(failure);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
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

    private async ValueTask<Result<IReadOnlyList<MessageTemplate>>> WordedAsync(
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
            return Result.Failure<IReadOnlyList<MessageTemplate>>(failure);
        }

        var written = new List<MessageTemplate>(languages.Count);

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
                return Result.Failure<IReadOnlyList<MessageTemplate>>(Undeclared());
            }

            written.Add(template);
        }

        return written.Count == 0
            ? Result.Failure<IReadOnlyList<MessageTemplate>>(Undeclared())
            : Result.Success<IReadOnlyList<MessageTemplate>>(written);
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

    private static Error Undeclared() =>
        Error.From(
            ErrorCodes.StartupDeclarationMissing,
            "key",
            JsonSerializer.SerializeToElement(Settings.NotificationLanguages.Key.ToString()));
}
