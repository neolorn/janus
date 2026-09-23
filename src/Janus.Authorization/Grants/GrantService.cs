using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Authorization.Groups;
using Janus.Authorization.Resources;
using Janus.Authorization.Roles;
using Janus.Core;

namespace Janus.Authorization.Grants;

/// <summary>
/// Stored grants, written and revoked by an administrator.
/// </summary>
/// <param name="gate">Whether the caller may manage grants where the grant is scoped.</param>
/// <param name="administrative">Where system administration is held.</param>
/// <param name="stepUp">What granting and revoking ask of the caller's session.</param>
/// <param name="grants">Where grants are read and written.</param>
/// <param name="roles">Where the role a grant names is read.</param>
/// <param name="groups">Where a group a grant is given to is read.</param>
/// <param name="resources">Where the record a grant is on, and its organization, are read.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, AUTHZ-GRANT-001 to AUTHZ-GRANT-004 and OPS-CFG-007. The
/// organization a grant is scoped to is read from the record it is on, never from the
/// caller, and the permission is asked there. A role carrying system administration is
/// granted and revoked only by a system administrator, a deny as much as an allow,
/// since a deny takes it away.
/// </remarks>
internal sealed class GrantService(
    IAccessGate gate,
    IAdministrativeOrganization administrative,
    IStepUpGate stepUp,
    IGrantStore grants,
    IRoleStore roles,
    IGroupStore groups,
    IResourceStore resources,
    IUnitOfWork work,
    TimeProvider time) : IGrants
{
    // The type the gate asks an organization-wide question on, which is how a request
    // names the whole organization rather than one record in it.
    private static readonly ResourceType OrganizationWide = ResourceType.Parse("organization");

    /// <inheritdoc/>
    public async ValueTask<Result<GrantId>> GrantAsync(
        AccessContext context,
        SessionId session,
        GrantRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure<GrantId>(Error.From(ErrorCodes.Denied));
        }

        if (await ScopeAsync(request.On, cancellationToken).ConfigureAwait(false)
            is not OrganizationId organization)
        {
            return Result.Failure<GrantId>(Malformed("resourceId"));
        }

        if (await ManagingRefusedAsync(context, organization, cancellationToken).ConfigureAwait(false)
            is Error denied)
        {
            return Result.Failure<GrantId>(denied);
        }

        Role? role = await roles.FindAsync(request.Role, cancellationToken).ConfigureAwait(false);

        if (role is null)
        {
            return Result.Failure<GrantId>(Malformed("role"));
        }

        if (await AdministeringRefusedAsync(context, role, cancellationToken).ConfigureAwait(false)
            is Error administering)
        {
            return Result.Failure<GrantId>(administering);
        }

        if (!await HolderAsync(request.Subject, organization, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<GrantId>(Malformed("subjectId"));
        }

        if (Unstated(request.Reason) is Error unstated)
        {
            return Result.Failure<GrantId>(unstated);
        }

        DateTimeOffset now = time.GetUtcNow();

        if (request.ExpiresAt <= now)
        {
            return Result.Failure<GrantId>(Error.From(ErrorCodes.GrantExpired));
        }

        if (await SteppedUpAsync(acting, session, cancellationToken).ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure<GrantId>(challenged);
        }

        return await Grant
            .Create(
                GrantId.New(time),
                request.Subject,
                request.Role,
                organization,
                request.On.Type == OrganizationWide ? null : request.On,
                request.Deny,
                GrantKind.Stored,
                request.ExpiresAt,
                acting,
                now,
                request.Reason)
            .Match(
                grant => WrittenAsync(grant, now, cancellationToken),
                refused => ValueTask.FromResult(Result.Failure<GrantId>(refused)))
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RevokeAsync(
        AccessContext context,
        SessionId session,
        GrantId grant,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reason);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        // A derived or materialised grant is the host's data speaking, and a refresh
        // would write it back; only a row someone wrote is revoked by someone.
        if (await grants.FindAsync(grant, cancellationToken).ConfigureAwait(false)
            is not { Kind: GrantKind.Stored, RevokedAt: null } held)
        {
            return Result.Failure(Error.From(ErrorCodes.GrantNotFound));
        }

        if (await ManagingRefusedAsync(context, held.Organization, cancellationToken).ConfigureAwait(false)
            is Error denied)
        {
            return Result.Failure(denied);
        }

        if (await AdministeringRefusedAsync(
                context,
                await roles.FindAsync(held.Role, cancellationToken).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false)
            is Error administering)
        {
            return Result.Failure(administering);
        }

        if (Unstated(reason) is Error unstated)
        {
            return Result.Failure(unstated);
        }

        if (await SteppedUpAsync(acting, session, cancellationToken).ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (held.Revoke(acting, time.GetUtcNow(), reason).Match<Error?>(() => null, error => error)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        await grants.RecordAsync(held, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    // API-CONV-002: a free-text field is 1 to 1024 characters after trimming. A blank
    // reason is the refusal 10 names for a grant; one past the limit is a request the
    // boundary does not read.
    private static Error? Unstated(string reason) =>
        (reason?.Trim().Length ?? 0) switch
        {
            0 => Error.From(ErrorCodes.GrantReasonRequired),
            > 1024 => Malformed("reason"),
            _ => null,
        };

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    // AUTHZ-GRANT-001 AC2: the whole organization is named by its identifier; a record
    // is scoped to the organization it was registered in.
    private async ValueTask<OrganizationId?> ScopeAsync(
        ResourceReference on,
        CancellationToken cancellationToken)
    {
        if (on.Type == OrganizationWide)
        {
            return Guid.TryParse(on.Id.ToString(), out Guid organization)
                ? new OrganizationId(organization)
                : null;
        }

        return (await resources.FindAsync(on, cancellationToken).ConfigureAwait(false))?.Organization;
    }

    // A group belongs to one organization, and its members hold what it holds there.
    private async ValueTask<bool> HolderAsync(
        GrantSubject subject,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        subject.Type != SubjectType.Group
            || (await groups.FindAsync(new GroupId(subject.Value), cancellationToken).ConfigureAwait(false))
                ?.Organization == organization;

    private async ValueTask<Error?> ManagingRefusedAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        (await gate
            .RequireAsync(context, Permissions.GrantManage, organization, cancellationToken)
            .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);

    // OPS-CFG-007: a role nobody can read is taken to carry it, so the check fails
    // closed.
    private async ValueTask<Error?> AdministeringRefusedAsync(
        AccessContext context,
        Role? role,
        CancellationToken cancellationToken)
    {
        if (role is not null && !role.Allows(Permissions.SystemAdminister))
        {
            return null;
        }

        if (await administrative.FindAsync(cancellationToken).ConfigureAwait(false)
            is not OrganizationId organization)
        {
            return Error.From(ErrorCodes.Denied);
        }

        return (await gate
                .RequireAsync(context, Permissions.SystemAdminister, organization, cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);
    }

    private async ValueTask<Result<GrantId>> WrittenAsync(
        Grant grant,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (await grants.ExistsAsync(grant, now, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<GrantId>(Error.From(ErrorCodes.GrantDuplicate));
        }

        await grants.CreateAsync(grant, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(grant.Id);
    }

    private async ValueTask<Error?> SteppedUpAsync(
        SubjectId acting,
        SessionId session,
        CancellationToken cancellationToken) =>
        (await stepUp
            .RequireAsync(acting, session, StepUpAction.GrantManage, cancellationToken)
            .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);
}
