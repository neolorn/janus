using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Authorization.Grants;
using Janus.Authorization.Model;
using Janus.Core;

namespace Janus.Authorization.Roles;

/// <summary>
/// The roles grants confer, defined and removed by an administrator at runtime.
/// </summary>
/// <param name="scope">Whether the caller may manage roles, and holds system administration.</param>
/// <param name="stepUp">What defining and removing ask of the caller's session.</param>
/// <param name="roles">Where roles are read and written.</param>
/// <param name="grants">Whether any grant names a role, and which the reserved account holds.</param>
/// <param name="administrative">Which organization administers the deployment.</param>
/// <param name="emergency">Which account the break-glass session belongs to.</param>
/// <param name="model">Which permissions exist, and which roles a derivation confers.</param>
/// <param name="audit">Where every change is written down.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, AUTHZ-GRANT-004, OPS-CFG-007 and OPS-BOOT-002. A role is the
/// deployment's rather than one organization's, so it is managed in the administrative
/// organization.
/// Its permissions are read live wherever access is worked out, so a change takes
/// effect on the next request with nothing to invalidate.
/// </remarks>
internal sealed class RoleService(
    AdministrativeScope scope,
    IStepUpGate stepUp,
    IRoleStore roles,
    IGrantStore grants,
    IAdministrativeOrganization administrative,
    IEmergencyAccount emergency,
    AuthorizationModel model,
    IRoleAudit audit,
    IUnitOfWork work,
    TimeProvider time) : IRoles
{
    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<DefinedRole>>> AllAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope.RefusedAsync(context, Permissions.RoleManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure<IReadOnlyList<DefinedRole>>(refused);
        }

        return Result.Success<IReadOnlyList<DefinedRole>>(
        [
            .. (await roles.AllAsync(cancellationToken).ConfigureAwait(false)).Select(Defined),
        ]);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<bool>> DefineAsync(
        AccessContext context,
        SessionId session,
        DefinedRole role,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(role.Permissions);

        // A change is made by a person, in their own session: a system principal has
        // none to step up.
        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure<bool>(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.RoleManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure<bool>(refused);
        }

        // AUTHZ-MODEL-004: a role grants only what the model declares, the library's
        // permissions and the host's.
        if (!role.Permissions.All(model.Declares))
        {
            return Result.Failure<bool>(Malformed("permissions"));
        }

        if (Stated(reason) is not string stated)
        {
            return Result.Failure<bool>(Malformed("reason"));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<bool>(notBegun);
        }

        Role? held = await roles.FindAsync(role.Name, cancellationToken).ConfigureAwait(false);
        var defined = Role.Of(role.Name, role.Permissions);

        if (await AdministeringRefusedAsync(context, [held, defined], cancellationToken).ConfigureAwait(false)
            is Error administering)
        {
            return await EndedAsync<bool>(administering, cancellationToken).ConfigureAwait(false);
        }

        // OPS-BOOT-002, D-166: the break-glass session holds what the reserved account's
        // role allows, so that role keeps every permission the library declares. A
        // permission the host declares may still be added to it.
        if (!Permissions.All.All(defined.Allows)
            && await ReservedHoldsAsync(role.Name, cancellationToken).ConfigureAwait(false))
        {
            return await EndedAsync<bool>(Error.From(ErrorCodes.Denied), cancellationToken).ConfigureAwait(false);
        }

        if (await SteppedUpAsync(acting, session, cancellationToken).ConfigureAwait(false) is Error challenged)
        {
            return await EndedAsync<bool>(challenged, cancellationToken).ConfigureAwait(false);
        }

        if (held is null)
        {
            await roles.CreateAsync(defined, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await roles.RecordAsync(defined, cancellationToken).ConfigureAwait(false);
        }

        await audit
            .DefinedAsync(
                role.Name,
                held is null ? null : Defined(held).Permissions,
                Defined(defined).Permissions,
                stated,
                acting,
                context.BreakGlassReason,
                time.GetUtcNow(),
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<bool>(notCommitted);
        }

        return Result.Success(held is null);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RemoveAsync(
        AccessContext context,
        SessionId session,
        RoleName role,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.RoleManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        if (Stated(reason) is not string stated)
        {
            return Result.Failure(Malformed("reason"));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // X5, D-166: a path naming a role the deployment does not hold names no record,
        // and under /admin nothing is concealed.
        if (await roles.FindAsync(role, cancellationToken).ConfigureAwait(false) is not Role held)
        {
            return await EndedAsync(Error.From(ErrorCodes.RoleNotFound), cancellationToken).ConfigureAwait(false);
        }

        if (await AdministeringRefusedAsync(context, [held], cancellationToken).ConfigureAwait(false)
            is Error administering)
        {
            return await EndedAsync(administering, cancellationToken).ConfigureAwait(false);
        }

        // AUTHZ-GRANT-003 AC3: a grant's history names its role, revoked or not, and a
        // derivation confers it from the host's data; neither is left naming nothing.
        if (Derived(role) || await grants.NamesAsync(role, cancellationToken).ConfigureAwait(false))
        {
            return await EndedAsync(Error.From(ErrorCodes.RoleInUse), cancellationToken).ConfigureAwait(false);
        }

        if (await SteppedUpAsync(acting, session, cancellationToken).ConfigureAwait(false) is Error challenged)
        {
            return await EndedAsync(challenged, cancellationToken).ConfigureAwait(false);
        }

        await roles.RemoveAsync(role, cancellationToken).ConfigureAwait(false);
        await audit
            .RemovedAsync(
                role,
                Defined(held).Permissions,
                stated,
                acting,
                context.BreakGlassReason,
                time.GetUtcNow(),
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    private static DefinedRole Defined(Role role) =>
        new(role.Name, [.. role.Permissions.OrderBy(permission => permission.ToString(), StringComparer.Ordinal)]);

    // API-CONV-002: a free-text field is 1 to 1024 characters after trimming.
    private static string? Stated(string reason) =>
        reason?.Trim() is { Length: > 0 and <= 1024 } stated ? stated : null;

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    // X9: a refusal made inside the unit of work writes nothing of the operation, so
    // the unit of work is ended before the refusal returns, which leaves the scope
    // clean for the next operation.
    private async ValueTask<Result> EndedAsync(Error refusal, CancellationToken cancellationToken)
    {
        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Failure(refusal);
    }

    private async ValueTask<Result<TValue>> EndedAsync<TValue>(Error refusal, CancellationToken cancellationToken) =>
        (await EndedAsync(refusal, cancellationToken).ConfigureAwait(false))
            .Match(() => Result.Failure<TValue>(refusal), Result.Failure<TValue>);

    // Bootstrap grants the reserved account its role in the administrative
    // organization, and nothing grants it anything further (OPS-BOOT-002).
    private async ValueTask<bool> ReservedHoldsAsync(RoleName role, CancellationToken cancellationToken)
    {
        if (await emergency.FindAsync(cancellationToken).ConfigureAwait(false) is not SubjectId reserved
            || await administrative.FindAsync(cancellationToken).ConfigureAwait(false)
                is not OrganizationId organization)
        {
            return false;
        }

        IReadOnlyList<Grant> held = await grants
            .HeldByAsync([GrantSubject.Of(reserved)], organization, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        return held.Any(grant => grant.Role == role && !grant.Deny);
    }

    private bool Derived(RoleName role) =>
        model.ResourceTypes.Any(type => type.Derivations.Any(derivation => derivation.Role == role));

    // OPS-CFG-007: a role that carries system administration before or after the change
    // is changed only by a system administrator.
    private async ValueTask<Error?> AdministeringRefusedAsync(
        AccessContext context,
        IEnumerable<Role?> touched,
        CancellationToken cancellationToken) =>
        touched.Any(role => role?.Allows(Permissions.SystemAdminister) == true)
            ? await scope.RefusedAsync(context, Permissions.SystemAdminister, cancellationToken).ConfigureAwait(false)
            : null;

    private async ValueTask<Error?> SteppedUpAsync(
        SubjectId acting,
        SessionId session,
        CancellationToken cancellationToken) =>
        (await stepUp
            .RequireAsync(acting, session, StepUpAction.GrantManage, cancellationToken)
            .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);
}
