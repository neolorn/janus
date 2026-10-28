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
/// The person records a collection reads and writes, over the identity area's record
/// store.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="records">The person records.</param>
/// <param name="identifiers">The identifiers, whose value locks a collection takes too.</param>
/// <param name="subjectKeys">Where a new record's data key is created.</param>
/// <remarks>
/// Implements REC-COL-002, REC-REC-001 and CONV-DESIGN-003.
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
        foreach (SubjectId subject in holding.Order())
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
            ? await CreatedAsync(person, cancellationToken).ConfigureAwait(false)
            : await records.HoldAsync(person.Subject, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The record a collection reuses has no row.");

        record.ChangeLanguage(person.Language);

        if (person.Name is DisplayName name)
        {
            record.Rename(name);
        }

        if (person.Age is CollectedAge age)
        {
            record.AnswerAge(new RecordAgeAnswer(age.AdultAffirmed, age.Group, age.AnsweredAt, age.DateOfBirth));
        }

        await records.RecordAsync(record, cancellationToken).ConfigureAwait(false);

        foreach (CollectedContact contact in person.Contacts)
        {
            await records
                .SetContactAsync(
                    person.Subject,
                    new RecordContact(contact.Kind, contact.Value, contact.VerifiedAt),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (person.Reference is string reference)
        {
            await records.AddReferenceAsync(person.Subject, reference, person.At, cancellationToken).ConfigureAwait(false);
        }

        await records
            .AddDocumentsAsync(
                person.Subject,
                new RecordDocuments(person.NoticeVersion, person.TermsVersion, person.At),
                cancellationToken)
            .ConfigureAwait(false);
    }

    // The key row is the record's parent by a constraint the model does not know, so it
    // is written before the record names it, and the record before anything names it.
    private async ValueTask<PersonRecord> CreatedAsync(CollectedPerson person, CancellationToken cancellationToken)
    {
        var record = PersonRecord.Create(
            person.Subject,
            person.Organization,
            PersonRecordOrigin.Collection,
            person.At,
            person.Language);

        await subjectKeys.CreateAsync(person.Subject, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await records.AddAsync(record, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return record;
    }
}
