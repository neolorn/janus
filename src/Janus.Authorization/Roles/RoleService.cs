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
/// <param name="grants">Which grants the reserved account holds.</param>
/// <param name="references">Whether any grant, standing invitation or other role names a role.</param>
/// <param name="administrative">Which organization administers the deployment.</param>
/// <param name="emergency">Which account the break-glass session belongs to.</param>
/// <param name="handOut">Whether a change stays within the reach of the caller making it.</param>
/// <param name="model">Which permissions exist, and which roles a derivation confers.</param>
/// <param name="audit">Where every change is written down.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, AUTHZ-GRANT-004, AUTHZ-GRANT-005, OPS-CFG-007 and OPS-BOOT-002.
/// A role is the deployment's rather than one organization's, so it is managed in the
/// administrative organization, with the roles its holders may hand out (D-212).
/// Its permissions are read live wherever access is worked out, so a change takes
/// effect on the next request with nothing to invalidate.
/// </remarks>
internal sealed class RoleService(
    AdministrativeScope scope,
    IStepUpGate stepUp,
    IRoleStore roles,
    IGrantStore grants,
    IRoleReferences references,
    IAdministrativeOrganization administrative,
    IEmergencyAccount emergency,
    IHandOutAdmission handOut,
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

        // AUTHZ-GRANT-004 (D-204): roles are a person's to manage; a system principal,
        // the start step's included, reaches them through none of these operations.
        if (context.Acting is null)
        {
            return Result.Failure<IReadOnlyList<DefinedRole>>(Error.From(ErrorCodes.Denied));
        }

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
        ArgumentNullException.ThrowIfNull(role.HandsOut);

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

        var defined = Role.Of(role.Name, role.Permissions, role.HandsOut);

        // CONV-DESIGN-002, D-207: every refusal the step-up must follow is judged first
        // on the role as it stands without a lock, so the step-up, which may ask the
        // carrier's signal, is judged before the unit of work begins.
        Role? found = await roles.FindAsync(role.Name, cancellationToken).ConfigureAwait(false);
        Error? unmet = await DefinitionRefusedAsync(context, found, defined, cancellationToken).ConfigureAwait(false)
            ?? await UnresolvedAsync(defined, cancellationToken).ConfigureAwait(false)
            ?? await ReachRefusedAsync(context, found, defined, cancellationToken).ConfigureAwait(false)
            ?? await SteppedUpAsync(acting, session, cancellationToken).ConfigureAwait(false);

        if (unmet is not null)
        {
            return Result.Failure<bool>(unmet);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<bool>(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await scope.RefusedAsync(context, Permissions.RoleManage, cancellationToken).ConfigureAwait(false)
            is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<bool>(since);
        }

        // D-166 X3: the role is read again under its row's lock, so two definitions at once
        // are made one after the other and the second is judged on what the first left; the
        // roles it hands out are held with it, in one order, so none is removed meanwhile.
        Role? held = await HeldAsync(defined, cancellationToken).ConfigureAwait(false);

        if ((await DefinitionRefusedAsync(context, held, defined, cancellationToken).ConfigureAwait(false)
                ?? await UnresolvedAsync(defined, cancellationToken).ConfigureAwait(false)
                ?? await ReachRefusedAsync(context, held, defined, cancellationToken).ConfigureAwait(false))
            is Error refusedSince)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<bool>(refusedSince);
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
                held is null ? null : Defined(held),
                Defined(defined),
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

        // CONV-DESIGN-002, D-207: every refusal the step-up must follow is judged first
        // on the role as it stands without a lock, so the step-up, which may ask the
        // carrier's signal, is judged before the unit of work begins.
        Role? found = await roles.FindAsync(role, cancellationToken).ConfigureAwait(false);
        Error? unmet = await RemovalRefusedAsync(context, role, found, cancellationToken).ConfigureAwait(false)
            ?? await ReachRefusedAsync(context, found, defined: null, cancellationToken).ConfigureAwait(false)
            ?? await SteppedUpAsync(acting, session, cancellationToken).ConfigureAwait(false);

        if (unmet is not null)
        {
            return Result.Failure(unmet);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await scope.RefusedAsync(context, Permissions.RoleManage, cancellationToken).ConfigureAwait(false)
            is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // D-166 X3: as for a definition; an invitation's issue and a grant hold the row
        // too, so none comes to name a role removed meanwhile.
        Role? held = await roles.FindForUpdateAsync(role, cancellationToken).ConfigureAwait(false);

        if ((await RemovalRefusedAsync(context, role, held, cancellationToken).ConfigureAwait(false)
                ?? await ReachRefusedAsync(context, held, defined: null, cancellationToken).ConfigureAwait(false))
            is Error refusedSince)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(refusedSince);
        }

        await roles.RemoveAsync(role, cancellationToken).ConfigureAwait(false);
        await audit
            .RemovedAsync(
                Defined(held!),
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
        new(
            role.Name,
            [.. role.Permissions.OrderBy(permission => permission.ToString(), StringComparer.Ordinal)],
            [.. role.HandsOut.OrderBy(handed => handed.ToString(), StringComparer.Ordinal)]);

    // AUTHZ-GRANT-005 (D-212): a role hands out roles the deployment holds; one naming
    // itself names a role that stands once it is defined. Inside the unit of work the
    // roles are read once their rows are held.
    private async ValueTask<Error?> UnresolvedAsync(Role defined, CancellationToken cancellationToken)
    {
        foreach (RoleName handed in defined.HandsOut.Where(handed => handed != defined.Name))
        {
            if (await roles.FindAsync(handed, cancellationToken).ConfigureAwait(false) is null)
            {
                return Error.From(ErrorCodes.GrantUnresolved, "member", JsonSerializer.SerializeToElement("handsOut"));
            }
        }

        return null;
    }

    // D-166 X3: the role and the roles it hands out are taken under their rows' locks in
    // the order of their names, so two definitions naming each other at once are made one
    // after the other; the role as it stood is what is answered.
    private async ValueTask<Role?> HeldAsync(Role defined, CancellationToken cancellationToken)
    {
        Role? held = null;

        foreach (RoleName name in defined.HandsOut
            .Append(defined.Name)
            .Distinct()
            .OrderBy(name => name.ToString(), StringComparer.Ordinal))
        {
            Role? read = await roles.FindForUpdateAsync(name, cancellationToken).ConfigureAwait(false);

            if (name == defined.Name)
            {
                held = read;
            }
        }

        return held;
    }

    // API-CONV-002: a free-text field is 1 to 1024 characters after trimming.
    private static string? Stated(string reason) =>
        reason?.Trim() is { Length: > 0 and <= 1024 } stated ? stated : null;

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

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

    // The refusals a definition meets before its step-up, judged on the role as it stands
    // where it is held: a role that carries system administration before or after the
    // change, by a caller who is no system administrator (OPS-CFG-007); and the
    // reserved account's role losing a permission the library declares (OPS-BOOT-002,
    // D-166), which keeps every one of them so that the break-glass session does, a
    // permission the host declares still being one it may gain.
    private async ValueTask<Error?> DefinitionRefusedAsync(
        AccessContext context,
        Role? held,
        Role defined,
        CancellationToken cancellationToken)
    {
        if (await AdministeringRefusedAsync(context, [held, defined], cancellationToken).ConfigureAwait(false)
            is Error administering)
        {
            return administering;
        }

        return !Permissions.All.All(defined.Allows)
            && await ReservedHoldsAsync(defined.Name, cancellationToken).ConfigureAwait(false)
                ? Error.From(ErrorCodes.Denied)
                : null;
    }

    // The refusals a removal meets before its step-up, judged on the role as it stands
    // where it is held: a role the deployment does not hold, named by a path that names
    // no record, and under /admin nothing is concealed (X5, D-166); one that carries
    // system administration, by a caller who is no system administrator (OPS-CFG-007); and
    // one a grant's history, a derivation or a standing invitation names, none of
    // which is left naming nothing (AUTHZ-GRANT-003 AC3, REG-INV-001).
    private async ValueTask<Error?> RemovalRefusedAsync(
        AccessContext context,
        RoleName role,
        Role? held,
        CancellationToken cancellationToken)
    {
        if (held is null)
        {
            return Error.From(ErrorCodes.RoleNotFound);
        }

        if (await AdministeringRefusedAsync(context, [held], cancellationToken).ConfigureAwait(false)
            is Error administering)
        {
            return administering;
        }

        return Derived(role) || await references.NamedAsync(role, cancellationToken).ConfigureAwait(false)
            ? Error.From(ErrorCodes.RoleInUse)
            : null;
    }

    // AUTHZ-GRANT-005 (D-212): a change stays within the caller's reach, what the role
    // carries before and after it and the roles it hands out after it, each as the
    // deployment holds it; a role handing itself out hands out what the change leaves.
    private async ValueTask<Error?> ReachRefusedAsync(
        AccessContext context,
        Role? held,
        Role? defined,
        CancellationToken cancellationToken)
    {
        List<DefinedRole> handedOut = [];

        foreach (RoleName name in (defined?.HandsOut ?? Enumerable.Empty<RoleName>())
            .OrderBy(name => name.ToString(), StringComparer.Ordinal))
        {
            Role read = defined is Role after && name == after.Name
                ? after
                : await roles.FindAsync(name, cancellationToken).ConfigureAwait(false) ?? Role.Of(name, []);

            handedOut.Add(Defined(read));
        }

        return (await handOut
                .AdmitAsync(
                    context,
                    new RoleChange(
                        held is null ? null : Defined(held),
                        defined is null ? null : Defined(defined),
                        handedOut),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);
    }

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
