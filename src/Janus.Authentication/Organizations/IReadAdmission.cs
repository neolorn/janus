using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Organizations;

/// <summary>
/// Where a read of an organization is admitted, as the gate judges it.
/// </summary>
/// <remarks>
/// Implements IDN-READ-001, IDN-READ-002, IDN-READ-004, AUTHZ-SCOPE-001 and
/// CONV-LAYOUT-001 (D-219). A read whose path names an organization is admitted under
/// its permission there or in the administrative organization as one admission, so the
/// reads ask through this contract and never name what answers it.
/// </remarks>
internal interface IReadAdmission
{
    /// <summary>
    /// Admits a read whose path names an organization.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">The read's permission.</param>
    /// <param name="organization">The organization the path names.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or the refusal, recorded once and naming the organization the path names.
    /// </returns>
    ValueTask<Error?> RefusedAsync(
        AccessContext context,
        Permission permission,
        OrganizationId organization,
        CancellationToken cancellationToken);

    /// <summary>
    /// Which organizations a caller lists: every organization, those of a person's
    /// current memberships the permission is held in, or a job's own.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">The list's permission.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The organizations listed, or the refusal.</returns>
    ValueTask<Result<ReadableOrganizations>> ListedAsync(
        AccessContext context,
        Permission permission,
        CancellationToken cancellationToken);
}
