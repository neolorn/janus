using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// The person records a collection reads and writes, staff enter, and a code reaches.
/// The records themselves belong to the identity area; this area reaches them only
/// through this port.
/// </summary>
/// <remarks>
/// Implements REC-COL-002, REC-COL-003, REC-ACC-001, REC-ADM-001 and CONV-DESIGN-003.
/// </remarks>
internal interface IRecordDirectory
{
    /// <summary>
    /// Locks each value until the operation's transaction ends, under the same lock an
    /// account's identifier takes, so collections of one value are decided one after
    /// another.
    /// </summary>
    /// <param name="values">Each value's kind and canonical form.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of locking them.</returns>
    ValueTask LockValuesAsync(
        IReadOnlyList<(IdentifierKind Kind, string Canonical)> values,
        CancellationToken cancellationToken);

    /// <summary>
    /// The records of an organization that hold a value as a contact and that a
    /// collection may reuse: active and attached to no account, each held under its lock.
    /// </summary>
    /// <param name="organization">The organization collecting.</param>
    /// <param name="kind">The contact's kind.</param>
    /// <param name="canonical">The contact's canonical value.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The records, with their contacts.</returns>
    ValueTask<IReadOnlyList<CollectableRecord>> CollectableAsync(
        OrganizationId organization,
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes what a collection gives a record: a new record of origin
    /// <c>collection</c> with its own data key, or the gains of one reused.
    /// </summary>
    /// <param name="person">What the collection writes.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask WriteAsync(CollectedPerson person, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the record staff entered, of origin <c>staff</c> with its own data key.
    /// </summary>
    /// <param name="person">What the entry writes.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask EnterAsync(EnteredPerson person, CancellationToken cancellationToken);

    /// <summary>
    /// The keyed fingerprints of a contact's value, one under each version of the key a
    /// record's contact may be held under, which a record access session holds in place
    /// of the value (REC-ACC-001).
    /// </summary>
    /// <param name="canonical">The contact's canonical value.</param>
    /// <returns>The fingerprints.</returns>
    IReadOnlyList<byte[]> FingerprintsOf(string canonical);

    /// <summary>
    /// Whether a reachable record holds a value as a contact verified at a collection,
    /// whenever it was verified (REC-ACC-001).
    /// </summary>
    /// <param name="kind">The contact's kind.</param>
    /// <param name="fingerprints">The value's keyed fingerprints.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether one does.</returns>
    ValueTask<bool> ReachableHoldingAsync(
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        CancellationToken cancellationToken);

    /// <summary>
    /// The reachable records that hold a value as a contact last verified at a
    /// collection at or after an instant (REC-ACC-001).
    /// </summary>
    /// <param name="kind">The contact's kind.</param>
    /// <param name="fingerprints">The value's keyed fingerprints.</param>
    /// <param name="verifiedSince">The earliest last verification that reaches a record.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The records.</returns>
    ValueTask<IReadOnlyList<ReachedRecord>> ReachedAsync(
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        DateTimeOffset verifiedSince,
        CancellationToken cancellationToken);

    /// <summary>
    /// The reachable records that hold a value as a contact verified at a collection and
    /// a reference, whenever the value was verified (REC-ACC-001).
    /// </summary>
    /// <param name="kind">The contact's kind.</param>
    /// <param name="fingerprints">The value's keyed fingerprints.</param>
    /// <param name="reference">The reference, in its <c>NFKC_Casefold</c> form.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The records.</returns>
    ValueTask<IReadOnlyList<ReachedRecord>> ReferencedAsync(
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        string reference,
        CancellationToken cancellationToken);

    /// <summary>
    /// Those of some records still in a record access session's reach: reachable, and
    /// holding the value as a contact verified at a collection (REC-ACC-001).
    /// </summary>
    /// <param name="subjects">The records the session reached.</param>
    /// <param name="kind">The contact's kind.</param>
    /// <param name="fingerprints">The value's keyed fingerprints.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The records still in reach, in the order given.</returns>
    ValueTask<IReadOnlyList<ReachedRecord>> InReachAsync(
        IReadOnlyList<SubjectId> subjects,
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        CancellationToken cancellationToken);

    /// <summary>
    /// The records an organization holds that hold a value as a contact, verified or
    /// not, in the order of their subjects (REC-ADM-001).
    /// </summary>
    /// <param name="organization">The organization, or nothing for every organization.</param>
    /// <param name="kind">The contact's kind.</param>
    /// <param name="fingerprints">The value's keyed fingerprints.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Their subjects.</returns>
    ValueTask<IReadOnlyList<SubjectId>> HoldingAsync(
        OrganizationId? organization,
        IdentifierKind kind,
        IReadOnlyList<byte[]> fingerprints,
        CancellationToken cancellationToken);

    /// <summary>
    /// The records an organization holds that hold a reference, in the order of their
    /// subjects (REC-ADM-001).
    /// </summary>
    /// <param name="organization">The organization, or nothing for every organization.</param>
    /// <param name="reference">The reference, in its <c>NFKC_Casefold</c> form.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Their subjects.</returns>
    ValueTask<IReadOnlyList<SubjectId>> ReferencingAsync(
        OrganizationId? organization,
        string reference,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads one person record as the person reads it (REC-ACC-002).
    /// </summary>
    /// <param name="subject">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The record, or nothing where no record bears the subject.</returns>
    ValueTask<ReadRecord?> ReadAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Takes one person record's row for a change, to the end of the transaction, and
    /// reads where it stands under that lock (CONV-DESIGN-003).
    /// </summary>
    /// <param name="subject">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Where it stands, or nothing where no record bears the subject.</returns>
    ValueTask<RecordStanding?> HoldAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Reads where one person record stands, taking no lock: where a path naming it is
    /// admitted, and whether it is attached (REC-ADM-001, REC-ATT-002).
    /// </summary>
    /// <param name="subject">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Where it stands, or nothing where no record bears the subject.</returns>
    ValueTask<RecordStanding?> StandingAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the name and the language of an active record held for the change
    /// (REC-ACC-002).
    /// </summary>
    /// <param name="subject">The record's subject.</param>
    /// <param name="name">The name it takes, or nothing to leave it as it is.</param>
    /// <param name="language">The language it takes, or nothing to leave it as it is.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of carrying the change.</returns>
    ValueTask RectifyAsync(
        SubjectId subject,
        DisplayName? name,
        string? language,
        CancellationToken cancellationToken);

    /// <summary>
    /// Starts the deletion grace window of a record held for the change, active or
    /// restricted, a restriction in force held through it (REC-REC-002).
    /// </summary>
    /// <param name="subject">The record's subject.</param>
    /// <param name="by">The person's own request, or one that arrived out of band.</param>
    /// <param name="at">When the window began.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of carrying the change.</returns>
    ValueTask BeginDeletionAsync(
        SubjectId subject,
        DeletionOrigin by,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancels the deletion window of a record held for the change, which returns active,
    /// or restricted where a restriction is held (REC-REC-002).
    /// </summary>
    /// <param name="subject">The record's subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of carrying the change.</returns>
    ValueTask CancelDeletionAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Where a message to one person record goes (REC-ACC-002).
    /// </summary>
    /// <param name="subject">The subject a message names.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The record's notice contacts and language, or nothing where no record bears the
    /// subject.
    /// </returns>
    ValueTask<RecordNoticeContacts?> NoticeContactsAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// The records attached to an account, in the order they were attached, a deleted one
    /// left out (REC-ATT-001).
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The records.</returns>
    ValueTask<IReadOnlyList<Attachment>> AttachedToAsync(SubjectId account, CancellationToken cancellationToken);

    /// <summary>
    /// Attaches a record to an account, judged under the record's lock: only an active
    /// record attached to no account is attached (REC-ATT-001).
    /// </summary>
    /// <param name="subject">The record's subject.</param>
    /// <param name="account">The account it is attached to.</param>
    /// <param name="at">The instant of the attachment.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The organization holding the record, or nothing where it was not attached.
    /// </returns>
    ValueTask<OrganizationId?> AttachAsync(
        SubjectId subject,
        SubjectId account,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
