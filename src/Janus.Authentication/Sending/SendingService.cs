using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// The one path every message the library sends takes: the named restrictions decide
/// it, the catalogue of the deployment words it, a transport carries it, and only
/// what a transport took is counted.
/// </summary>
/// <param name="configuration">Where the restrictions and the languages come from.</param>
/// <param name="ledger">Where what has been sent is counted.</param>
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
/// INT-SMS-001, INT-GEN-005 and CONV-CONTENT-001. The refusal a restriction produces is the same
/// whether or not the destination belongs to an account: nothing on this path reads
/// the account to decide it.
/// </remarks>
internal sealed class SendingService(
    IConfigurationStore configuration,
    ISendLedger ledger,
    IMessageTemplates templates,
    IMailTransport mail,
    ISmsTransport sms,
    RestrictionKeySuppliers suppliers,
    PhoneSignals signals,
    SmsBalance balance,
    IUnitOfWork work,
    IEvents events,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    /// <summary>
    /// Sends one message, or says why it was not sent.
    /// </summary>
    /// <param name="request">What is to be sent.</param>
    /// <param name="cancellationToken">Abandons the send.</param>
    /// <returns>
    /// The correlation reference the transport took it under, or the failure. A
    /// refusal by a restriction carries <c>retryAt</c>.
    /// </returns>
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

        SendReference reference = (await CarryAsync(request, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<SendReference>(error, ref failure));

        if (failure is not null)
        {
            // A transport that would not take it is an attempt to retry, never a
            // reason to count the send (AUTH-ABUSE-004).
            return Result.Failure<SendReference>(failure);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        await ledger
            .RecordAsync(reference.Fingerprint(), plan.Counted, plan.Spent, now, cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(reference);
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

        IReadOnlyDictionary<RestrictionKey, SendCounter> counters = await ledger
            .CountersAsync([.. keyed.Select(one => one.Key)], cancellationToken)
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

    private async ValueTask<Result<SendReference>> CarryAsync(
        SendRequest request,
        CancellationToken cancellationToken)
    {
        // Which key the catalogue could not answer for is the library's to name; the
        // failure the catalogue itself produced says nothing the operator can act on.
        MessageTemplate? template = templates
            .Find(request.Message, request.Kind, request.Language)
            .Match(found => (MessageTemplate?)found, _ => null);

        if (template is null)
        {
            return Result.Failure<SendReference>(
                Error.From(
                    ErrorCodes.StartupDeclarationMissing,
                    "key",
                    JsonSerializer.SerializeToElement(Settings.NotificationLanguages.Key.ToString())));
        }

        var reference = SendReference.Draw(randomness);
        string body = MessageRendering.Fill(template.Text, request.Values);

        await events
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

        // INT-GEN-005: the payload of an outbound message is built here and nowhere
        // else, so its field set is one thing to read and one thing to test.
        Result carried = request.Kind is SendKind.Email
            ? await mail
                .SendAsync(
                    new MailMessage(
                        request.Destination.Mail,
                        MessageRendering.Fill(template.Subject ?? string.Empty, request.Values),
                        body,
                        reference.Value),
                    cancellationToken)
                .ConfigureAwait(false)
            : await sms
                .SendAsync(
                    new SmsMessage(request.Destination.Phone, body, reference.Value),
                    cancellationToken)
                .ConfigureAwait(false);

        return carried.Match(() => Result.Success(reference), Result.Failure<SendReference>);
    }
}
