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

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// The person records a collection reads and writes and staff enter, over the identity
/// area's record store.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="records">The person records.</param>
/// <param name="identifiers">The identifiers, whose value locks a collection takes too.</param>
/// <param name="subjectKeys">Where a new record's data key is created.</param>
/// <remarks>
/// Implements REC-COL-002, REC-COL-003, REC-REC-001 and CONV-DESIGN-003.
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
