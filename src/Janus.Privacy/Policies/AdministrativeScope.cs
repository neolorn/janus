using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Policies;

/// <summary>
/// Whether the caller may perform an operation of the deployment rather than of one
/// record.
/// </summary>
/// <param name="gate">The one place a permission is evaluated.</param>
/// <param name="memberships">Where the caller's own organizations are read.</param>
/// <remarks>
/// Implements AUTHZ-SEAM-001, AUTHZ-SCOPE-001, AUTHZ-CONCEAL-005 and LIB-API-005.
/// Calling the service in process is no way round the permission the endpoint
/// applies, so every administrative operation of the area passes through here.
/// </remarks>
internal sealed class AdministrativeScope(IAccessGate gate, IMembershipLookup memberships)
{
    /// <summary>
    /// The refusal, or nothing where the caller holds the permission in one of the
    /// organizations they belong to.
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
        if (context.Effective is not SubjectId subject)
        {
            return Error.From(ErrorCodes.Denied);
        }

        var refused = Error.From(ErrorCodes.Denied);

        foreach (OrganizationId organization in
            await memberships.OfAsync(subject, cancellationToken).ConfigureAwait(false))
        {
            Error? failure = (await gate
                    .RequireAsync(context, permission, organization, cancellationToken)
                    .ConfigureAwait(false))
                .Match<Error?>(() => null, error => error);

            if (failure is null)
            {
                return null;
            }

            refused = failure;
        }

        return refused;
    }
}
