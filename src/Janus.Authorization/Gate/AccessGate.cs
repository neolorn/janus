using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Model;
using Janus.Authorization.Resources;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// The one place a permission is evaluated.
/// </summary>
/// <param name="model">The host's declared domain, read for containment and concealment.</param>
/// <param name="records">Where a record's organization is read from.</param>
/// <param name="evaluator">Where a rendered rule is run.</param>
/// <param name="subjects">Who the principal is, resolved once per operation.</param>
/// <param name="time">The clock liveness is read against.</param>
/// <remarks>
/// Implements AUTHZ-SEAM-001, AUTHZ-PRIN-001, AUTHZ-PRIN-003, AUTHZ-GATE-002,
/// AUTHZ-GATE-004, AUTHZ-GATE-005, AUTHZ-SCOPE-001 and LIB-SEAM-001. A check and a
/// filter are the one rule rendered two ways, so neither can come to answer what the
/// other would refuse. Every path that cannot resolve what it needs denies.
/// </remarks>
internal sealed class AccessGate(
    AuthorizationModel model,
    IResourceStore records,
    IAccessEvaluator evaluator,
    SubjectSets subjects,
    TimeProvider time) : IAccessGate
{
    /// <inheritdoc/>
    public async ValueTask<Result> RequireAsync(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        CancellationToken cancellationToken)
    {
        CandidateGrant? decided = await DecideAsync(context, permission, resource, cancellationToken)
            .ConfigureAwait(false);

        return decided is { Deny: false }
            ? Result.Success()
            : Result.Failure(Error.From(
                ErrorCodes.Denied,
                "correlation",
                JsonSerializer.SerializeToElement(Guid.CreateVersion7(time.GetUtcNow()))));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<AccessExplanation>> ExplainAsync(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        CandidateGrant? decided = await DecideAsync(context, permission, resource, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new AccessExplanation(
            decided is { Deny: false } ? AccessOutcome.Allowed : AccessOutcome.Denied,
            permission,
            new ExplainedPrincipal(context.Acting, context.Effective),
            decided is null ? null : Explained(decided, resource)));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<Expression<Func<TResource, bool>>>> FilterAsync<TResource>(
        AccessContext context,
        Permission permission,
        ResourceType type,
        OrganizationId organization,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken)
    {
        PermissionRule rule = await RuleAsync(
            context,
            [permission],
            type,
            organization,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(rule.ToExpression(sources));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<SqlFilter>> FragmentAsync(
        AccessContext context,
        Permission permission,
        ResourceType type,
        OrganizationId organization,
        string rowAlias,
        string column,
        CancellationToken cancellationToken)
    {
        PermissionRule rule = await RuleAsync(
            context,
            [permission],
            type,
            organization,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(rule.ToFragment(rowAlias, column));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<Capability>>> CapabilitiesAsync(
        AccessContext context,
        ResourceType type,
        IReadOnlyList<ResourceId> resources,
        IReadOnlyList<Permission> permissions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(permissions);

        Declared(type);

        if (resources.Count == 0 || permissions.Count == 0)
        {
            return Nothing(resources);
        }

        RegisteredResource? first = await records
            .FindAsync(new ResourceReference(type, resources[0]), cancellationToken)
            .ConfigureAwait(false);

        if (first is null)
        {
            return Nothing(resources);
        }

        var rule = new PermissionRule(
            permissions,
            type,
            first.Organization,
            await subjects.OfAsync(context, cancellationToken).ConfigureAwait(false),
            time.GetUtcNow());

        IReadOnlyList<PageCapability> conferred = await evaluator
            .PageAsync(rule.ToPage(), resources, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<Capability>>(
            [.. resources.Select(resource => Held(resource, conferred))]);
    }

    // AUTHZ-GATE-005: a capability is permitted by grants; what it still requires is
    // what the per-row query does not evaluate. Nothing is outstanding on a permission
    // the grants do not confer at all, so only the conferred ones carry residuals.
    private static Capability Held(ResourceId resource, IReadOnlyList<PageCapability> conferred)
    {
        HashSet<Permission> can =
        [
            .. conferred
                .Where(row => !row.Denied
                    && string.Equals(row.Resource, resource.ToString(), StringComparison.Ordinal))
                .Select(row => Permission.Parse(row.Permission)),
        ];

        return new Capability(resource, can, NoResiduals);
    }

    private static Result<IReadOnlyList<Capability>> Nothing(IReadOnlyList<ResourceId> resources) =>
        Result.Success<IReadOnlyList<Capability>>(
        [
            .. resources.Select(resource =>
                new Capability(resource, new HashSet<Permission>(), NoResiduals)),
        ]);

    private static ExplainedGrant Explained(CandidateGrant decided, ResourceReference resource)
    {
        ResourceReference? above = decided.AncestorType is null || decided.AncestorId is null
            ? null
            : new ResourceReference(
                ResourceType.Parse(decided.AncestorType),
                ResourceId.Parse(decided.AncestorId));

        return new ExplainedGrant(
            new GrantId(decided.Grant),
            decided.Kind,
            decided.SubjectType,
            decided.SubjectId,
            decided.Role,
            decided.Deny,
            above == resource ? null : above);
    }

    private ResourceTypeDeclaration Declared(ResourceType type) =>
        model.Find(type)
        ?? throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture,
            $"The resource type '{type}' is not declared, so no policy governs it."));

    private async ValueTask<PermissionRule> RuleAsync(
        AccessContext context,
        IReadOnlyList<Permission> permissions,
        ResourceType type,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        Declared(type);

        return new PermissionRule(
            permissions,
            type,
            organization,
            await subjects.OfAsync(context, cancellationToken).ConfigureAwait(false),
            time.GetUtcNow());
    }

    private async ValueTask<CandidateGrant?> DecideAsync(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        CancellationToken cancellationToken)
    {
        Declared(resource.Type);

        // AUTHZ-SCOPE-001: the organization is the one owning the record, and a record
        // the library holds no row for belongs to none, so nothing confers anything on
        // it (AUTHZ-PRIN-003).
        RegisteredResource? registered = await records
            .FindAsync(resource, cancellationToken)
            .ConfigureAwait(false);

        if (registered is null)
        {
            return null;
        }

        var rule = new PermissionRule(
            [permission],
            resource.Type,
            registered.Organization,
            await subjects.OfAsync(context, cancellationToken).ConfigureAwait(false),
            time.GetUtcNow());

        IReadOnlyList<CandidateGrant> candidates = await evaluator
            .CandidatesAsync(rule.ToCandidates(), resource.Id, cancellationToken)
            .ConfigureAwait(false);

        return PermissionRule.Decides(candidates);
    }

    private static readonly IReadOnlyDictionary<Permission, IReadOnlySet<CapabilityResidual>>
        NoResiduals = new Dictionary<Permission, IReadOnlySet<CapabilityResidual>>();
}
