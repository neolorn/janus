using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Tests;

/// <summary>
/// The gate as a privacy test needs it: it grants what a test grants within
/// an organization and nothing on a record, so every path that is not the one under
/// test is refused rather than quietly allowed.
/// </summary>
internal sealed class AccessGateInMemory : IAccessGate
{
    private readonly HashSet<(SubjectId Subject, OrganizationId Organization, Permission Permission)> _granted = [];

    /// <summary>
    /// Grants a principal a permission within an organization.
    /// </summary>
    /// <param name="subject">The principal.</param>
    /// <param name="organization">The organization.</param>
    /// <param name="permission">The permission.</param>
    public void Grant(SubjectId subject, OrganizationId organization, Permission permission) =>
        _granted.Add((subject, organization, permission));

    /// <inheritdoc/>
    public ValueTask<Result> RequireAsync(
        AccessContext context,
        Permission permission,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ValueTask.FromResult(
            context.Effective is SubjectId subject
            && _granted.Contains((subject, organization, permission))
                ? Result.Success()
                : Refused());
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
            new ExplainedPrincipal(context.Acting, context.Effective),
            Grant: null)));
    }

    /// <inheritdoc/>
    public ValueTask<Result<AccessExplanation>> ResolveAsync(
        AccessContext context,
        OrganizationId organization,
        AuditRecordId correlation,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Failure<AccessExplanation>(Error.From(ErrorCodes.Denied)));

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
}
