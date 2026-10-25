using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Where a grant, or its revocation, is admitted in the organization it is scoped to.
/// </summary>
/// <remarks>
/// Implements AUTHZ-SCOPE-001, AUTHZ-SEAM-001 and CONV-DESIGN-007 (D-208). A grant in an
/// organization other than the administrative organization is admitted under
/// <c>grant:manage</c> there or <c>system:administer</c> held in the administrative
/// organization, and the two are one admission: asked in turn, each refusal would be
/// recorded and counted for an operation the other then admits. The gate decides each
/// permission as its single check decides it, and the permission is evaluated behind
/// the one seam, so the grant operations ask through this contract and never name what
/// answers it (LIB-SEAM-001 AC1).
/// </remarks>
internal interface IGrantAdmission
{
    /// <summary>
    /// Admits a grant, or its revocation, in its organization.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="organization">The organization the grant is scoped to.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success; <c>authz.restricted</c> for a restricted account, recording nothing;
    /// otherwise the refusal of <c>grant:manage</c> in the grant's organization, recorded
    /// once and only where neither permission admits.
    /// </returns>
    ValueTask<Result> AdmitAsync(AccessContext context, OrganizationId organization, CancellationToken cancellationToken);
}
