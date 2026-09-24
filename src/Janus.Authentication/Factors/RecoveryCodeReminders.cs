using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Factors;

/// <summary>
/// The one reminder a set of recovery codes gets once it is older than
/// <c>recovery.codes.reminder</c>, sent to the account's security-notice set.
/// </summary>
/// <param name="sets">Where the sets are read and their reminder recorded.</param>
/// <param name="identifiers">Where the channels a reminder reaches are read.</param>
/// <param name="sending">Where a reminder goes out.</param>
/// <param name="configuration">Where the reminder age and the languages come from.</param>
/// <param name="work">The one transaction each reminder is recorded and written in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-FACT-008 AC5 and INF-BG-001. A set is marked reminded in the
/// transaction that writes its notices (D-022), so a pass that fails leaves the set
/// owed its reminder and a pass that succeeds is never repeated for the same set.
/// </remarks>
internal sealed class RecoveryCodeReminders(
    IRecoveryCodeStore sets,
    IIdentifierDirectory identifiers,
    INotificationHandler sending,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time)
{
    // A pass never holds more than this many accounts in memory at once.
    private const int Batch = 100;

    // The reminder is asked for by no request, so it counts against the deployment
    // itself and not against a person's address.
    private const string Origin = "recovery.codes.reminder";

    /// <summary>
    /// Reminds the owner of every set that is owed its reminder.
    /// </summary>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many sets were reminded of, or the failure that stopped the pass.</returns>
    public async ValueTask<Result<int>> RemindAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan after = (await configuration
                .ReadAsync(Settings.RecoveryCodesReminder, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<int>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();
        int reminded = 0;

        while (true)
        {
            IReadOnlyList<SubjectId> due = await sets
                .DueReminderAsync(now - after, Batch, cancellationToken)
                .ConfigureAwait(false);

            int page = 0;

            foreach (SubjectId subject in due)
            {
                page += await RemindedAsync(subject, now, after, cancellationToken).ConfigureAwait(false);
            }

            reminded += page;

            if (due.Count < Batch || page == 0)
            {
                return Result.Success(reminded);
            }
        }
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static SendDestination? Destination(HeldIdentifier identifier) =>
        identifier.Kind switch
        {
            IdentifierKind.Email => EmailAddress.TryParse(identifier.Canonical, out EmailAddress address)
                ? SendDestination.Of(address)
                : null,
            IdentifierKind.Phone => PhoneNumber.TryParse(identifier.Canonical, out PhoneNumber number)
                ? SendDestination.Of(number)
                : null,
            _ => null,
        };

    private async ValueTask<int> RemindedAsync(
        SubjectId subject,
        DateTimeOffset now,
        TimeSpan after,
        CancellationToken cancellationToken)
    {
        RecoveryCodeSet? held = await sets.FindAsync(subject, cancellationToken).ConfigureAwait(false);

        if (held is null || !held.RemindsAt(now, after))
        {
            return 0;
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        held.Reminded(now);
        await sets.RecordAsync(held, cancellationToken).ConfigureAwait(false);
        _ = await TellAsync(subject, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return 1;
    }

    // Every channel of the security-notice set hears of it; a channel that refuses
    // the notice does not hold back the others or the set's one reminder.
    private async ValueTask<int> TellAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        string? language = await LanguageAsync(subject, cancellationToken).ConfigureAwait(false);
        int told = 0;

        foreach (HeldIdentifier identifier in held.NoticeSet)
        {
            if (Destination(identifier) is not SendDestination destination)
            {
                continue;
            }

            Result<SendReference> sent = await sending
                .SendAsync(
                    new SendRequest(
                        destination,
                        MessageKind.RecoveryCodesReminder,
                        RestrictionPurpose.Notification,
                        Origin,
                        language)
                    {
                        Subject = subject,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            told += sent.Match(_ => 1, _ => 0);
        }

        return told;
    }

    private async ValueTask<string?> LanguageAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        string? settled = await identifiers.LanguageAsync(subject, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(read => read, _ => (IReadOnlyList<string>)[]);

        return RecipientLanguage.Of(settled, requested: null, languages);
    }
}
