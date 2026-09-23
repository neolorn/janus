using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Policies;

/// <summary>
/// Whether the caller may perform an operation of the deployment rather than of one
/// organization's records.
/// </summary>
/// <param name="gate">The one place a permission is evaluated.</param>
/// <param name="administrative">Which organization administers the deployment.</param>
/// <remarks>
/// Implements AUTHZ-SEAM-001, AUTHZ-SCOPE-001, AUTHZ-CONCEAL-005 and LIB-API-005. The
/// deployment is the administrative organization's to administer, so the permission is
/// asked there and nowhere else, and calling the service in process is no way round it.
/// </remarks>
internal sealed class AdministrativeScope(IAccessGate gate, IAdministrativeOrganization administrative)
{
    /// <summary>
    /// The refusal, or nothing where the caller holds the permission in the
    /// administrative organization.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">What they are asking to do.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The refusal, or nothing.</returns>
    public async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        Permission permission,
        CancellationToken cancellationToken)
    {
        if (await administrative.FindAsync(cancellationToken).ConfigureAwait(false)
            is not OrganizationId organization)
        {
            return Error.From(ErrorCodes.Denied);
        }

        return (await gate
                .RequireAsync(context, permission, organization, cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);
    }
}
