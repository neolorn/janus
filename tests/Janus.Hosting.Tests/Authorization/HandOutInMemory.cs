using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Tests;
using Janus.Authorization.Gate;
using Janus.Authorization.Tests.Gate;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The hand-out admission over a test deployment's gate and groups: <c>system:administer</c>
/// held in the administrative organization admits any change; otherwise a change widening
/// the actor's own access is refused <c>authz.grant.self</c>, and each role is admitted
/// where a test said the actor hands it out in the organization, or, with
/// <c>authz.handout.byholding</c> on, where the gate admits every permission it carries
/// there; a change to a role is admitted where the gate admits in the administrative
/// organization every permission it carries before and after and the roles it hands out
/// pass (AUTHZ-GRANT-005, D-212).
/// </summary>
/// <param name="gate">The deployment's gate, which holds what a test granted.</param>
/// <param name="groups">The deployment's groups, which hold whom each group holds.</param>
/// <param name="configuration">Where the deployment's settings are read.</param>
/// <remarks>
/// CONV-TEST-004: a fake that answers as the admission answers, not a recorder of calls.
/// </remarks>
internal sealed class HandOutInMemory(
    AccessGateInMemory gate,
    GroupsInMemory groups,
    IConfigurationStore configuration) : IHandOutAdmission
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
    public async ValueTask<Result> AdmitAsync(
        AccessContext context,
        HandOut change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(change);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (gate.Administrative is OrganizationId administrative
            && gate.Admits(context, Permissions.SystemAdminister, administrative))
        {
            return Result.Success();
        }

        IReadOnlyList<GroupId> belongsTo = await groups
            .GroupsOfAsync(GrantSubject.Of(acting), cancellationToken)
            .ConfigureAwait(false);

        if (change.Widened.Any(holder => holder == GrantSubject.Of(acting)
            || (holder.Type is SubjectType.Group && belongsTo.Contains(new GroupId(holder.Value)))))
        {
            return Result.Failure(Error.From(ErrorCodes.GrantSelf));
        }

        if (change.Roles.Any(role => role.Permissions.Contains(Permissions.SystemAdminister)))
        {
            return Result.Failure(Error.From(ErrorCodes.RoleNotAssignable));
        }

        bool byHolding = (await configuration
                .ReadAsync(Settings.AuthzHandoutByHolding, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, _ => false);

        return change.Roles.All(role => _handsOut.Contains((acting, change.Organization, role.Name))
            || (byHolding && role.Permissions.All(permission => gate.Admits(context, permission, change.Organization))))
            ? Result.Success()
            : Result.Failure(Error.From(ErrorCodes.RoleNotAssignable));
    }

    /// <inheritdoc/>
    public async ValueTask<Result> AdmitAsync(
        AccessContext context,
        Janus.Authorization.Gate.RoleChange change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(change);

        if (context.Acting is null || gate.Administrative is not OrganizationId administrative)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (gate.Admits(context, Permissions.SystemAdminister, administrative))
        {
            return Result.Success();
        }

        IEnumerable<Permission> carried = (change.Before?.Permissions ?? [])
            .Concat(change.After?.Permissions ?? []);

        return carried.All(permission => gate.Admits(context, permission, administrative))
            ? await AdmitAsync(
                    context,
                    new HandOut(Permissions.RoleManage, administrative, Resource: null, change.HandedOut, Widened: []),
                    cancellationToken)
                .ConfigureAwait(false)
            : Result.Failure(Error.From(ErrorCodes.RoleNotAssignable));
    }
}
