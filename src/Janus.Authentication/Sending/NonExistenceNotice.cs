using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// The answer to a recovery, sign-in link or email code whose message is not going
/// out: an address no account holds is told so, once per window, and every such ask
/// counts against the sending restrictions as the message would have, so the response
/// the asker sees is the one an existing address produces.
/// </summary>
/// <param name="configuration">Where the window and the alert threshold come from.</param>
/// <param name="sending">What carries the message.</param>
/// <param name="restrictions">What an ask that sends nothing draws on.</param>
/// <param name="ledger">What remembers which addresses were told.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="alerts">Where the enumeration-probe alert goes.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-ABUSE-002, AUTH-ABUSE-003, AUTH-ABUSE-004 and OPS-ALERT-001. The real
/// owner gets their answer and an enumerating attacker learns nothing, because they do
/// not hold the mailbox. The message names no requester: nothing about the asker
/// crosses into it. The notice is judged as the message the ask asked for, under that
/// message's purpose, and an ask the window has already answered draws on the same
/// restrictions, so a restriction refuses a second ask inside its interval whether or
/// not an account holds the address (BFF-ABUSE-001, BFF-ABUSE-002).
/// </remarks>
internal sealed class NonExistenceNotice(
    IConfigurationStore configuration,
    INotificationHandler sending,
    ISendingRestrictions restrictions,
    INoticeLedger ledger,
    IUnitOfWork work,
    IAlertChannels alerts,
    TimeProvider time)
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    /// <summary>
    /// Answers one ask whose message is not going out.
    /// </summary>
    /// <param name="destination">The address or number that was asked about.</param>
    /// <param name="message">The message the ask asked for.</param>
    /// <param name="purpose">Which restrictions that message answers to.</param>
    /// <param name="source">Where the request came from.</param>
    /// <param name="language">
    /// The locale of the request that asked, which is the only language known for an
    /// address no account holds (IDN-ATTR-001).
    /// </param>
    /// <param name="unheld">
    /// Whether no account holds the destination, which alone is told so; an account
    /// the ask cannot reach is answered by the restrictions and told nothing.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the refusal the message would have met.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result> AnswerAsync(
        SendDestination destination,
        MessageKind message,
        RestrictionPurpose purpose,
        string source,
        string language,
        bool unheld,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(source);

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
            return Result.Failure(failure);
        }

        // Whoever holds the address, the ask is judged as the message it asked for,
        // in the language an address no account holds is written to.
        var asked = new SendRequest(
            destination,
            message,
            purpose,
            source,
            RecipientLanguage.Found(language, languages));

        if (unheld && destination.Kind is SendKind.Email)
        {
            DateTimeOffset now = time.GetUtcNow();

            await work.BeginAsync(cancellationToken).ConfigureAwait(false);

            if (await ledger
                .FirstAsync(destination.Canonical, now, window, cancellationToken)
                .ConfigureAwait(false))
            {
                return await ToldAsync(asked, now, threshold, cancellationToken).ConfigureAwait(false);
            }

            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return await restrictions.DrawAsync(asked, cancellationToken).ConfigureAwait(false);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // The notice is the ask's message, so it counts as the message would have and a
    // refusal is the refusal the message would have met; one refused leaves the window
    // unmarked, so the next ask tells the address. The message carries no value at
    // all: a template that named the asker would turn the notice itself into the
    // disclosure it exists to prevent.
    private async ValueTask<Result> ToldAsync(
        SendRequest asked,
        DateTimeOffset now,
        int threshold,
        CancellationToken cancellationToken)
    {
        Result<SendReference> sent = await sending
            .SendAsync(asked with { Message = MessageKind.NoAccount }, cancellationToken)
            .ConfigureAwait(false);

        if (sent.Match(_ => (Error?)null, error => error) is Error refused)
        {
            return Result.Failure(refused);
        }

        int recent = await ledger.SinceAsync(now - Hour, cancellationToken).ConfigureAwait(false);

        if (recent > threshold)
        {
            Result published = await alerts
                .RaiseAsync(Alerts.Of(AlertCondition.NonexistentNoticeRate, null, now), cancellationToken)
                .ConfigureAwait(false);

            if (published.Match(() => (Error?)null, error => error) is Error unpublished)
            {
                return Result.Failure(unpublished);
            }
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
