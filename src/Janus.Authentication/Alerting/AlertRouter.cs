using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Alerting;

/// <summary>
/// What carries a raised condition to the people who have to see it: email for every
/// condition, SMS for the severe ones, the owner where the deployment says so, and
/// one alert per condition per window rather than one per occurrence.
/// </summary>
/// <param name="configuration">Where the destinations and the window come from.</param>
/// <param name="sending">What carries a message.</param>
/// <param name="ledger">What remembers which conditions already went out.</param>
/// <param name="log">Where an unreachable channel is written down.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <remarks>
/// Implements OPS-ALERT-001 to OPS-ALERT-004. An alert-class send is outside the
/// destination restrictions and outside the gateway hard stop: the volume bound on
/// alerting is deduplication, and a drained account must not be able to silence it.
/// </remarks>
internal sealed class AlertRouter(
    IConfigurationStore configuration,
    SendingService sending,
    IAlertLedger ledger,
    IUnitOfWork work,
    IAlertLog log)
{
    private const string Operator = "operator";

    /// <summary>
    /// Raises one condition to the destinations the deployment configured.
    /// </summary>
    /// <param name="raised">What fired.</param>
    /// <param name="cancellationToken">Abandons the delivery.</param>
    /// <returns>What became of it.</returns>
    /// <exception cref="ArgumentNullException">The alert is absent.</exception>
    public async ValueTask<Result<AlertDelivery>> RaiseAsync(
        AlertRaised raised,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(raised);

        Error? failure = null;

        TimeSpan window = (await configuration
                .ReadAsync(Settings.AlertingDedupeWindow, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        AlertAudience audience = (await AudienceAsync(raised, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<AlertAudience>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<AlertDelivery>(failure);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        bool first = await ledger
            .FirstAsync(Alerts.Deduplication(raised.IdempotencyKey), raised.RaisedAt, window, cancellationToken)
            .ConfigureAwait(false);

        Result<AlertDelivery> delivered = first
            ? await DeliverAsync(raised, audience, cancellationToken).ConfigureAwait(false)
            : Result.Success(new AlertDelivery(0, 0, SmsUnreachable: false, Deduplicated: true));

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return delivered;
    }

    /// <summary>
    /// Carries one condition to a stated audience, which is what a destination change
    /// uses to reach the destinations it is about to replace (OPS-ALERT-004a).
    /// </summary>
    /// <param name="raised">What fired.</param>
    /// <param name="audience">Who is told.</param>
    /// <param name="cancellationToken">Abandons the delivery.</param>
    /// <returns>What became of it.</returns>
    /// <exception cref="ArgumentNullException">The alert or the audience is absent.</exception>
    public async ValueTask<Result<AlertDelivery>> DeliverAsync(
        AlertRaised raised,
        AlertAudience audience,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(raised);
        ArgumentNullException.ThrowIfNull(audience);

        Error? failure = null;

        AlertSeverity threshold = (await configuration
                .ReadAsync(Settings.AlertingSmsSeverityThreshold, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<AlertSeverity>(error, ref failure));

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<AlertDelivery>(failure);
        }

        // An alert about the mail system arriving by mail is a loop, so the phone is
        // asked first and the severity threshold does not hold it back (OPS-ALERT-003).
        bool aboutMail = raised.Condition is AlertCondition.RelayDomainUnregistered;
        bool bySms = aboutMail || raised.Severity >= threshold;

        int sms = 0;

        if (aboutMail)
        {
            sms = await SmsAsync(raised, audience, languages, cancellationToken).ConfigureAwait(false);
        }

        int email = await MailAsync(raised, audience, languages, cancellationToken).ConfigureAwait(false);

        if (!aboutMail)
        {
            // Mail carrying nothing is the failure the second channel exists for,
            // whatever the severity of the condition (OPS-ALERT-003).
            bySms = bySms || email == 0;

            if (bySms)
            {
                sms = await SmsAsync(raised, audience, languages, cancellationToken).ConfigureAwait(false);
            }
        }

        bool unreachable = bySms && sms == 0;

        if (unreachable)
        {
            log.Unreachable(Named(raised.Condition), "sms");
        }

        return Result.Success(new AlertDelivery(email, sms, unreachable, Deduplicated: false));
    }

    /// <summary>
    /// Who one condition goes to, which is the configured destination lists and,
    /// where the deployment says so or the condition is a break-glass use, the owner.
    /// </summary>
    /// <param name="raised">What fired.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The audience.</returns>
    /// <exception cref="ArgumentNullException">The alert is absent.</exception>
    public async ValueTask<Result<AlertAudience>> AudienceAsync(
        AlertRaised raised,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(raised);

        Error? failure = null;

        IReadOnlyList<string> email = (await configuration
                .ReadAsync(Settings.AlertingEmailDestinations, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        IReadOnlyList<string> sms = (await configuration
                .ReadAsync(Settings.AlertingSmsDestinations, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        bool owner = (await configuration
                .ReadAsync(Settings.AlertingOwnerEnabled, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<AlertAudience>(failure);
        }

        // Break-glass use reaches the owner whether or not routine alerts do: the
        // switch exists to spare them noise, not to hide the emergency credential
        // being used (OPS-BOOT-002, OPS-ALERT-004).
        if (!owner && raised.Condition is not AlertCondition.BreakGlassUsed)
        {
            return Result.Success(new AlertAudience(email, sms));
        }

        string ownerEmail = (await configuration
                .ReadAsync(Settings.AlertingOwnerEmail, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<string>(error, ref failure));

        string ownerSms = (await configuration
                .ReadAsync(Settings.AlertingOwnerSms, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<string>(error, ref failure));

        return failure is not null
            ? Result.Failure<AlertAudience>(failure)
            : Result.Success(new AlertAudience([.. email, ownerEmail], [.. sms, ownerSms]));
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static string Named(AlertCondition condition) => Alerts.Key(condition, null);

    private static Dictionary<string, string> Values(AlertRaised raised)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["condition"] = Named(raised.Condition),
            ["raisedAt"] = raised.RaisedAt.ToString("O", CultureInfo.InvariantCulture),
        };

        foreach (KeyValuePair<string, JsonElement> detail in raised.Details)
        {
            values[detail.Key] = detail.Value.ToString();
        }

        return values;
    }

    private async ValueTask<int> MailAsync(
        AlertRaised raised,
        AlertAudience audience,
        IReadOnlyList<string> languages,
        CancellationToken cancellationToken)
    {
        int reached = 0;

        foreach (string destination in audience.Email)
        {
            if (!EmailAddress.TryParse(destination, out EmailAddress address))
            {
                continue;
            }

            if (await CarriedAsync(raised, SendDestination.Of(address), languages, cancellationToken)
                .ConfigureAwait(false))
            {
                reached++;
            }
        }

        return reached;
    }

    private async ValueTask<int> SmsAsync(
        AlertRaised raised,
        AlertAudience audience,
        IReadOnlyList<string> languages,
        CancellationToken cancellationToken)
    {
        int reached = 0;

        foreach (string destination in audience.Sms)
        {
            if (!PhoneNumber.TryParse(destination, out PhoneNumber number))
            {
                continue;
            }

            if (await CarriedAsync(raised, SendDestination.Of(number), languages, cancellationToken)
                .ConfigureAwait(false))
            {
                reached++;
            }
        }

        return reached;
    }

    private async ValueTask<bool> CarriedAsync(
        AlertRaised raised,
        SendDestination destination,
        IReadOnlyList<string> languages,
        CancellationToken cancellationToken)
    {
        bool carried = false;

        // An operator destination belongs to no account, so the language resolves at
        // step three: every language the deployment declared (IDN-ATTR-001).
        foreach (string language in languages)
        {
            Result<SendReference> sent = await sending
                .SendAsync(
                    new SendRequest(
                        destination,
                        MessageKind.Alert,
                        RestrictionPurpose.Notification,
                        Operator,
                        language)
                    {
                        Values = Values(raised),
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            carried = sent.Match(_ => true, _ => false) || carried;
        }

        return carried;
    }
}
