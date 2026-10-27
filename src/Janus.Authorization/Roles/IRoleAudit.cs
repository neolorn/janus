using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Roles;

/// <summary>
/// Where a change to a role is written down: who, what it was, what it became, when
/// and why.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-004, AUTHZ-GRANT-005, OPS-CFG-007, IDN-AUD-001 and
/// IDN-PRIN-001. A role changes what every holder of it may do and hand out at once, so
/// the change is recorded as a grant is, with the permissions and the roles handed out
/// before and after (D-212).
/// </remarks>
internal interface IRoleAudit
{
    /// <summary>
    /// Records a role created, or the permissions or the roles handed out of one changed.
    /// </summary>
    /// <param name="before">The role as it stood, or nothing where it is new.</param>
    /// <param name="after">The role as it stands now.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who defined it.</param>
    /// <param name="breakGlassReason">
    /// The reason given at the use of the break-glass credential, where the action was
    /// taken in the session it opened, or nothing.
    /// </param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask DefinedAsync(
        DefinedRole? before,
        DefinedRole after,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a role a system principal created, as bootstrap seeds the administrative
    /// ones and the start step the declared ones (IDN-PRIN-001).
    /// </summary>
    /// <param name="after">The role as it was created.</param>
    /// <param name="principal">The principal that created it, with its stated reason.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask DefinedAsync(
        DefinedRole after,
        SystemPrincipal principal,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a role removed.
    /// </summary>
    /// <param name="before">The role as it stood.</param>
    /// <param name="reason">Why.</param>
    /// <param name="actor">Who removed it.</param>
    /// <param name="breakGlassReason">
    /// The reason given at the use of the break-glass credential, where the action was
    /// taken in the session it opened, or nothing.
    /// </param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RemovedAsync(
        DefinedRole before,
        string reason,
        SubjectId actor,
        string? breakGlassReason,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
