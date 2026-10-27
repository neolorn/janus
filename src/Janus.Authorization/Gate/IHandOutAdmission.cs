using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Where a change that hands out roles is admitted: the actor may hand out every role it
/// touches, and the change does not widen the actor's own access.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-005, AUTHZ-SEAM-001 and LIB-SEAM-001 (D-212). Which roles the
/// actor holds and what its grants admit are the gate's to decide, so the operations
/// that hand out roles ask through this contract and never name what answers it. It is
/// asked beside the admission of AUTHZ-SCOPE-001, before the step-up and again inside
/// the unit of work (AUTHZ-GATE-006).
/// </remarks>
internal interface IHandOutAdmission
{
    /// <summary>
    /// Admits a change that hands out roles.
    /// </summary>
    /// <param name="context">Who is acting.</param>
    /// <param name="change">What the change hands out, where, and whose access it widens.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success; otherwise <c>authz.grant.self</c> where the change widens the actor's own
    /// access, or <c>authz.role.notassignable</c> where the actor may not hand out a role
    /// it touches, each recorded as a refusal.
    /// </returns>
    ValueTask<Result> AdmitAsync(AccessContext context, HandOut change, CancellationToken cancellationToken);
}
