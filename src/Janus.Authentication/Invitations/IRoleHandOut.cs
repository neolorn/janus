using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Invitations;

/// <summary>
/// Whether the actor may hand out the roles a change touches, and whether the change
/// widens the actor's own access.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-005 and CONV-LAYOUT-001. The rule is the authorization area's;
/// this is what an invitation and its acknowledgement ask of it. A refusal is recorded
/// where the rule is judged, so the caller writes nothing of it.
/// </remarks>
internal interface IRoleHandOut
{
    /// <summary>
    /// Judges a change that hands out roles across an organization.
    /// </summary>
    /// <param name="context">Who makes the change.</param>
    /// <param name="operation">The permission the change is admitted under.</param>
    /// <param name="organization">Where the roles are handed out.</param>
    /// <param name="roles">The roles the change hands out.</param>
    /// <param name="widened">The holders whose access the change widens.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where the change is admitted; otherwise <c>authz.grant.self</c> or
    /// <c>authz.role.notassignable</c>.
    /// </returns>
    ValueTask<Error?> RefusedAsync(
        AccessContext context,
        Permission operation,
        OrganizationId organization,
        IReadOnlyList<DefinedRole> roles,
        IReadOnlyList<GrantSubject> widened,
        CancellationToken cancellationToken);
}
