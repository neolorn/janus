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
/// <param name="grants">Whether any grant names a role.</param>
/// <param name="model">Which permissions exist, and which roles a derivation confers.</param>
/// <param name="audit">Where every change is written down.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, AUTHZ-GRANT-004 and OPS-CFG-007. A role is the deployment's
/// rather than one organization's, so it is managed in the administrative organization.
/// Its permissions are read live wherever access is worked out, so a change takes
/// effect on the next request with nothing to invalidate.
/// </remarks>
internal sealed class RoleService(
    AdministrativeScope scope,
    IStepUpGate stepUp,
    IRoleStore roles,
    IGrantStore grants,
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

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        Role? held = await roles.FindAsync(role.Name, cancellationToken).ConfigureAwait(false);
        var defined = Role.Of(role.Name, role.Permissions);

        if (await AdministeringRefusedAsync(context, [held, defined], cancellationToken).ConfigureAwait(false)
            is Error administering)
        {
            return Result.Failure<bool>(administering);
        }

        if (await SteppedUpAsync(acting, session, cancellationToken).ConfigureAwait(false) is Error challenged)
        {
            return Result.Failure<bool>(challenged);
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
                time.GetUtcNow(),
                cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (await roles.FindAsync(role, cancellationToken).ConfigureAwait(false) is not Role held)
        {
            return Result.Failure(Malformed("name"));
        }

        if (await AdministeringRefusedAsync(context, [held], cancellationToken).ConfigureAwait(false)
            is Error administering)
        {
            return Result.Failure(administering);
        }

        // AUTHZ-GRANT-003 AC3: a grant's history names its role, revoked or not, and a
        // derivation confers it from the host's data; neither is left naming nothing.
        if (Derived(role) || await grants.NamesAsync(role, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(Error.From(ErrorCodes.RoleInUse));
        }

        if (await SteppedUpAsync(acting, session, cancellationToken).ConfigureAwait(false) is Error challenged)
        {
            return Result.Failure(challenged);
        }

        await roles.RemoveAsync(role, cancellationToken).ConfigureAwait(false);
        await audit
            .RemovedAsync(
                role,
                Defined(held).Permissions,
                stated,
                acting,
                time.GetUtcNow(),
                cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static DefinedRole Defined(Role role) =>
        new(role.Name, [.. role.Permissions.OrderBy(permission => permission.ToString(), StringComparer.Ordinal)]);

    // API-CONV-002: a free-text field is 1 to 1024 characters after trimming.
    private static string? Stated(string reason) =>
        reason?.Trim() is { Length: > 0 and <= 1024 } stated ? stated : null;

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

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
