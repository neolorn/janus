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
/// <param name="audit">Where a refusal is recorded and read back.</param>
/// <param name="subjects">Who the principal is, resolved once per operation.</param>
/// <param name="time">The clock liveness is read against.</param>
/// <remarks>
/// Implements AUTHZ-SEAM-001, AUTHZ-PRIN-001, AUTHZ-PRIN-003, AUTHZ-GATE-002,
/// AUTHZ-GATE-004, AUTHZ-GATE-005, AUTHZ-SCOPE-001, AUTHZ-CONCEAL-004 and LIB-SEAM-001.
/// A check and a filter are the one rule rendered two ways, so neither can come to
/// answer what the other would refuse. Every path that cannot resolve what it needs
/// denies.
/// </remarks>
internal sealed class AccessGate(
    AuthorizationModel model,
    IResourceStore records,
    IAccessEvaluator evaluator,
    IAccessAudit audit,
    SubjectSets subjects,
    TimeProvider time) : IAccessGate
{
    private static readonly ResourceType OrganizationWide = ResourceType.Parse("organization");

    private static readonly IReadOnlyDictionary<Permission, IReadOnlySet<CapabilityResidual>>
        NoResiduals = new Dictionary<Permission, IReadOnlySet<CapabilityResidual>>();

    /// <inheritdoc/>
    public async ValueTask<Result> RequireAsync(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        CancellationToken cancellationToken)
    {
        Decision decided = await DecideAsync(context, permission, resource, cancellationToken)
            .ConfigureAwait(false);

        return decided.Grant is { Deny: false }
            ? Result.Success()
            : await RefusedAsync(
                context,
                permission,
                resource.Type,
                decided.Organization,
                cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RequireAsync(
        AccessContext context,
        Permission permission,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        CandidateGrant? decided = await HoldsAsync(context, permission, organization, cancellationToken)
            .ConfigureAwait(false);

        // AUTHZ-CONCEAL-005: nothing is concealed here, so what the caller answers is
        // that the operation is forbidden. The refusal is recorded the same way.
        return decided is { Deny: false }
            ? Result.Success()
            : await RefusedAsync(
                context,
                permission,
                OrganizationWide,
                organization,
                cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<AccessExplanation>> ExplainAsync(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // AUTHZ-GATE-004: on a type whose denial answers as a record that does not
        // exist, an explanation saying no grant matched says that it does.
        if (Declared(resource.Type).Concealment is ConcealmentBehaviour.Conceal)
        {
            return Result.Failure<AccessExplanation>(Error.From(ErrorCodes.Denied));
        }

        Decision decided = await DecideAsync(context, permission, resource, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new AccessExplanation(
            decided.Grant is { Deny: false } ? AccessOutcome.Allowed : AccessOutcome.Denied,
            permission,
            new ExplainedPrincipal(context.Acting, context.Effective),
            decided.Grant is null ? null : Explained(decided.Grant, resource)));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<AccessExplanation>> ResolveAsync(
        AccessContext context,
        OrganizationId organization,
        AuditRecordId correlation,
        CancellationToken cancellationToken)
    {
        Result held = await RequireAsync(context, Permissions.AuditRead, organization, cancellationToken)
            .ConfigureAwait(false);

        if (!held.Match(() => true, _ => false))
        {
            return Result.Failure<AccessExplanation>(Error.From(ErrorCodes.Denied));
        }

        DeniedAccess? recorded = await audit.FindAsync(correlation, cancellationToken)
            .ConfigureAwait(false);

        // A refusal recorded against no organization is one about a record the library
        // holds no row for, which no organization owns; the identifier, which only its
        // holder has, is the whole of what reaches it.
        if (recorded is null || recorded.Organization is OrganizationId owner && owner != organization)
        {
            return Result.Failure<AccessExplanation>(Error.From(ErrorCodes.Denied));
        }

        return Result.Success(new AccessExplanation(
            AccessOutcome.Denied,
            recorded.Permission,
            new ExplainedPrincipal(recorded.Acting, recorded.Effective),
            Grant: null));
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

    // AUTHZ-CONCEAL-004, CONV-LOG-005: one path answers every refusal, and the
    // identifier it hands back is the row the refusal was recorded as. A context naming
    // no account names nobody the trail can record the refusal against, both of its
    // identity fields being accounts (IDN-AUD-001).
    private async ValueTask<Result> RefusedAsync(
        AccessContext context,
        Permission permission,
        ResourceType type,
        OrganizationId? organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting || context.Effective is not SubjectId effective)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        var correlation = AuditRecordId.New(time);

        await audit
            .RecordAsync(
                new DeniedAccess(
                    correlation,
                    acting,
                    effective,
                    organization,
                    permission,
                    type,
                    time.GetUtcNow()),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Failure(Error.From(
            ErrorCodes.Denied,
            "correlation",
            JsonSerializer.SerializeToElement(correlation.Value)));
    }

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

    private async ValueTask<CandidateGrant?> HoldsAsync(
        AccessContext context,
        Permission permission,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        var rule = new PermissionRule(
            [permission],
            organization,
            await subjects.OfAsync(context, cancellationToken).ConfigureAwait(false),
            time.GetUtcNow());

        IReadOnlyList<CandidateGrant> candidates = await evaluator
            .OrganizationCandidatesAsync(rule.ToOrganizationCandidates(), cancellationToken)
            .ConfigureAwait(false);

        return PermissionRule.Decides(candidates);
    }

    private async ValueTask<Decision> DecideAsync(
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
            return new Decision(Grant: null, Organization: null);
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

        return new Decision(PermissionRule.Decides(candidates), registered.Organization);
    }

    // What an evaluation decided, and the organization it was scoped to, which is what
    // a refusal is recorded against.
    private sealed record Decision(CandidateGrant? Grant, OrganizationId? Organization);
}
