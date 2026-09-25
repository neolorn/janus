using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Requests;

namespace Janus.Hosting.Privacy;

/// <summary>
/// Where a message the privacy area needs delivered goes: to every channel the
/// account holds, in the language the account settled on.
/// </summary>
/// <param name="identifiers">Where the account's channels are read.</param>
/// <param name="sending">The one path every message the library sends takes.</param>
/// <param name="configuration">Where the fallback language is read.</param>
/// <remarks>
/// Implements CONV-CONTENT-001, CONV-DESIGN-003 and AUTH-ABUSE-005. A message one
/// destination refuses still reaches the rest: the set exists so that no one channel
/// can silence it.
/// </remarks>
internal sealed class SubjectNotices(
    IIdentifierDirectory identifiers,
    INotificationHandler sending,
    IConfigurationStore configuration) : ISubjectNotices
{
    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(capacity: 0, StringComparer.Ordinal);

    /// <inheritdoc/>
    public async ValueTask<int> TellAsync(
        SubjectId subject,
        MessageKind message,
        string source,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers channels = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        string? language = await LanguageAsync(subject, cancellationToken).ConfigureAwait(false);
        int told = 0;

        foreach (HeldIdentifier identifier in channels.NoticeSet)
        {
            if (Destination(identifier) is not SendDestination destination)
            {
                continue;
            }

            Result<SendReference> sent = await sending
                .SendAsync(
                    new SendRequest(
                        destination,
                        message,
                        RestrictionPurpose.Notification,
                        source,
                        language)
                    {
                        Subject = subject,
                        Values = Nothing,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            told += sent.Match(_ => 1, _ => 0);
        }

        return told;
    }

    private static SendDestination? Destination(HeldIdentifier identifier)
    {
        if (identifier.Kind is IdentifierKind.Email)
        {
            return EmailAddress.TryParse(identifier.Canonical, out EmailAddress address)
                ? SendDestination.Of(address)
                : null;
        }

        return PhoneNumber.TryParse(identifier.Canonical, out PhoneNumber number)
            ? SendDestination.Of(number)
            : null;
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
