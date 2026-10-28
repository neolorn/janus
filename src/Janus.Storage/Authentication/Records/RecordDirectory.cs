using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;
using Janus.Identity.Identifiers;
using Janus.Identity.Records;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Identity.Records;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// The person records a collection reads and writes, staff enter and a code reaches,
/// over the identity area's record store.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="records">The person records.</param>
/// <param name="identifiers">The identifiers, whose value locks a collection takes too.</param>
/// <param name="subjectKeys">Where a new record's data key is created.</param>
/// <remarks>
/// Implements REC-COL-002, REC-COL-003, REC-REC-001, REC-REC-002, REC-ACC-001, REC-ACC-002,
/// REC-ATT-001, REC-ADM-001 and CONV-DESIGN-003.
/// </remarks>
internal sealed class RecordDirectory(
    StoreContext context,
    IPersonRecordStore records,
    IIdentifierStore identifiers,
    ISubjectKeyStore subjectKeys) : IRecordDirectory
{
    /// <inheritdoc/>
    public ValueTask LockValuesAsync(
        IReadOnlyList<(IdentifierKind Kind, string Canonical)> values,
        CancellationToken cancellationToken) =>
        identifiers.LockValuesAsync(values, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<CollectableRecord>> CollectableAsync(
        OrganizationId organization,
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(canonical);

        IReadOnlyList<SubjectId> holding = await records
            .HoldingContactAsync(kind, canonical, cancellationToken)
            .ConfigureAwait(false);
        var collectable = new List<CollectableRecord>(holding.Count);

        // Rows are held in one order, so two collections holding the same records never
        // wait on each other's.
        foreach (SubjectId subject in holding.OrderBy(id => id.Value))
        {
            if (await records.HoldAsync(subject, cancellationToken).ConfigureAwait(false)
                is not { State: PersonRecordState.Active, AttachedTo: null } held
                || held.Organization != organization)
            {
                continue;
            }

            IReadOnlyList<RecordContact> contacts = await records
                .ContactsAsync(subject, cancellationToken)
                .ConfigureAwait(false);

            collectable.Add(new CollectableRecord(
                subject,
                [.. contacts.Select(contact => new CollectedContact(contact.Kind, contact.Value, contact.VerifiedAt))],
                held.Age is not null));
        }

        return collectable;
    }

    /// <inheritdoc/>
    public async ValueTask WriteAsync(CollectedPerson person, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(person);

        PersonRecord record = person.Created
            ? await CreatedAsync(
                    person.Subject,
                    person.Organization,
                    PersonRecordOrigin.Collection,
                    person.Language,
                    person.At,
                    cancellationToken)
                .ConfigureAwait(false)
            : await records.HoldAsync(person.Subject, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The record a collection reuses has no row.");

        await GainedAsync(
                record,
                person.Language,
                person.Name,
                person.Age,
                person.Contacts,
                person.Reference,
                person.At,
                cancellationToken)
            .ConfigureAwait(false);

        await records
            .AddDocumentsAsync(
                person.Subject,
                new RecordDocuments(person.NoticeVersion, person.TermsVersion, person.At),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask EnterAsync(EnteredPerson person, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(person);

        PersonRecord record = await CreatedAsync(
                person.Subject,
                person.Organization,
                PersonRecordOrigin.Staff,
                person.Language,
                person.At,
                cancellationToken)
            .ConfigureAwait(false);

        await GainedAsync(
                record,
                person.Language,
                person.Name,
                person.Age,
                person.Contacts,
                person.Reference,
                person.At,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IReadOnlyList<byte[]> FingerprintsOf(string canonical) => records.FingerprintsOf(canonical);

    /// <inheritdoc/>
    public async ValueTask<bool> ReachableHoldingAsync(
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        CancellationToken cancellationToken) =>
        (await ReachingAsync(kind, fingerprints, verifiedSince: null, among: null, cancellationToken)
            .ConfigureAwait(false))
        .Count > 0;

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ReachedRecord>> ReachedAsync(
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        DateTimeOffset verifiedSince,
        CancellationToken cancellationToken) =>
        ReachingAsync(kind, fingerprints, verifiedSince, among: null, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ReachedRecord>> ReferencedAsync(
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        string reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        IReadOnlyList<SubjectId> referenced = await records
            .HoldingReferenceAsync(reference, cancellationToken)
            .ConfigureAwait(false);

        return await ReachingAsync(kind, fingerprints, verifiedSince: null, [.. referenced], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ReachedRecord>> InReachAsync(
        IReadOnlyList<SubjectId> subjects,
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        IReadOnlyList<ReachedRecord> reaching = await ReachingAsync(
                kind,
                fingerprints,
                verifiedSince: null,
                [.. subjects],
                cancellationToken)
            .ConfigureAwait(false);

        return [.. subjects.SelectMany(subject => reaching.Where(record => record.Subject == subject))];
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SubjectId>> HoldingAsync(
        OrganizationId organization,
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        CancellationToken cancellationToken) =>
        await HeldByAsync(
                organization,
                await records.HoldingContactAsync(kind, fingerprints, cancellationToken).ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SubjectId>> ReferencingAsync(
        OrganizationId organization,
        string reference,
        CancellationToken cancellationToken) =>
        await HeldByAsync(
                organization,
                await records.HoldingReferenceAsync(reference, cancellationToken).ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<ReadRecord?> ReadAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        if (await records.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false)
            is not PersonRecord record)
        {
            return null;
        }

        IReadOnlyList<RecordContact> contacts = await records
            .ContactsAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<RecordDocuments> documents = await records
            .DocumentsAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        // Chapter 09 section 7a: the read carries the age answer and never the date of
        // birth it was computed from.
        return new ReadRecord(
            record.Subject,
            record.Organization,
            record.Origin,
            record.State,
            record.CreatedAt,
            record.Name?.Value,
            record.Language,
            [.. contacts.Select(contact => new CollectedContact(contact.Kind, contact.Value, contact.VerifiedAt))],
            record.Age is RecordAgeAnswer age ? new RecordAge(age.AdultAffirmed, age.Group, age.AnsweredAt) : null,
            [.. documents.Select(held => new RecordDocument(held.NoticeVersion, held.RecordedAt))],
            [
                .. documents
                    .Where(held => held.TermsVersion is not null)
                    .Select(held => new RecordDocument(held.TermsVersion!, held.RecordedAt)),
            ]);
    }

    /// <inheritdoc/>
    public async ValueTask<RecordStanding?> HoldAsync(SubjectId subject, CancellationToken cancellationToken) =>
        await records.HoldAsync(subject, cancellationToken).ConfigureAwait(false) is PersonRecord record
            ? new RecordStanding(
                record.Organization,
                record.State,
                record.DeletingBy,
                record.DeletingSince,
                record.AttachedTo is not null)
            : null;

    /// <inheritdoc/>
    public ValueTask RectifyAsync(
        SubjectId subject,
        DisplayName? name,
        string? language,
        CancellationToken cancellationToken) =>
        ChangedAsync(
            subject,
            record =>
            {
                if (name is DisplayName given)
                {
                    record.Rename(given);
                }

                if (language is not null)
                {
                    record.ChangeLanguage(language);
                }
            },
            cancellationToken);

    /// <inheritdoc/>
    public ValueTask BeginDeletionAsync(
        SubjectId subject,
        DeletionOrigin by,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        ChangedAsync(subject, record => record.RequestDeletion(by, at), cancellationToken);

    /// <inheritdoc/>
    public ValueTask CancelDeletionAsync(SubjectId subject, CancellationToken cancellationToken) =>
        ChangedAsync(subject, record => record.CancelDeletion(), cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<RecordNoticeContacts?> NoticeContactsAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        if (await records.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false)
            is not PersonRecord record)
        {
            return null;
        }

        IReadOnlyList<RecordContact> contacts = await records
            .ContactsAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        // REC-ACC-002: the contacts verified at a collection, or, where none was, every
        // contact the record holds.
        IEnumerable<RecordContact> notified = contacts.Any(contact => contact.VerifiedAt is not null)
            ? contacts.Where(contact => contact.VerifiedAt is not null)
            : contacts;

        return new RecordNoticeContacts(
            record.Language,
            [.. notified.Select(contact => new CollectedContact(contact.Kind, contact.Value, contact.VerifiedAt))]);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Attachment>> AttachedToAsync(
        SubjectId account,
        CancellationToken cancellationToken)
    {
        List<PersonRecordRow> attached = await context.PersonRecords
            .Where(record => record.AttachedTo == account && record.State != PersonRecordState.Deleted)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. attached
                .OrderBy(record => record.AttachedAt)
                .ThenBy(record => record.Subject.Value)
                .Select(record => new Attachment(
                    record.Subject,
                    record.Organization,
                    record.CreatedAt,
                    record.AttachedAt!.Value)),
        ];
    }

    /// <inheritdoc/>
    public async ValueTask<OrganizationId?> AttachAsync(
        SubjectId subject,
        SubjectId account,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        // REC-ATT-001: judged again under the record's lock, so of two attachments at
        // once only one finds the record unattached.
        if (await records.HoldAsync(subject, cancellationToken).ConfigureAwait(false)
            is not { State: PersonRecordState.Active, AttachedTo: null } record)
        {
            return null;
        }

        record.Attach(account, at);

        await records.RecordAsync(record, cancellationToken).ConfigureAwait(false);

        return record.Organization;
    }

    // A transition of the record the caller holds: the row is read under the lock the
    // caller took, changed by the aggregate, and carried back.
    private async ValueTask ChangedAsync(
        SubjectId subject,
        Action<PersonRecord> change,
        CancellationToken cancellationToken)
    {
        PersonRecord record = await records.HoldAsync(subject, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A record is changed only where a row bears it.");

        change(record);

        await records.RecordAsync(record, cancellationToken).ConfigureAwait(false);
    }

    // Those of some records the organization holds, in the order of their subjects.
    private async ValueTask<IReadOnlyList<SubjectId>> HeldByAsync(
        OrganizationId organization,
        IReadOnlyList<SubjectId> subjects,
        CancellationToken cancellationToken)
    {
        if (subjects.Count == 0)
        {
            return [];
        }

        List<SubjectId> held = await context.PersonRecords
            .Where(record => record.Organization == organization && subjects.Contains(record.Subject))
            .Select(record => record.Subject)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. held.OrderBy(subject => subject.Value)];
    }

    // REC-ACC-001: a code reaches a record collected from the person, attached to no
    // account, active, restricted or deleting by its own request, whose contact of the
    // kind is the value and was verified at a collection; where an instant is named, at
    // or after it. A record staff entered is never reached.
    private async ValueTask<IReadOnlyList<ReachedRecord>> ReachingAsync(
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        DateTimeOffset? verifiedSince,
        HashSet<SubjectId>? among,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprints);

        IReadOnlyList<SubjectId> holding = await records
            .HoldingContactAsync(kind, fingerprints, cancellationToken)
            .ConfigureAwait(false);
        var reached = new List<ReachedRecord>();

        foreach (SubjectId subject in holding.OrderBy(id => id.Value))
        {
            if ((among is not null && !among.Contains(subject))
                || await records.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false)
                    is not { Origin: PersonRecordOrigin.Collection, IsReachable: true } record)
            {
                continue;
            }

            IReadOnlyList<RecordContact> contacts = await records
                .ContactsAsync(subject, cancellationToken)
                .ConfigureAwait(false);

            if (contacts.FirstOrDefault(contact => contact.Kind == kind) is { VerifiedAt: DateTimeOffset verified }
                && (verifiedSince is null || verified >= verifiedSince))
            {
                reached.Add(new ReachedRecord(subject, record.Organization, record.CreatedAt, record.State));
            }
        }

        return reached;
    }

    // What a record takes from a collection or an entry: its language, the name given,
    // the age answer, each contact and the reference.
    private async ValueTask GainedAsync(
        PersonRecord record,
        string language,
        DisplayName? name,
        CollectedAge? age,
        IReadOnlyList<CollectedContact> contacts,
        string? reference,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        record.ChangeLanguage(language);

        if (name is DisplayName given)
        {
            record.Rename(given);
        }

        if (age is CollectedAge answered)
        {
            record.AnswerAge(new RecordAgeAnswer(
                answered.AdultAffirmed,
                answered.Group,
                answered.AnsweredAt,
                answered.DateOfBirth));
        }

        await records.RecordAsync(record, cancellationToken).ConfigureAwait(false);

        foreach (CollectedContact contact in contacts)
        {
            await records
                .SetContactAsync(
                    record.Subject,
                    new RecordContact(contact.Kind, contact.Value, contact.VerifiedAt),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (reference is string held)
        {
            await records.AddReferenceAsync(record.Subject, held, at, cancellationToken).ConfigureAwait(false);
        }
    }

    // The key row is the record's parent by a constraint the model does not know, so it
    // is written before the record names it, and the record before anything names it.
    private async ValueTask<PersonRecord> CreatedAsync(
        SubjectId subject,
        OrganizationId organization,
        PersonRecordOrigin origin,
        string language,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var record = PersonRecord.Create(subject, organization, origin, at, language);

        await subjectKeys.CreateAsync(subject, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await records.AddAsync(record, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return record;
    }
}
