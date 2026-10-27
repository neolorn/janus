using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Authentication.Tests.Policies;
using Janus.Core;

namespace Janus.Authentication.Tests.Invitations;

/// <summary>
/// The hand-out rule over a test's gate: <c>system:administer</c> held in the
/// administrative organization admits any change; otherwise a change widening the
/// actor's own access is refused <c>authz.grant.self</c>, and each role is admitted where
/// a test said the actor hands it out in the organization (AUTHZ-GRANT-005, D-212).
/// </summary>
/// <param name="gate">The test's gate, which holds what a test granted.</param>
/// <param name="administrative">The organization that administers the deployment.</param>
/// <remarks>
/// CONV-TEST-004: a fake that answers as the rule answers, not a recorder of calls.
/// </remarks>
internal sealed class RoleHandOutInMemory(
    AccessGateInMemory gate,
    AdministrativeOrganizationInMemory administrative) : IRoleHandOut
{
    private readonly HashSet<(SubjectId Subject, OrganizationId Organization, RoleName Role)> _handsOut = [];

    /// <summary>
    /// Lets an account hand out a role in an organization, as a role it holds there
    /// listing it would.
    /// </summary>
    /// <param name="subject">The account.</param>
    /// <param name="organization">Where.</param>
    /// <param name="role">The role it hands out.</param>
    public void HandsOut(SubjectId subject, OrganizationId organization, RoleName role) =>
        _handsOut.Add((subject, organization, role));

    /// <inheritdoc/>
    public ValueTask<Error?> RefusedAsync(
        AccessContext context,
        Permission operation,
        OrganizationId organization,
        IReadOnlyList<DefinedRole> roles,
        IReadOnlyList<GrantSubject> widened,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(widened);

        if (context.Acting is not SubjectId acting)
        {
            return ValueTask.FromResult<Error?>(Error.From(ErrorCodes.Denied));
        }

        if (administrative.Organization is OrganizationId administering
            && gate.Holds(acting, administering, Permissions.SystemAdminister))
        {
            return ValueTask.FromResult<Error?>(null);
        }

        if (widened.Contains(GrantSubject.Of(acting)))
        {
            return ValueTask.FromResult<Error?>(Error.From(ErrorCodes.GrantSelf));
        }

        return ValueTask.FromResult<Error?>(
            roles.All(role => !role.Permissions.Contains(Permissions.SystemAdminister)
                && _handsOut.Contains((acting, organization, role.Name)))
                ? null
                : Error.From(ErrorCodes.RoleNotAssignable));
    }
}
