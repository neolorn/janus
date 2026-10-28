using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Records;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Requests;

namespace Janus.Hosting.Privacy;

/// <summary>
/// Where a message the privacy area needs delivered goes: to every channel the
/// account holds, in the language the account settled on; for a person record, to its
/// notice contacts, in the record's language.
/// </summary>
/// <param name="identifiers">Where the account's channels are read.</param>
/// <param name="records">Where a person record's notice contacts are read.</param>
/// <param name="sending">The one path every message the library sends takes.</param>
/// <param name="configuration">Where the fallback language is read.</param>
/// <remarks>
/// Implements CONV-CONTENT-001, CONV-DESIGN-003, AUTH-ABUSE-005 and REC-ACC-002. A
/// message one destination refuses still reaches the rest: the set exists so that no one
/// channel can silence it.
/// </remarks>
internal sealed class SubjectNotices(
    IIdentifierDirectory identifiers,
    IRecordDirectory records,
    IGovernedSend sending,
    IConfigurationStore configuration) : ISubjectNotices
{
    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(capacity: 0, StringComparer.Ordinal);

    /// <inheritdoc/>
    public async ValueTask<int> TellAsync(
        SubjectId subject,
        MessageKind message,
        string? source,
        CancellationToken cancellationToken)
    {
        // REC-ACC-002: every message the privacy area sends a person record goes to its
        // notice contacts, in the record's language.
        (IReadOnlyList<(IdentifierKind Kind, string Value)> channels, string? settled) =
            await records.NoticeContactsAsync(subject, cancellationToken).ConfigureAwait(false)
                is RecordNoticeContacts notice
                ? ([.. notice.Contacts.Select(contact => (contact.Kind, contact.Value))], notice.Language)
                : await HeldAsync(subject, cancellationToken).ConfigureAwait(false);

        string? language = await LanguageAsync(settled, cancellationToken).ConfigureAwait(false);
        int told = 0;

        foreach ((IdentifierKind kind, string value) in channels)
        {
            if (Destination(kind, value) is not SendDestination destination)
            {
                continue;
            }

            Result<SendReference> sent = await sending
                .UndertakeAsync(
                    new OutboundMessage(
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

    private static SendDestination? Destination(IdentifierKind kind, string value)
    {
        if (kind is IdentifierKind.Email)
        {
            return EmailAddress.TryParse(value, out EmailAddress address)
                ? SendDestination.Of(address)
                : null;
        }

        return PhoneNumber.TryParse(value, out PhoneNumber number)
            ? SendDestination.Of(number)
            : null;
    }

    private async ValueTask<(IReadOnlyList<(IdentifierKind Kind, string Value)> Channels, string? Settled)> HeldAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        string? settled = await identifiers.LanguageAsync(subject, cancellationToken).ConfigureAwait(false);

        return ([.. held.NoticeSet.Select(identifier => (identifier.Kind, identifier.Canonical))], settled);
    }

    private async ValueTask<string?> LanguageAsync(
        string? settled,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        return RecipientLanguage.Of(settled, requested: null, languages);
    }
}
