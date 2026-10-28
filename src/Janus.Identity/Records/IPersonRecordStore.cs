using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Identity.Records;

/// <summary>
/// Where person records, their contacts, their references and the documents their
/// collections recorded are read and written.
/// </summary>
/// <remarks>
/// Implements REC-REC-001, REC-REC-002 and CONV-DESIGN-003. A contact's value and a
/// record's name and date of birth are held under the record's key; a contact and a
/// reference are found by a keyed fingerprint and never by their value
/// (PRIV-RIGHT-005c).
/// </remarks>
internal interface IPersonRecordStore
{
    /// <summary>
    /// Reads a person record.
    /// </summary>
    /// <param name="subject">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The record, or nothing where no record bears the subject. A deleted record's
    /// personal fields are unreadable and are answered as absent.
    /// </returns>
    ValueTask<PersonRecord?> FindBySubjectAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a person record under a lock on its row, held until the operation's
    /// transaction ends.
    /// </summary>
    /// <param name="subject">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The record as committed when the lock was taken, or nothing.</returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    ValueTask<PersonRecord?> HoldAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Records a newly created person record. Its subject's key row exists already.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AddAsync(PersonRecord record, CancellationToken cancellationToken);

    /// <summary>
    /// Carries what the record now holds onto its row: its state, its language, its
    /// name, its age answer and its attachment.
    /// </summary>
    /// <param name="record">The record as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="InvalidOperationException">The record has no row.</exception>
    ValueTask RecordAsync(PersonRecord record, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the records standing in a deletion grace window that began on or before an
    /// instant.
    /// </summary>
    /// <param name="before">The instant the window must have begun by.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Them, oldest window first.</returns>
    ValueTask<IReadOnlyList<PersonRecord>> DeletingSinceAsync(
        DateTimeOffset before,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the contacts a record holds.
    /// </summary>
    /// <param name="subject">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>At most one of each kind; none for a deleted record.</returns>
    ValueTask<IReadOnlyList<RecordContact>> ContactsAsync(
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sets the record's contact of a kind, replacing the one it held.
    /// </summary>
    /// <param name="subject">The record's subject.</param>
    /// <param name="contact">The contact, in its canonical form.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask SetContactAsync(SubjectId subject, RecordContact contact, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the record's contact of a kind, where it holds one.
    /// </summary>
    /// <param name="subject">The record's subject.</param>
    /// <param name="kind">An email or a phone.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of removing it.</returns>
    ValueTask RemoveContactAsync(SubjectId subject, IdentifierKind kind, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the records that hold a contact, by its fingerprint.
    /// </summary>
    /// <param name="kind">An email or a phone.</param>
    /// <param name="value">The canonical form.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The subjects of the records that hold it.</returns>
    ValueTask<IReadOnlyList<SubjectId>> HoldingContactAsync(
        IdentifierKind kind,
        string value,
        CancellationToken cancellationToken);

    /// <summary>
    /// The keyed fingerprints of a contact's value, one under each version of the key a
    /// contact may be held under (REC-ACC-001).
    /// </summary>
    /// <param name="value">The canonical form.</param>
    /// <returns>The fingerprints.</returns>
    IReadOnlyList<byte[]> FingerprintsOf(string value);

    /// <summary>
    /// Finds the records that hold a contact, by fingerprints already taken of its value.
    /// </summary>
    /// <param name="kind">An email or a phone.</param>
    /// <param name="fingerprints">The value's keyed fingerprints.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The subjects of the records that hold it.</returns>
    ValueTask<IReadOnlyList<SubjectId>> HoldingContactAsync(
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a reference a collection gave the record, as a keyed fingerprint alone.
    /// </summary>
    /// <param name="subject">The record's subject.</param>
    /// <param name="reference">The reference, in its <c>NFKC_Casefold</c> form.</param>
    /// <param name="at">When it was given.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AddReferenceAsync(
        SubjectId subject,
        string reference,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds the records that hold a reference, by its fingerprint.
    /// </summary>
    /// <param name="reference">The reference, in its <c>NFKC_Casefold</c> form.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The subjects of the records that hold it.</returns>
    ValueTask<IReadOnlyList<SubjectId>> HoldingReferenceAsync(
        string reference,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the presentation of the privacy notice a collection made, and the
    /// acceptance of the terms where it asked for one, with the instant.
    /// </summary>
    /// <param name="subject">The record's subject.</param>
    /// <param name="documents">What the collection presented and the person accepted.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AddDocumentsAsync(
        SubjectId subject,
        RecordDocuments documents,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads every presentation and acceptance the record's collections recorded.
    /// </summary>
    /// <param name="subject">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Them, oldest first.</returns>
    ValueTask<IReadOnlyList<RecordDocuments>> DocumentsAsync(
        SubjectId subject,
        CancellationToken cancellationToken);
}
