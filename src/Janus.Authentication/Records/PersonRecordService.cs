using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Organizations;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Records;

/// <summary>
/// What staff do to a person record: enter one for someone who gave their details
/// otherwise than on a host's screen, and find the records an organization holds; and
/// what an account does to attach the records a proof offers it.
/// </summary>
/// <param name="gate">Where each operation's permission is asked.</param>
/// <param name="records">The person records staff enter.</param>
/// <param name="audit">Where what staff did is recorded.</param>
/// <param name="organizations">Which organizations the deployment holds.</param>
/// <param name="consents">Where each consent the person gave staff is recorded.</param>
/// <param name="configuration">Where the message languages and the age rules are read.</param>
/// <param name="work">The unit of work an operation runs in, the host's where it calls inside its own.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a new record's subject identifier is drawn from.</param>
/// <param name="attachments">What an account does to attach the records a proof offers it.</param>
/// <param name="reach">Where a finding is asked, and whether it reaches every organization.</param>
/// <param name="accounts">Whether an account whose attached records are read exists.</param>
/// <param name="volume">Where a person's reads of records are counted.</param>
/// <param name="administration">What staff do to a record for the person who reaches it by no code.</param>
/// <param name="attached">The person's rights on a record attached to their account.</param>
/// <remarks>
/// Implements REC-COL-003, REC-AGE-001, REC-ADM-001, REC-ATT-001, PRIV-CONS-001 and
/// chapter 09 sections 7a and 8a.
/// Everything is judged before the unit of work is joined save the consents, whose
/// refusal rolls the whole back, so a refusal writes nothing and keeps nothing.
/// </remarks>
internal sealed class PersonRecordService(
    IAccessGate gate,
    IRecordDirectory records,
    IRecordAudit audit,
    IOrganizationDirectory organizations,
    IConsents consents,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness,
    RecordAttachments attachments,
    AccountReach reach,
    IAccountDirectory accounts,
    IReadVolume volume,
    RecordAdministration administration,
    AttachedRecordRights attached) : IPersonRecords
{
    private const int ReferenceMaximum = 64;

    /// <inheritdoc/>
    public async ValueTask<Result<SubjectId>> EnterAsync(
        AccessContext context,
        PersonRecordEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(entry);

        // REC-COL-003: the staff member is the entry's acting identity, so a principal
        // that is no person enters nothing.
        if (context.Acting is not SubjectId acting)
        {
            return Refused(Error.From(ErrorCodes.Denied));
        }

        // 09 section 8a: an organization the deployment does not hold is a member of the
        // request out of its rule; the organization it names is where the gate is asked.
        if (await ScopeOfAsync(entry.Organization, cancellationToken).ConfigureAwait(false) is null)
        {
            return Refused(Invalid("organization"));
        }

        if ((await gate
                .RequireAsync(context, Permissions.RecordCreate, entry.Organization, cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error denied)
        {
            return Refused(denied);
        }

        // 09 section 8a: an organization on its way out takes no new record.
        if (await organizations.FindAsync(entry.Organization, cancellationToken).ConfigureAwait(false)
            is not { DeletionRequestedAt: null, ErasedAt: null })
        {
            return Refused(Invalid("organization"));
        }

        Error? failure = null;

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        AttributeRequirement affirmation = (await configuration
                .ReadAsync(Settings.RegistrationAdultAffirmation, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<AttributeRequirement>(error, ref failure));

        AttributeRequirement retention = (await configuration
                .ReadAsync(Settings.ProfileDateOfBirth, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<AttributeRequirement>(error, ref failure));

        if (failure is not null)
        {
            return Refused(failure);
        }

        if (Taken(entry, languages, out DisplayName? name, out string? reference, out List<CollectedContact> given)
            is Error malformed)
        {
            return Refused(malformed);
        }

        DateTimeOffset now = time.GetUtcNow();

        // REC-AGE-001, PRIV-MINOR-001: on an adults-only host the service is not for the
        // person an under-age answer describes, so nothing is written for them.
        if (entry.DateOfBirth is DateOnly answered
            && affirmation is not AttributeRequirement.Off
            && !RegistrationService.IsAdult(answered, DateOnly.FromDateTime(now.UtcDateTime)))
        {
            return Refused(Error.From(ErrorCodes.ProfileUnderage));
        }

        var person = new EnteredPerson(
            SubjectId.New(randomness),
            entry.Organization,
            entry.Language,
            name,
            entry.DateOfBirth is DateOnly born ? Answered(born, affirmation, retention, now) : null,
            given,
            reference,
            now);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Refused(notBegun);
        }

        await records.EnterAsync(person, cancellationToken).ConfigureAwait(false);

        if (await ConsentedAsync(person.Subject, entry.Consents, cancellationToken).ConfigureAwait(false)
            is Error unconsented)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Refused(unconsented);
        }

        await audit
            .EnteredAsync(
                acting,
                context.BreakGlassReason,
                person.Subject,
                person.Organization,
                person.Age is not null,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => Result.Success(person.Subject), Result.Failure<SubjectId>);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<SubjectId>>> FindAsync(
        AccessContext context,
        OrganizationId organization,
        IdentifierKind kind,
        string value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(value);

        Result<AdmittedRead> asked = await reach
            .AskedAsync(context, Permissions.RecordRead, organization, cancellationToken)
            .ConfigureAwait(false);

        if (asked.Match(_ => (Error?)null, error => error) is Error denied)
        {
            return Result.Failure<IReadOnlyList<SubjectId>>(denied);
        }

        // IDN-ACCT-004: read in the canonical form a contact is held in, so another case,
        // another Unicode form or other digits find the same record; a value that is not
        // well formed is one no record holds.
        if (Canonical(kind, value.Trim()) is not string canonical)
        {
            return Result.Success<IReadOnlyList<SubjectId>>([]);
        }

        return Result.Success(await records
            .HoldingAsync(Searched(asked), kind, records.FingerprintsOf(canonical), cancellationToken)
            .ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<SubjectId>>> FindAsync(
        AccessContext context,
        OrganizationId organization,
        string reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reference);

        Result<AdmittedRead> asked = await reach
            .AskedAsync(context, Permissions.RecordRead, organization, cancellationToken)
            .ConfigureAwait(false);

        if (asked.Match(_ => (Error?)null, error => error) is Error denied)
        {
            return Result.Failure<IReadOnlyList<SubjectId>>(denied);
        }

        // PRIV-RIGHT-005c: a reference is held only in its folded form.
        if (reference.Trim() is not { Length: > 0 } trimmed)
        {
            return Result.Success<IReadOnlyList<SubjectId>>([]);
        }

        return Result.Success(await records
            .ReferencingAsync(Searched(asked), CanonicalForm.Of(trimmed), cancellationToken)
            .ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<PersonRecordDetail>> ReadAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if ((await administration.AdmittedAsync(context, Permissions.RecordRead, subject, cancellationToken).ConfigureAwait(false))
            .Match(_ => (Error?)null, error => error) is Error refused)
        {
            return Result.Failure<PersonRecordDetail>(refused);
        }

        if (await records.ReadAsync(subject, cancellationToken).ConfigureAwait(false) is not ReadRecord read)
        {
            return Result.Failure<PersonRecordDetail>(Error.From(ErrorCodes.RecordNotFound));
        }

        OrganizationStanding? holding = await organizations
            .FindAsync(read.Organization, cancellationToken)
            .ConfigureAwait(false);

        // OPS-ALERT-005: a person's read of a record counts toward their read volume; a
        // system principal's reads are nobody's and are not counted.
        return (await volume.ReturnedAsync(context, 1, cancellationToken).ConfigureAwait(false))
            .Match(() => Result.Success(Detail(read, holding?.Name ?? string.Empty)), Result.Failure<PersonRecordDetail>);
    }

    /// <inheritdoc/>
    public ValueTask<Result> RectifyAsync(
        AccessContext context,
        SubjectId subject,
        PersonRecordCorrection correction,
        CancellationToken cancellationToken) =>
        administration.RectifyAsync(context, subject, correction, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> LiftRestrictionAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken) =>
        administration.LiftRestrictionAsync(context, session, subject, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> CancelDeletionAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken) =>
        administration.CancelDeletionAsync(context, session, subject, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<SubjectExport>> ExportAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        CancellationToken cancellationToken) =>
        administration.ExportAsync(context, session, subject, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<ExecutedTakedown>> TakeDownAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        TakedownTrigger trigger,
        string reason,
        CancellationToken cancellationToken) =>
        administration.TakeDownAsync(context, session, subject, trigger, reason, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<TakedownProgress>> ReadTakedownAsync(
        AccessContext context,
        SubjectId subject,
        CancellationToken cancellationToken) =>
        administration.ReadTakedownAsync(context, subject, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> ReverseTakedownAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        string reason,
        CancellationToken cancellationToken) =>
        administration.ReverseTakedownAsync(context, session, subject, reason, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<SubjectId>>> AttachedAsync(
        AccessContext context,
        SubjectId account,
        OrganizationId? organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // REC-ATT-002: a person's own context reads its own.
        if (context.Effective is SubjectId own && own.Equals(account))
        {
            return Result.Success<IReadOnlyList<SubjectId>>(
            [
                .. (await records.AttachedToAsync(account, cancellationToken).ConfigureAwait(false))
                    .Select(attached => attached.Record),
            ]);
        }

        return await (await reach.AskedAsync(context, Permissions.RecordRead, organization, cancellationToken).ConfigureAwait(false))
            .Match(
                read => AttachedWithinAsync(read, account, cancellationToken),
                error => ValueTask.FromResult(Result.Failure<IReadOnlyList<SubjectId>>(error)))
            .ConfigureAwait(false);
    }

    private async ValueTask<Result<IReadOnlyList<SubjectId>>> AttachedWithinAsync(
        AdmittedRead read,
        SubjectId account,
        CancellationToken cancellationToken)
    {
        // IDN-READ-004: an account outside the reach is answered as one no person bears.
        if (await accounts.StateAsync(account, cancellationToken).ConfigureAwait(false) is null or AccountState.Deleted
            || !await reach.ReachesAsync(read, account, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<IReadOnlyList<SubjectId>>(Error.From(ErrorCodes.AccountNotFound));
        }

        // D-222: the attached records the organization holds, every one in the
        // administrative organization.
        return Result.Success<IReadOnlyList<SubjectId>>(
        [
            .. (await records.AttachedToAsync(account, cancellationToken).ConfigureAwait(false))
                .Where(attached => read.Administrative || attached.Organization == read.Organization)
                .Select(attached => attached.Record),
        ]);
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<AttachedRecord>>> ListAttachedAsync(
        AccessContext context,
        CancellationToken cancellationToken) =>
        attachments.ListAttachedAsync(context, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<RecordOffer>> ReadOfferAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken) =>
        attachments.ReadOfferAsync(context, session, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> SendFindCodeAsync(
        AccessContext context,
        SessionId session,
        IdentifierId identifier,
        string source,
        CancellationToken cancellationToken) =>
        attachments.SendFindCodeAsync(context, session, identifier, source, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<RecordOffer>> VerifyFindCodeAsync(
        AccessContext context,
        SessionId session,
        [NeverLogged] string code,
        string source,
        CancellationToken cancellationToken) =>
        attachments.VerifyFindCodeAsync(context, session, code, source, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<int>> AttachAsync(
        AccessContext context,
        SessionId session,
        CancellationToken cancellationToken) =>
        attachments.AttachAsync(context, session, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<int>> AttachByReferenceAsync(
        AccessContext context,
        SessionId session,
        string reference,
        string source,
        CancellationToken cancellationToken) =>
        attachments.AttachByReferenceAsync(context, session, reference, source, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<PersonRecordDetail>> ReadAttachedAsync(
        AccessContext context,
        SubjectId record,
        CancellationToken cancellationToken) =>
        attached.ReadAsync(context, record, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> RectifyAttachedAsync(
        AccessContext context,
        SubjectId record,
        string? name,
        string? language,
        CancellationToken cancellationToken) =>
        attached.RectifyAsync(context, record, name, language, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<SubjectExport>> ExportAttachedAsync(
        AccessContext context,
        SessionId session,
        SubjectId record,
        CancellationToken cancellationToken) =>
        attached.ExportAsync(context, session, record, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<ConsentRecord>>> AttachedConsentsAsync(
        AccessContext context,
        SubjectId record,
        CancellationToken cancellationToken) =>
        attached.ConsentsAsync(context, record, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> GrantAttachedConsentAsync(
        AccessContext context,
        SubjectId record,
        string purpose,
        CancellationToken cancellationToken) =>
        attached.GrantAsync(context, record, purpose, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> WithdrawAttachedConsentAsync(
        AccessContext context,
        SubjectId record,
        string purpose,
        CancellationToken cancellationToken) =>
        attached.WithdrawAsync(context, record, purpose, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<ObjectionRecord>>> AttachedObjectionsAsync(
        AccessContext context,
        SubjectId record,
        CancellationToken cancellationToken) =>
        attached.ObjectionsAsync(context, record, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> ObjectForAttachedAsync(
        AccessContext context,
        SubjectId record,
        string purpose,
        CancellationToken cancellationToken) =>
        attached.ObjectAsync(context, record, purpose, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> WithdrawAttachedObjectionAsync(
        AccessContext context,
        SubjectId record,
        string purpose,
        CancellationToken cancellationToken) =>
        attached.WithdrawObjectionAsync(context, record, purpose, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<PrivacyRequestReceipt>> SubmitAttachedRequestAsync(
        AccessContext context,
        SubjectId record,
        PrivacyRequestType type,
        string detail,
        CancellationToken cancellationToken) =>
        attached.SubmitAsync(context, record, type, detail, cancellationToken);

    // What the person gave, judged before anything is written: the language, the name,
    // each contact and the reference.
    private static Error? Taken(
        PersonRecordEntry entry,
        IReadOnlyList<string> languages,
        out DisplayName? name,
        out string? reference,
        out List<CollectedContact> given)
    {
        name = null;
        reference = null;
        given = [];

        if (!languages.Contains(entry.Language, StringComparer.Ordinal))
        {
            return Invalid("language");
        }

        // REC-COL-003: a record holds at least one of the three, and the refusal names the
        // first (D-222).
        if (entry is { Name: null, Email: null, Phone: null })
        {
            return Invalid("name");
        }

        if (entry.Name is string entered)
        {
            if (!DisplayName.TryParse(entered, out DisplayName parsed))
            {
                return Error.From(ErrorCodes.ProfileInvalid);
            }

            name = parsed;
        }

        if (entry.Email is string email)
        {
            if (!EmailAddress.TryParse(email.Trim(), out EmailAddress address))
            {
                return Error.From(ErrorCodes.IdentifierInvalid);
            }

            given.Add(new CollectedContact(IdentifierKind.Email, address.Value, VerifiedAt: null));
        }

        if (entry.Phone is string phone)
        {
            if (!PhoneNumber.TryParse(phone.Trim(), out PhoneNumber number))
            {
                return Error.From(ErrorCodes.IdentifierInvalid);
            }

            given.Add(new CollectedContact(IdentifierKind.Phone, number.Value, VerifiedAt: null));
        }

        // PRIV-RIGHT-005c: the reference is matched later in its folded form, and that
        // form is the only one ever kept.
        if (entry.Reference is string held)
        {
            if (held.Trim() is not { Length: > 0 and <= ReferenceMaximum } trimmed)
            {
                return Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement("reference"));
            }

            reference = CanonicalForm.Of(trimmed);
        }

        return null;
    }

    // REC-AGE-001: the person's own answer, recorded as the screen of REG-PROF-002
    // records it, with the instant the staff member entered it.
    private static CollectedAge Answered(
        DateOnly born,
        AttributeRequirement affirmation,
        AttributeRequirement retention,
        DateTimeOffset now)
    {
        bool adult = RegistrationService.IsAdult(born, DateOnly.FromDateTime(now.UtcDateTime));

        return new CollectedAge(
            affirmation is AttributeRequirement.Off ? null : adult,
            affirmation is AttributeRequirement.Off ? RegistrationService.Band(adult) : null,
            retention is AttributeRequirement.Off ? null : born,
            now);
    }

    // PRIV-CONS-001, PRIV-CONS-002: one consent per purpose the person gave, recorded
    // against the current version of the document governing it as the record's own,
    // entered on its behalf.
    private async ValueTask<Error?> ConsentedAsync(
        SubjectId subject,
        IReadOnlyList<string> purposes,
        CancellationToken cancellationToken)
    {
        var holder = AccessContext.Of(subject);

        foreach (string purpose in purposes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if ((await consents
                    .GrantAsync(holder, purpose, ConsentMechanism.Administrator, cancellationToken)
                    .ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error refused)
            {
                return refused;
            }
        }

        return null;
    }

    // The record as the person reads it, with its origin and the account it is attached to.
    internal static PersonRecordDetail Detail(ReadRecord read, string organizationName) =>
        new(
            read.Subject,
            read.Organization,
            organizationName,
            read.Origin,
            read.State,
            read.CreatedAt,
            read.Name,
            read.Language,
            [.. read.Contacts.Select(contact => new PersonRecordContact(contact.Kind, contact.Value, contact.VerifiedAt))],
            read.Age is RecordAge age ? new PersonRecordAge(age.AdultAffirmed, age.Group, age.AnsweredAt) : null,
            [.. read.Notices.Select(notice => new PersonRecordDocument(notice.Version, notice.At))],
            [.. read.Terms.Select(terms => new PersonRecordDocument(terms.Version, terms.At))],
            read.AttachedTo);

    // REC-ADM-001, D-222: a finding is asked in the organization named, under record:read,
    // and searches the records it holds; asked in the administrative organization, those
    // every organization holds.
    private static OrganizationId? Searched(Result<AdmittedRead> asked) =>
        asked.Match(read => read.Administrative ? null : (OrganizationId?)read.Organization, _ => null);

    private static string? Canonical(IdentifierKind kind, string entered) =>
        kind switch
        {
            IdentifierKind.Email when EmailAddress.TryParse(entered, out EmailAddress address) => address.Value,
            IdentifierKind.Phone when PhoneNumber.TryParse(entered, out PhoneNumber number) => number.Value,
            _ => null,
        };

    private async ValueTask<OrganizationId?> ScopeOfAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        await organizations.FindAsync(organization, cancellationToken).ConfigureAwait(false) is null
            ? null
            : organization;

    private static Error Invalid(string member) =>
        Error.From(ErrorCodes.RequestInvalid, "member", JsonSerializer.SerializeToElement(member));

    private static Result<SubjectId> Refused(Error error) => Result.Failure<SubjectId>(error);

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
