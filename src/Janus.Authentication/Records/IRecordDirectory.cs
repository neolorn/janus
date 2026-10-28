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
/// Implements REC-COL-002, REC-COL-003, REC-ACC-001 and CONV-DESIGN-003.
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
}
