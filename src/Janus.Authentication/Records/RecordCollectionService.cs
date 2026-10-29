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
/// The collection of a person record from the session a browser holds: the record its
/// verified contacts reach, reused or created, with what the screen took and presented.
/// </summary>
/// <param name="sessions">The collection sessions, one of which the collection spends.</param>
/// <param name="records">The person records a collection reads and writes.</param>
/// <param name="audit">Where the collection is recorded.</param>
/// <param name="organizations">Which organizations the deployment holds.</param>
/// <param name="documents">The privacy notice's current version.</param>
/// <param name="consents">Where each consent the person ticked is recorded.</param>
/// <param name="configuration">Where the message languages and the proof's age are read.</param>
/// <param name="work">The host's unit of work, which the collection joins.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a new record's subject identifier is drawn from.</param>
/// <remarks>
/// Implements REC-COL-002, REC-AGE-001, PRIV-CONS-001 and PRIV-CONS-008a. Everything
/// that needs no session is judged before the unit of work is joined; everything after
/// is judged on the session and the records held under their locks, and a refusal
/// there rolls the whole back, so a refusal writes nothing and keeps nothing.
/// </remarks>
internal sealed class RecordCollectionService(
    CollectionSessions sessions,
    IRecordDirectory records,
    IRecordAudit audit,
    IOrganizationDirectory organizations,
    ILegalDocuments documents,
    IConsents consents,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    private const int ReferenceMaximum = 64;

    /// <summary>
    /// Writes or reuses the record the session's verified contacts reach, and spends
    /// the session.
    /// </summary>
    /// <param name="session">The collection session the browser holds, where it holds one.</param>
    /// <param name="collection">What the screen took and presented.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The record and whether it was created, or the refusal of REC-COL-002.</returns>
    /// <exception cref="ArgumentNullException">The collection is absent.</exception>
    public async ValueTask<Result<CollectedRecord>> CollectAsync(
        CollectionSessionId? session,
        RecordCollection collection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (session is not CollectionSessionId named)
        {
            return Refused(Error.From(ErrorCodes.CollectionExpired));
        }

        Error? failure = null;

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        TimeSpan proofAge = (await configuration
                .ReadAsync(Settings.RecordsProofAge, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Refused(failure);
        }

        if (Taken(collection, languages, out DisplayName? name, out string? reference, out List<CollectedContact> given)
            is Error malformed)
        {
            return Refused(malformed);
        }

        if (await PresentedAsync(collection, cancellationToken).ConfigureAwait(false) is Error unpresented)
        {
            return Refused(unpresented);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Refused(notBegun);
        }

        Result<CollectedRecord> decided = await HeldAsync(
                named,
                collection,
                new Taking(name, reference, given, proofAge),
                cancellationToken)
            .ConfigureAwait(false);

        if (decided.Match<Error?>(_ => null, error => error) is Error refused)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Refused(refused);
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => decided, Result.Failure<CollectedRecord>);
    }

    // What the screen took that needs no session to judge: the language, the name, the
    // reference and the contacts it did not verify.
    private static Error? Taken(
        RecordCollection collection,
        IReadOnlyList<string> languages,
        out DisplayName? name,
        out string? reference,
        out List<CollectedContact> given)
    {
        name = null;
        reference = null;
        given = [];

        if (!languages.Contains(collection.Language, StringComparer.Ordinal))
        {
            return Invalid("language");
        }

        if (collection.Name is string entered)
        {
            if (!DisplayName.TryParse(entered, out DisplayName parsed))
            {
                return Error.From(ErrorCodes.ProfileInvalid);
            }

            name = parsed;
        }

        // PRIV-RIGHT-005c: the reference is matched later in its folded form, and that
        // form is the only one ever kept.
        if (collection.Reference is string held)
        {
            if (held.Trim() is not { Length: > 0 and <= ReferenceMaximum } trimmed)
            {
                return Invalid("reference");
            }

            reference = CanonicalForm.Of(trimmed);
        }

        foreach (KeyValuePair<IdentifierKind, string> contact in collection.Contacts
            ?? new Dictionary<IdentifierKind, string>())
        {
            if (Canonical(contact.Key, contact.Value.Trim()) is not string canonical)
            {
                return Invalid("contacts");
            }

            given.Add(new CollectedContact(contact.Key, canonical, VerifiedAt: null));
        }

        return null;
    }

    // PRIV-CONS-008a: the notice the screen presented, and the terms it asked the person
    // to accept, are the ones in force, or the person was shown something the deployment
    // no longer stands behind.
    private async ValueTask<Error?> PresentedAsync(RecordCollection collection, CancellationToken cancellationToken) =>
        await PresentedVersions
            .RefusedAsync(documents, PresentedVersions.Notice, collection.NoticeVersion, "noticeVersion", cancellationToken)
            .ConfigureAwait(false)
        ?? (collection.TermsVersion is string terms
            ? await PresentedVersions
                .RefusedAsync(documents, PresentedVersions.Terms, terms, "termsVersion", cancellationToken)
                .ConfigureAwait(false)
            : null);

    // Under the session's lock and every verified value's, from the read to the write.
    private async ValueTask<Result<CollectedRecord>> HeldAsync(
        CollectionSessionId named,
        RecordCollection collection,
        Taking taking,
        CancellationToken cancellationToken)
    {
        if (await sessions.HoldAsync(named, cancellationToken).ConfigureAwait(false) is not CollectionSession live)
        {
            return Refused(Error.From(ErrorCodes.CollectionExpired));
        }

        IReadOnlyList<CollectedContact> verified = live.Contacts;

        if ((live.AsksAge && !live.AgeAnswered)
            || verified.Any(contact => !contact.IsVerified)
            || (verified.Count is 0 && taking.Given.Count is 0))
        {
            return Refused(Error.From(ErrorCodes.CollectionIncomplete));
        }

        if (taking.Given.Any(contact => live.Contact(contact.Kind) is not null))
        {
            return Refused(Invalid("contacts"));
        }

        if (await StandsAsync(collection.Organization, cancellationToken).ConfigureAwait(false) is Error unheld)
        {
            return Refused(unheld);
        }

        DateTimeOffset now = time.GetUtcNow();

        // CONV-DESIGN-003, REC-COL-002 AC5: collections of one value are decided one
        // after another, so two at once end with one record.
        await records
            .LockValuesAsync([.. verified.Select(contact => (contact.Kind, contact.Value))], cancellationToken)
            .ConfigureAwait(false);

        CollectableRecord? reused = await ReusedAsync(
                collection.Organization,
                verified,
                now - taking.ProofAge,
                cancellationToken)
            .ConfigureAwait(false);

        var person = new CollectedPerson(
            reused?.Subject ?? SubjectId.New(randomness),
            collection.Organization,
            Created: reused is null,
            collection.Language,
            taking.Name,
            reused is { AgeAnswered: true } || live.AnsweredAgeAt is not DateTimeOffset answeredAt
                ? null
                : new CollectedAge(live.AdultAffirmed, live.Group, live.DateOfBirth, answeredAt),
            [
                .. verified.Select(contact => contact with { VerifiedAt = now }),
                .. taking.Given.Where(contact =>
                    reused is null || reused.Contacts.All(held => held.Kind != contact.Kind)),
            ],
            taking.Reference,
            collection.NoticeVersion,
            collection.TermsVersion,
            now);

        await records.WriteAsync(person, cancellationToken).ConfigureAwait(false);

        if (await ConsentedAsync(person.Subject, collection.Consents, cancellationToken).ConfigureAwait(false)
            is Error unconsented)
        {
            return Refused(unconsented);
        }

        await sessions.EndAsync(named, cancellationToken).ConfigureAwait(false);
        await audit
            .CollectedAsync(person.Subject, person.Organization, person.Created, now, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new CollectedRecord(person.Subject, person.Created));
    }

    // REC-COL-002: the record of the organization that holds a contact the session
    // verified as one last verified at a collection within the proof's age, and whose
    // every contact is the session's of its kind or of a kind the session did not
    // verify; the most recently verified where several do.
    private async ValueTask<CollectableRecord?> ReusedAsync(
        OrganizationId organization,
        IReadOnlyList<CollectedContact> verified,
        DateTimeOffset provenSince,
        CancellationToken cancellationToken)
    {
        var candidates = new Dictionary<SubjectId, CollectableRecord>();

        foreach (CollectedContact contact in verified)
        {
            foreach (CollectableRecord held in await records
                .CollectableAsync(organization, contact.Kind, contact.Value, cancellationToken)
                .ConfigureAwait(false))
            {
                candidates.TryAdd(held.Subject, held);
            }
        }

        return candidates.Values
            .Select(record => (Record: record, Proven: Proven(record, verified, provenSince)))
            .Where(candidate => candidate.Proven is not null
                && candidate.Record.Contacts.All(held => Agrees(held, verified)))
            .OrderByDescending(candidate => candidate.Proven)
            .Select(candidate => candidate.Record)
            .FirstOrDefault();
    }

    // The latest instant at which a contact the session verified was verified on the
    // record at a collection within the proof's age.
    private static DateTimeOffset? Proven(
        CollectableRecord record,
        IReadOnlyList<CollectedContact> verified,
        DateTimeOffset provenSince) =>
        record.Contacts
            .Where(held => held.VerifiedAt is DateTimeOffset at
                && at >= provenSince
                && verified.Any(contact => contact.Kind == held.Kind
                    && string.Equals(contact.Value, held.Value, StringComparison.Ordinal)))
            .Max(held => held.VerifiedAt);

    private static bool Agrees(CollectedContact held, IReadOnlyList<CollectedContact> verified) =>
        verified.FirstOrDefault(contact => contact.Kind == held.Kind) is not CollectedContact ours
        || string.Equals(ours.Value, held.Value, StringComparison.Ordinal);

    private async ValueTask<Error?> StandsAsync(OrganizationId organization, CancellationToken cancellationToken) =>
        await organizations.FindAsync(organization, cancellationToken).ConfigureAwait(false)
            is { DeletionRequestedAt: null, ErasedAt: null }
            ? null
            : Invalid("organization");

    // PRIV-CONS-001, PRIV-CONS-002: one consent per purpose ticked, recorded against the
    // current version of the document governing it, as the person's own.
    private async ValueTask<Error?> ConsentedAsync(
        SubjectId subject,
        IReadOnlyList<string> purposes,
        CancellationToken cancellationToken)
    {
        var holder = AccessContext.Of(subject);

        foreach (string purpose in purposes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if ((await consents
                    .GrantAsync(holder, purpose, ConsentMechanism.Collection, cancellationToken)
                    .ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error refused)
            {
                return refused;
            }
        }

        return null;
    }

    private static string? Canonical(IdentifierKind kind, string entered) =>
        kind switch
        {
            IdentifierKind.Email when EmailAddress.TryParse(entered, out EmailAddress address) => address.Value,
            IdentifierKind.Phone when PhoneNumber.TryParse(entered, out PhoneNumber number) => number.Value,
            _ => null,
        };

    private static Error Invalid(string member) =>
        Error.From(ErrorCodes.RequestInvalid, "member", JsonSerializer.SerializeToElement(member));

    private static Result<CollectedRecord> Refused(Error error) => Result.Failure<CollectedRecord>(error);

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // What the screen took, judged before the unit of work is joined.
    private sealed record Taking(
        DisplayName? Name,
        string? Reference,
        IReadOnlyList<CollectedContact> Given,
        TimeSpan ProofAge);
}
