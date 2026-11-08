using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Grants;

/// <summary>
/// Where a grant written or revoked is written down.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-003, AUTHZ-INHERIT-002 and IDN-AUD-001 (D-197, D-203, D-206).
/// Each record is written in the transaction that writes or revokes the grant, so the
/// trail and the grant never disagree, and names the grant's holder as its subject where
/// the holder is an account, whoever acted.
/// </remarks>
internal interface IGrantAudit
{
    /// <summary>
    /// Records a grant a person, or an acknowledgement, wrote.
    /// </summary>
    /// <param name="created">The grant, carrying its instant and reason.</param>
    /// <param name="actor">Who wrote it.</param>
    /// <param name="breakGlassReason">
    /// The reason given at the use of the break-glass credential, where the grant was
    /// written in the session it opened, or nothing.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask CreatedAsync(
        Grant created,
        SubjectId actor,
        string? breakGlassReason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a grant a system principal wrote, as bootstrap writes the first grants.
    /// </summary>
    /// <param name="created">The grant, carrying its instant and reason.</param>
    /// <param name="principal">The principal that wrote it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask CreatedAsync(Grant created, SystemPrincipal principal, CancellationToken cancellationToken);

    /// <summary>
    /// Records a grant revoked by a person, or by the removal of the record it names.
    /// </summary>
    /// <param name="revoked">The grant, revoked, carrying its revoker, instant and reason.</param>
    /// <param name="breakGlassReason">
    /// The reason given at the use of the break-glass credential, where the grant was
    /// revoked in the session it opened, or nothing.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RevokedAsync(Grant revoked, string? breakGlassReason, CancellationToken cancellationToken);

    /// <summary>
    /// Records a grant the job <c>membership-ends</c> revoked at an end set in advance,
    /// under its principal, naming the administrator who set the end (IDN-MEM-001, D-217).
    /// </summary>
    /// <param name="revoked">The grant, revoked, carrying the nil revoker, its instant and the end's reason.</param>
    /// <param name="principal">The principal that revoked it.</param>
    /// <param name="endSetBy">The administrator who set the end.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RevokedAsync(
        Grant revoked,
        SystemPrincipal principal,
        SubjectId endSetBy,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a removal of access the host's check refused, as
    /// <c>authz.access.removalrefused</c> under the change's organization, naming who
    /// acted, the subject where it is an account, the kind, the code and the path, with the
    /// outcome <c>refused</c> (AUTHZ-GRANT-005, D-281). Unlike the other records it is
    /// written in a unit of work of its own, since the change it refuses writes nothing.
    /// </summary>
    /// <param name="removal">What was refused.</param>
    /// <param name="code">The code it was refused with.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RemovalRefusedAsync(
        AccessRemoval removal,
        ErrorCode code,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
