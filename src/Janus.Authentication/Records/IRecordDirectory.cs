using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// The person records a collection reads and writes. The records themselves belong to
/// the identity area; a collection reaches them only through this port.
/// </summary>
/// <remarks>
/// Implements REC-COL-002 and CONV-DESIGN-003.
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
}
