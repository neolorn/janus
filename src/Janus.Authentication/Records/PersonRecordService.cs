using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Records;

/// <summary>
/// What staff do to a person record: enter one for someone who gave their details
/// otherwise than on a host's screen, and find the records an organization holds.
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
/// <remarks>
/// Implements REC-COL-003, REC-AGE-001, REC-ADM-001, PRIV-CONS-001 and chapter 09 section
/// 8a.
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
    RandomNumberGenerator randomness) : IPersonRecords
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

        if (await DeniedAsync(context, organization, cancellationToken).ConfigureAwait(false) is Error denied)
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
            .HoldingAsync(organization, kind, records.FingerprintsOf(canonical), cancellationToken)
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

        if (await DeniedAsync(context, organization, cancellationToken).ConfigureAwait(false) is Error denied)
        {
            return Result.Failure<IReadOnlyList<SubjectId>>(denied);
        }

        // PRIV-RIGHT-005c: a reference is held only in its folded form.
        if (reference.Trim() is not { Length: > 0 } trimmed)
        {
            return Result.Success<IReadOnlyList<SubjectId>>([]);
        }

        return Result.Success(await records
            .ReferencingAsync(organization, CanonicalForm.Of(trimmed), cancellationToken)
            .ConfigureAwait(false));
    }

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

    // REC-ADM-001: a finding is asked in the organization named, under record:read.
    private async ValueTask<Error?> DeniedAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        (await gate
            .RequireAsync(context, Permissions.RecordRead, organization, cancellationToken)
            .ConfigureAwait(false))
        .Match<Error?>(() => null, error => error);

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
