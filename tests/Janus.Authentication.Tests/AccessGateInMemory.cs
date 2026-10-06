using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests;

/// <summary>
/// The gate as an authentication test needs it: it grants what a test grants within
/// an organization and nothing on a record, so every path that is not the one under
/// test is refused rather than quietly allowed. A refusal within an organization is
/// kept under an identifier of its own, as the gate records it, so that resolving one
/// can be asked of it.
/// </summary>
internal sealed class AccessGateInMemory : IAccessGate
{
    private readonly HashSet<(SubjectId Subject, OrganizationId Organization, Permission Permission)> _granted = [];
    private readonly HashSet<SubjectId> _restricted = [];
    private readonly HashSet<(OrganizationId Organization, Permission Permission)> _everyone = [];
    private readonly List<(AuditRecordId Correlation, AccessExplanation Explanation)> _refusals = [];
    private readonly Dictionary<ResourceReference, (OrganizationId Organization, ResourceAccess Access)> _registered = [];

    /// <summary>
    /// Gets or sets the organization a support role resolves a refusal in.
    /// </summary>
    public OrganizationId? Administrative { get; set; }

    /// <summary>
    /// Gets the identifiers of the refusals within an organization, oldest first.
    /// </summary>
    public IReadOnlyList<AuditRecordId> Refusals => [.. _refusals.Select(refusal => refusal.Correlation)];

    /// <summary>
    /// Grants a principal a permission within an organization.
    /// </summary>
    /// <param name="subject">The principal.</param>
    /// <param name="organization">The organization.</param>
    /// <param name="permission">The permission.</param>
    public void Grant(SubjectId subject, OrganizationId organization, Permission permission) =>
        _granted.Add((subject, organization, permission));

    /// <summary>
    /// Gets or sets what happens once the gate next admits a modifying action, where a
    /// test sets it: what follows an operation's gate step. It happens once, and is
    /// handed the account admitted.
    /// </summary>
    public Action<SubjectId>? Admitted { get; set; }

    /// <summary>
    /// Restricts a principal's processing, as a restriction that committed does: every
    /// modifying permission is refused it from then on, and a reading one is not
    /// (AUTHZ-GATE-006).
    /// </summary>
    /// <param name="subject">The principal.</param>
    public void Restrict(SubjectId subject) => _restricted.Add(subject);

    /// <summary>
    /// Lifts the restriction on a principal's processing.
    /// </summary>
    /// <param name="subject">The principal.</param>
    public void Lift(SubjectId subject) => _restricted.Remove(subject);

    /// <summary>
    /// Takes back a permission granted to a principal within an organization.
    /// </summary>
    /// <param name="subject">The principal.</param>
    /// <param name="organization">The organization.</param>
    /// <param name="permission">The permission.</param>
    public void Revoke(SubjectId subject, OrganizationId organization, Permission permission) =>
        _granted.Remove((subject, organization, permission));

    /// <summary>
    /// Holds a record as registered in an organization, with the answer the view of
    /// who can access it gives a principal holding <c>grant:read</c> there.
    /// </summary>
    /// <param name="organization">The organization the record sits in.</param>
    /// <param name="access">Who can access the record.</param>
    public void Register(OrganizationId organization, ResourceAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);

