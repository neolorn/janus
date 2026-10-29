using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Organizations;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Records;

/// <summary>
/// The rights a person exercises on one person record they reached: its read, the change
/// of its name and language, and the beginning and cancellation of its own deletion; and
/// whether its state admits the rights the privacy area carries for it (REC-ACC-002).
/// </summary>
/// <param name="records">The person records.</param>
/// <param name="organizations">The organizations, whose names the read shows.</param>
/// <param name="recordAudit">Where a change of the record's fields is recorded.</param>
/// <param name="audit">Where a deletion begun or cancelled is recorded, as an account's is.</param>
/// <param name="sending">The governed send the deletion notice goes out through.</param>
/// <param name="events">Where a deletion begun or cancelled is announced.</param>
/// <param name="configuration">The deployment's settings.</param>
/// <param name="work">The one transaction each change runs in.</param>
/// <param name="time">The deployment's clock.</param>
/// <remarks>
/// Implements REC-ACC-002, REC-REC-002 and CONV-DESIGN-003. Each right is asked of a
/// record its caller has already reached, and each change is decided again on the
/// record's row under its lock: a record attached meanwhile is no longer the code's to
/// change, and is answered as one the caller does not reach. Every action is recorded as
/// the account's same action is, the record acting and its subject.
/// </remarks>
internal sealed class RecordRights(
    IRecordDirectory records,
    IOrganizationDirectory organizations,
    IRecordAudit recordAudit,
    IAccountAudit audit,
    IGovernedSend sending,
    IEvents events,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time)
{
    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(capacity: 0, StringComparer.Ordinal);

    /// <summary>
    /// Reads the record.
    /// </summary>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The read, or <c>identity.record.notfound</c>.</returns>
    public async ValueTask<Result<RecordReading>> ReadAsync(
        SubjectId record,
        CancellationToken cancellationToken)
    {
        if (await records.ReadAsync(record, cancellationToken).ConfigureAwait(false) is not ReadRecord read)
        {
            return Result.Failure<RecordReading>(Error.From(ErrorCodes.RecordNotFound));
        }

        OrganizationStanding? organization = await organizations
            .FindAsync(read.Organization, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new RecordReading(read, organization?.Name ?? string.Empty));
    }

    /// <summary>
    /// Whether the record's state admits a right other than its read and the
    /// cancellation of its deletion: while its own deletion runs, it admits none
    /// (REC-REC-002).
    /// </summary>
    /// <param name="reached">The record as its caller reached it.</param>
    /// <returns>Nothing where it admits them, or <c>identity.record.stateconflict</c>.</returns>
    /// <exception cref="ArgumentNullException">The record is absent.</exception>
    public static Error? Unadmitted(ReachedRecord reached)
    {
        ArgumentNullException.ThrowIfNull(reached);

        return reached.State is PersonRecordState.Active or PersonRecordState.Restricted
            ? null
            : StateConflict(reached.State);
    }

    /// <summary>
    /// Changes the record's name, its language, or both.
    /// </summary>
    /// <param name="record">The record's subject.</param>
    /// <param name="name">The name given, or nothing to leave it.</param>
    /// <param name="language">The language given, or nothing to leave it.</param>
    /// <param name="attached">
    /// Whether it is asked through the account the record is attached to, rather than by a
    /// code that reached it.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or <c>identity.profile.invalid</c>, <c>api.request.invalid</c> naming
    /// <c>language</c>, <c>authz.restricted</c>, <c>identity.record.stateconflict</c>,
    /// <c>identity.record.notfound</c>.
    /// </returns>
    public async ValueTask<Result> RectifyAsync(
        SubjectId record,
        string? name,
        string? language,
        bool attached,
        CancellationToken cancellationToken)
    {
        DisplayName? named = null;

        if (name is not null)
        {
            if (!DisplayName.TryParse(name, out DisplayName parsed))
            {
                return Result.Failure(Error.From(ErrorCodes.ProfileInvalid));
            }

            named = parsed;
        }

        if (language is not null)
        {
            IReadOnlyList<string> languages = await LanguagesAsync(cancellationToken).ConfigureAwait(false);

            if (!languages.Contains(language, StringComparer.Ordinal))
            {
                return Result.Failure(Error.From(
                    ErrorCodes.RequestInvalid,
                    "member",
                    JsonSerializer.SerializeToElement("language")));
            }
        }

        return await ChangedAsync(
                record,
                attached,
                async standing =>
                {
                    // REC-REC-002: a restricted record admits its rights and no change of
                    // its own fields; a record in its deletion window admits neither.
                    if (standing.State is PersonRecordState.Restricted)
                    {
                        return Error.From(ErrorCodes.Restricted);
                    }

                    if (standing.State is not PersonRecordState.Active)
                    {
                        return StateConflict(standing.State);
                    }

                    await records.RectifyAsync(record, named, language, cancellationToken).ConfigureAwait(false);

                    DateTimeOffset now = time.GetUtcNow();

                    foreach (string field in Fields(named, language))
                    {
                        await recordAudit
                            .ChangedAsync(record, breakGlassReason: null, record, field, reason: null, now, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    return null;
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Begins the record's own deletion: its window runs <c>record.deletion.grace</c>,
    /// the host is told, and its notice contacts are sent <c>record-deletion-notice</c>.
    /// </summary>
    /// <param name="record">The record's subject.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or <c>identity.record.stateconflict</c>, <c>identity.takedown.active</c>,
    /// <c>identity.record.notfound</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public async ValueTask<Result> DeleteAsync(
        SubjectId record,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        TimeSpan grace = await GraceAsync(cancellationToken).ConfigureAwait(false);

        return await ChangedAsync(
                record,
                attached: false,
                async standing =>
                {
                    if (standing is { State: PersonRecordState.Deleting, DeletingBy: DeletionOrigin.Takedown })
                    {
                        return Error.From(ErrorCodes.TakedownActive);
                    }

                    if (standing.State is not (PersonRecordState.Active or PersonRecordState.Restricted))
                    {
                        return StateConflict(standing.State);
                    }

                    DateTimeOffset now = time.GetUtcNow();

                    await records
                        .BeginDeletionAsync(record, DeletionOrigin.Self, now, cancellationToken)
                        .ConfigureAwait(false);

                    if (await PublishedAsync(
                            new RecordDeletionRequested(now, Key(record, now), DeletionOrigin.Self, now + grace)
                            {
                                Subject = record,
                                Actor = record,
                                Effective = record,
                            },
                            cancellationToken)
                        .ConfigureAwait(false) is Error unpublished)
                    {
                        return unpublished;
                    }

                    await audit
                        .RecordedAsync(AuditActions.DeletionRequested, record, breakGlassReason: null, record, now, cancellationToken)
                        .ConfigureAwait(false);

                    _ = await ToldAsync(record, MessageKind.RecordDeletionNotice, source, cancellationToken)
                        .ConfigureAwait(false);

                    return null;
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels the record's own deletion inside its window, which returns it as it was.
    /// </summary>
    /// <param name="record">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or <c>identity.record.stateconflict</c>, <c>identity.takedown.active</c>,
    /// <c>identity.deletion.windowelapsed</c>, <c>identity.record.notfound</c>.
    /// </returns>
    public async ValueTask<Result> CancelDeletionAsync(
        SubjectId record,
        CancellationToken cancellationToken)
    {
        TimeSpan grace = await GraceAsync(cancellationToken).ConfigureAwait(false);

        return await ChangedAsync(
                record,
                attached: false,
                async standing =>
                {
                    if (standing is { State: PersonRecordState.Deleting, DeletingBy: DeletionOrigin.Takedown })
                    {
                        return Error.From(ErrorCodes.TakedownActive);
                    }

                    if (standing is not
                        {
                            State: PersonRecordState.Deleting,
                            DeletingBy: DeletionOrigin.Self,
                            DeletingSince: DateTimeOffset since,
                        })
                    {
                        return StateConflict(standing.State);
                    }

                    DateTimeOffset now = time.GetUtcNow();

                    if (now >= since + grace)
                    {
                        return Error.From(ErrorCodes.DeletionWindowElapsed);
                    }

                    await records.CancelDeletionAsync(record, cancellationToken).ConfigureAwait(false);

                    if (await PublishedAsync(
                            new RecordDeletionCancelled(now, Key(record, now))
                            {
                                Subject = record,
                                Actor = record,
                                Effective = record,
                            },
                            cancellationToken)
                        .ConfigureAwait(false) is Error unpublished)
                    {
                        return unpublished;
                    }

                    await audit
                        .RecordedAsync(AuditActions.DeletionCancelled, record, breakGlassReason: null, record, now, cancellationToken)
                        .ConfigureAwait(false);

                    return null;
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static Error StateConflict(PersonRecordState state) =>
        Error.From(ErrorCodes.RecordStateConflict, "state", JsonSerializer.SerializeToElement(WrittenName.Of(state)));

    // IDN-AUD-001: a change is recorded by field, never by value.
    private static IEnumerable<string> Fields(DisplayName? name, string? language)
    {
        if (name is not null)
        {
            yield return "name";
        }

        if (language is not null)
        {
            yield return "language";
        }
    }

    private static string Key(SubjectId record, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{record.Value}@{at.UtcTicks}");

    private static SendDestination? Destination(CollectedContact contact) =>
        contact.Kind switch
        {
            IdentifierKind.Email => EmailAddress.TryParse(contact.Value, out EmailAddress address)
                ? SendDestination.Of(address)
                : null,
            IdentifierKind.Phone => PhoneNumber.TryParse(contact.Value, out PhoneNumber number)
                ? SendDestination.Of(number)
                : null,
            _ => null,
        };

    private async ValueTask<IReadOnlyList<string>> LanguagesAsync(CancellationToken cancellationToken) =>
        (await configuration
            .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
        .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

    private async ValueTask<TimeSpan> GraceAsync(CancellationToken cancellationToken) =>
        (await configuration
            .ReadAsync(Settings.RecordDeletionGrace, cancellationToken).ConfigureAwait(false))
        .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

    private async ValueTask<Error?> PublishedAsync(DomainEvent raised, CancellationToken cancellationToken) =>
        (await events.PublishAsync(raised, cancellationToken).ConfigureAwait(false))
        .Match(() => (Error?)null, error => error);

    // The change decided on the record's row under its lock, in one unit of work: a record
    // no row bears is no one's to change; one attached to an account meanwhile is not the
    // code's, and one not attached to it is not the account's (REC-ATT-002).
    private async ValueTask<Result> ChangedAsync(
        SubjectId record,
        bool attached,
        Func<RecordStanding, ValueTask<Error?>> change,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        Error? refused = await records.HoldAsync(record, cancellationToken).ConfigureAwait(false)
            is RecordStanding standing && standing.Attached == attached
            ? await change(standing).ConfigureAwait(false)
            : Error.From(ErrorCodes.RecordNotFound);

        if (refused is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(refused);
        }

        return await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    // REC-ACC-002: a message to a record goes to its notice contacts, in its language.
    private async ValueTask<int> ToldAsync(
        SubjectId record,
        MessageKind message,
        string source,
        CancellationToken cancellationToken)
    {
        if (await records.NoticeContactsAsync(record, cancellationToken).ConfigureAwait(false)
            is not RecordNoticeContacts notice)
        {
            return 0;
        }

        IReadOnlyList<string> languages = await LanguagesAsync(cancellationToken).ConfigureAwait(false);
        string? language = RecipientLanguage.Of(notice.Language, requested: null, languages);
        int told = 0;

        foreach (CollectedContact contact in notice.Contacts)
        {
            if (Destination(contact) is not SendDestination destination)
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
                        Subject = record,
                        Values = Nothing,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            told += sent.Match(_ => 1, _ => 0);
        }

        return told;
    }
}
