using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// The answer to a recovery, sign-in link or email code asked for an address no
/// account holds: the address itself is told, once per window, and the response the
/// asker sees is the one an existing address produces.
/// </summary>
/// <param name="configuration">Where the window and the alert threshold come from.</param>
/// <param name="sending">What carries the message.</param>
/// <param name="ledger">What remembers which addresses were told.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="alerts">Where the enumeration-probe alert goes.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-ABUSE-003 and OPS-ALERT-001. The real owner gets their answer and
/// an enumerating attacker learns nothing, because they do not hold the mailbox. The
/// message names no requester: nothing about the asker crosses into it.
/// </remarks>
internal sealed class NonExistenceNotice(
    IConfigurationStore configuration,
    INotificationHandler sending,
    INoticeLedger ledger,
    IUnitOfWork work,
    IAlertChannels alerts,
    TimeProvider time)
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    /// <summary>
    /// Tells one address that no account holds it, unless it has been told inside
    /// the window.
    /// </summary>
    /// <param name="destination">The address that was asked about.</param>
    /// <param name="source">Where the request came from.</param>
    /// <param name="language">
    /// The locale of the request that asked, which is the only language known for an
    /// address no account holds (IDN-ATTR-001).
    /// </param>
    /// <param name="cancellationToken">Abandons the send.</param>
    /// <returns>Whether a notice went out, or the failure where it could not.</returns>
    public async ValueTask<Result<bool>> TellAsync(
        EmailAddress destination,
        string source,
        string language,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan window = (await configuration
                .ReadAsync(Settings.AbuseNonexistentWindow, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        int threshold = (await configuration
                .ReadAsync(Settings.AlertingNonexistentThreshold, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<bool>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (!await ledger
            .FirstAsync(destination.Value, now, window, cancellationToken)
            .ConfigureAwait(false))
        {
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success(false);
        }

        // The message carries no value at all: a template that named the asker would
        // turn the notice itself into the disclosure it exists to prevent.
        Result<SendReference> sent = await sending
            .SendAsync(
                new SendRequest(
                    SendDestination.Of(destination),
                    MessageKind.NoAccount,
                    RestrictionPurpose.Notification,
                    source,
                    RecipientLanguage.Found(language, languages)),
                cancellationToken)
            .ConfigureAwait(false);

        if (sent.Match(_ => (Error?)null, error => error) is Error refused)
        {
            return Result.Failure<bool>(refused);
        }

        int recent = await ledger.SinceAsync(now - Hour, cancellationToken).ConfigureAwait(false);

        if (recent > threshold)
        {
            Result published = await alerts
                .RaiseAsync(Alerts.Of(AlertCondition.NonexistentNoticeRate, null, now), cancellationToken)
                .ConfigureAwait(false);

            if (published.Match(() => (Error?)null, error => error) is Error unpublished)
            {
                return Result.Failure<bool>(unpublished);
            }
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(true);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
