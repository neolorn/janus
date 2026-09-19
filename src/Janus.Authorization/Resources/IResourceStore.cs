using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Resources;

/// <summary>
/// Where the host's records and the ancestry inheritance is resolved through are read
/// and written.
/// </summary>
/// <remarks>
/// Implements AUTHZ-INHERIT-002, AUTHZ-INHERIT-003 and CONV-DESIGN-003. Every write
/// here carries the ancestry with it in the same transaction, which is what keeps
/// permission data and business data from diverging and what lets the permission
/// predicate be a join rather than a recursion.
/// </remarks>
internal interface IResourceStore
{
    /// <summary>
    /// Reads one registered record.
    /// </summary>
    /// <param name="reference">Which record.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The registration, or nothing where no such row exists.</returns>
    ValueTask<RegisteredResource?> FindAsync(
        ResourceReference reference,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a new resource and the ancestry that follows from it.
    /// </summary>
    /// <param name="resource">The record the host created.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RegisterAsync(RegisteredResource resource, CancellationToken cancellationToken);

    /// <summary>
    /// Records many resources and their ancestry, for a host importing in bulk.
    /// </summary>
    /// <param name="resources">The records, containers before their contents.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording them.</returns>
    ValueTask RegisterManyAsync(
        IReadOnlyList<RegisteredResource> resources,
        CancellationToken cancellationToken);

    /// <summary>
    /// Moves a record under another container, carrying the ancestry of every record
    /// beneath it with the move.
    /// </summary>
    /// <param name="reference">Which record moves.</param>
    /// <param name="containedIn">Its new container, or nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="System.InvalidOperationException">No such row exists.</exception>
    ValueTask MoveAsync(
        ResourceReference reference,
        ResourceReference? containedIn,
        CancellationToken cancellationToken);

    /// <summary>
    /// The record itself and everything containing it, nearest first. This is the
    /// ancestry an explanation names the container from.
    /// </summary>
    /// <param name="reference">Which record.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The record and its containers.</returns>
    ValueTask<IReadOnlyList<ResourceReference>> AncestryAsync(
        ResourceReference reference,
        CancellationToken cancellationToken);
}
