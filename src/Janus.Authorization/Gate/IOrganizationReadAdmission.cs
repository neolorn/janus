using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Where a read of an organization is admitted: one whose path names the organization,
/// and the list of the organizations a caller may read.
/// </summary>
/// <remarks>
/// Implements IDN-READ-001, IDN-READ-004, AUTHZ-SCOPE-001, AUTHZ-SEAM-001 and
/// LIB-SEAM-001 (D-219). A read of an organization other than the administrative one is
/// admitted under its permission there or in the administrative organization, and the
/// two are one admission, as a grant's are: asked in turn, each refusal would be
/// recorded and counted for a read the other then admits. The permission is evaluated
/// behind the one seam, so the reads ask through this contract and never name what
/// answers it.
/// </remarks>
internal interface IOrganizationReadAdmission
{
    /// <summary>
    /// Admits a read whose path names an organization.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">The read's permission.</param>
    /// <param name="organization">The organization the path names.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success; otherwise the refusal of the permission in the organization the path
    /// names, recorded once and only where neither organization admits.
    /// </returns>
    ValueTask<Result> AdmitAsync(
        AccessContext context,
        Permission permission,
        OrganizationId organization,
        CancellationToken cancellationToken);

    /// <summary>
    /// Which organizations a caller lists.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">The list's permission.</param>
    /// <param name="memberships">The organizations of the person's current memberships.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Every organization for a holder in the administrative organization; for any other
    /// person, those of the memberships the permission is held in; for a job, its own
    /// organization. Otherwise <c>authz.denied</c>, recorded in the administrative
    /// organization, and before bootstrap, recording nothing.
    /// </returns>
    ValueTask<Result<ListedOrganizations>> ListedAsync(
        AccessContext context,
        Permission permission,
        IReadOnlyCollection<OrganizationId> memberships,
        CancellationToken cancellationToken);
}
