using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Recovery;

/// <summary>
/// Whether an approver reaches the account whose recovery they approve: an approval
/// hands the account to whoever completes the enrolment, so it is judged as handing out
/// every role the account holds.
/// </summary>
/// <remarks>
/// Implements AUTH-RECOV-002 and AUTHZ-GRANT-005 (D-251). Which roles the account holds
/// and whether the approver may hand them out are the gate's to decide, so the approval
/// asks through this contract and never names what answers it. It is asked beside the
/// admission of <c>recovery:approve</c>, before the step-up and again inside the unit of
/// work (AUTHZ-GATE-006).
/// </remarks>
internal interface IApproverReach
{
    /// <summary>
    /// Judges an approval as handing out every role the account holds through a live
    /// allow grant, its own or a group's it belongs to, directly or through nesting, each
    /// in the organization that grant is in.
    /// </summary>
    /// <param name="context">Who approves.</param>
    /// <param name="subject">Whose recovery.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where the approver may hand out every such role, an account holding none
    /// included; otherwise <c>authz.role.notassignable</c>, recorded as a refusal.
    /// </returns>
    ValueTask<Error?> RefusedAsync(AccessContext context, SubjectId subject, CancellationToken cancellationToken);
}
