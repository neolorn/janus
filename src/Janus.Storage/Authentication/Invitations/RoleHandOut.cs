using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Storage.Authentication.Invitations;

/// <summary>
/// The hand-out rule an invitation and its acknowledgement ask, judged by the gate.
/// </summary>
/// <param name="admission">The gate's judgment of a change that hands out roles.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-005 and CONV-LAYOUT-001. The roles of an invitation are
/// granted across the organization, so the change has no record of its own.
/// </remarks>
internal sealed class RoleHandOut(IHandOutAdmission admission) : IRoleHandOut
{
    /// <inheritdoc/>
    public async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        Permission operation,
        OrganizationId organization,
        IReadOnlyList<DefinedRole> roles,
        IReadOnlyList<GrantSubject> widened,
        CancellationToken cancellationToken) =>
        (await admission
            .AdmitAsync(context, new HandOut(operation, organization, Resource: null, roles, widened), cancellationToken)
            .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);
}