        _registered[access.Resource] = (organization, access);
    }

    /// <summary>
    /// Grants every principal a permission within an organization, for a test that is
    /// not about who holds it.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <param name="permission">The permission.</param>
    public void GrantEveryone(OrganizationId organization, Permission permission) =>
        _everyone.Add((organization, permission));

    /// <inheritdoc/>
    public ValueTask<Result> RequireAsync(
        AccessContext context,
        Permission permission,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Restricted(context, permission))
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.Restricted)));
        }

        if (context.Effective is SubjectId subject
            && (_granted.Contains((subject, organization, permission)) || _everyone.Contains((organization, permission))))
        {
            return ValueTask.FromResult(Admit(subject, permission));
        }

        _refusals.Add((
            AuditRecordId.New(TimeProvider.System),
            new AccessExplanation(
                AccessOutcome.Denied,
                permission,
                new ExplainedPrincipal(context.Acting, context.Effective, context.Principal?.Name, context.Principal?.Reason),
                Grant: null)));

        return ValueTask.FromResult(Refused());
    }

    /// <inheritdoc/>
    public ValueTask<Result> RequireAsync(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Refused());

    /// <inheritdoc/>
    public ValueTask<Result> RequireAsync<TResource>(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Refused());

    /// <inheritdoc/>
    public ValueTask<Result<Expression<Func<TResource, bool>>>> FilterAsync<TResource>(
        AccessContext context,
        Permission permission,
        ResourceType type,
        OrganizationId organization,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success<Expression<Func<TResource, bool>>>(_ => false));

    /// <inheritdoc/>
    public ValueTask<Result<SqlFilter>> FragmentAsync(
        AccessContext context,
        Permission permission,
        ResourceType type,
        OrganizationId organization,
        string rowAlias,
        string column,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(
            new SqlFilter("false", new Dictionary<string, object>(StringComparer.Ordinal))));

    /// <inheritdoc/>
    public ValueTask<Result<AccessExplanation>> ExplainAsync(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ValueTask.FromResult(Result.Success(new AccessExplanation(
            AccessOutcome.Denied,
            permission,
            new ExplainedPrincipal(context.Acting, context.Effective, context.Principal?.Name, context.Principal?.Reason),
            Grant: null)));
    }

    /// <inheritdoc/>
    public ValueTask<Result<AccessExplanation>> ExplainAsync<TResource>(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken) =>
        ExplainAsync(context, permission, resource, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>
    /// Nothing is granted on a record here, so the whole of an organization is answered
    /// with no grant, and a record a test registered with the answer it was registered
    /// with, each to a principal holding <c>grant:read</c> in the organization.
    /// </remarks>
    public ValueTask<Result<ResourceAccess>> WhoCanAccessAsync(
        AccessContext context,
        ResourceReference resource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_registered.TryGetValue(resource, out (OrganizationId Organization, ResourceAccess Access) registered))
        {
            return ValueTask.FromResult(
                context.Effective is SubjectId reader
                && _granted.Contains((reader, registered.Organization, Permissions.GrantRead))
                    ? Result.Success(registered.Access)
                    : Result.Failure<ResourceAccess>(Error.From(ErrorCodes.Denied)));
        }

        return ValueTask.FromResult(
            resource.Type == ResourceType.Parse("organization")
            && Guid.TryParse(resource.Id.ToString(), out Guid organization)
            && context.Effective is SubjectId subject
            && _granted.Contains((subject, new OrganizationId(organization), Permissions.GrantRead))
                ? Result.Success(new ResourceAccess(resource, [], Partial: false, Unevaluated: []))
                : Result.Failure<ResourceAccess>(Error.From(ErrorCodes.Denied)));
    }

    /// <inheritdoc/>
    public ValueTask<Result<ResourceAccess>> WhoCanAccessAsync<TResource>(
        AccessContext context,
        ResourceReference resource,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken) =>
        WhoCanAccessAsync(context, resource, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<AccessExplanation>> ResolveAsync(
        AccessContext context,
        AuditRecordId correlation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ValueTask.FromResult(
            Administrative is OrganizationId organization
            && context.Effective is SubjectId subject
            && _granted.Contains((subject, organization, Permissions.AuditRead))
                ? Recorded(correlation, _ => true)
                : Result.Failure<AccessExplanation>(Error.From(ErrorCodes.Denied)));
    }

    /// <inheritdoc/>
    public ValueTask<Result<AccessExplanation>> ResolveOwnAsync(
        AccessContext context,
        AuditRecordId correlation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Every refusal kept here is within an organization, which conceals nothing.
        return ValueTask.FromResult(Recorded(
            correlation,
            explanation => context.Acting is not null
                && explanation.Principal == new ExplainedPrincipal(context.Acting, context.Effective, context.Principal?.Name, context.Principal?.Reason)));
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<Capability>>> CapabilitiesAsync(
        AccessContext context,
        ResourceType type,
        IReadOnlyList<ResourceId> resources,
        IReadOnlyList<Permission> permissions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resources);

        return ValueTask.FromResult(Result.Success<IReadOnlyList<Capability>>(
        [
            .. resources.Select(resource => new Capability(
                resource,
                new HashSet<Permission>(),
                new Dictionary<Permission, IReadOnlySet<CapabilityResidual>>())),
        ]));
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<Capability>>> CapabilitiesAsync<TResource>(
        AccessContext context,
        ResourceType type,
        IReadOnlyList<ResourceId> resources,
        IReadOnlyList<Permission> permissions,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken) =>
        CapabilitiesAsync(context, type, resources, permissions, cancellationToken);

    private static Result Refused() => Result.Failure(Error.From(ErrorCodes.Denied));

    // What a test set to follow the gate step runs once, after a modifying action is
    // admitted and before the caller is answered.
    private Result Admit(SubjectId subject, Permission permission)
    {
        if (permission.Action is not ("read" or "list" or "export") && Admitted is Action<SubjectId> admitted)
        {
            Admitted = null;
            admitted(subject);
        }

        return Result.Success();
    }

    // AUTHZ-GATE-006: an action named read, list or export reads; every other modifies.
    private bool Restricted(AccessContext context, Permission permission) =>
        context.Effective is SubjectId subject
        && _restricted.Contains(subject)
        && permission.Action is not ("read" or "list" or "export");

    private Result<AccessExplanation> Recorded(AuditRecordId correlation, Func<AccessExplanation, bool> resolves) =>
        _refusals.Find(refusal => refusal.Correlation == correlation) is { Explanation: { } explanation }
        && resolves(explanation)
            ? Result.Success(explanation)
            : Result.Failure<AccessExplanation>(Error.From(ErrorCodes.Denied));
}
