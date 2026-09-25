using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The host's records as the library knows them: which organization owns each, whose
/// data it is, and what contains it. The library never reads the host's tables, so
/// the host says this when it creates a record or moves one.
/// </summary>
/// <remarks>
/// Implements AUTHZ-INHERIT-001, AUTHZ-INHERIT-002, AUTHZ-SCOPE-001, LIB-HOST-002 and
/// PRIV-SENS-002. The ancestry every permission filter reads is written from these
/// calls in the transaction the call runs in, which is the unit of work the host has
/// already opened where it opened one. A record is placed only where the declaration
/// says its type is contained, and only in a container of its own organization.
/// </remarks>
public interface IResources
{
    /// <summary>
    /// Records a record the host has just created, and the ancestry that follows from
    /// where it sits.
    /// </summary>
    /// <param name="registration">The record, its organization, its container and its subject.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>api.request.malformed</c> naming <c>resourceType</c>
    /// where the model declares no such type, <c>resourceId</c> where the record is
    /// registered already, or <c>containedIn</c> where the container is not of the type
    /// the declaration contains the record in, is absent where the declaration requires
    /// one, is not registered, or belongs to another organization.
    /// </returns>
    ValueTask<Result> RegisterAsync(
        ResourceRegistration registration,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records many records at once, for a host importing in bulk, each judged as
    /// <see cref="RegisterAsync"/> judges one; containers come before their contents.
    /// </summary>
    /// <param name="registrations">The records, containers first.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the first refusal, in which case nothing is recorded.</returns>
    ValueTask<Result> RegisterManyAsync(
        IReadOnlyList<ResourceRegistration> registrations,
        CancellationToken cancellationToken);

    /// <summary>
    /// Moves a record under another container, or out of every container, carrying the
    /// ancestry of everything beneath it with it.
    /// </summary>
    /// <param name="resource">Which record moves.</param>
    /// <param name="containedIn">Its new container, or nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>api.request.malformed</c> naming <c>resourceId</c>
    /// where the record is not registered, or <c>containedIn</c> where the container
    /// could not hold it at registration.
    /// </returns>
    ValueTask<Result> MoveAsync(
        ResourceReference resource,
        ResourceReference? containedIn,
        CancellationToken cancellationToken);
}
