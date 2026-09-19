using System;
using System.Collections.Frozen;
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
/// <param name="gates">What an action's step-up gate still asks of the session.</param>
/// <param name="derived">Which of the host's relationships confer what is being asked.</param>
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
    StepUpGates gates,
    Derivations derived,
    TimeProvider time) : IAccessGate
{
    private static readonly ResourceType OrganizationWide = ResourceType.Parse("organization");

    private static readonly IReadOnlyDictionary<Permission, IReadOnlySet<CapabilityResidual>>
        NoResiduals = new Dictionary<Permission, IReadOnlySet<CapabilityResidual>>();

    private static readonly IReadOnlySet<string> NoRecords = new HashSet<string>(StringComparer.Ordinal);

    // A restriction admits no modifying action, so what the host composes into its
    // query is a fragment that matches nothing rather than a rule that cannot match.
    private static readonly SqlFilter MatchesNothing =
        new("false", new Dictionary<string, object>(StringComparer.Ordinal));

    /// <inheritdoc/>
    public async ValueTask<Result> RequireAsync(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        CancellationToken cancellationToken)
    {
        if (await FollowsFromTheHostsDataAsync(resource.Type, [permission], cancellationToken)
            .ConfigureAwait(false))
        {
            return Result.Failure(Error.From(ErrorCodes.DerivationSourcesMissing));
        }

        if (await RestrictedAsync(context, permission, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(Error.From(ErrorCodes.Restricted));
        }

        Decision decided = await DecideAsync(context, permission, resource, cancellationToken)
            .ConfigureAwait(false);

        if (decided.Grant is not { Deny: false })
        {
            return await RefusedAsync(
                context,
                permission,
                resource.Type,
                decided.Organization,
                cancellationToken).ConfigureAwait(false);
        }

        return Outstanding(permission);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RequireAsync<TResource>(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sources);

        if (await RestrictedAsync(context, permission, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(Error.From(ErrorCodes.Restricted));
        }

        Decision decided = await DecideAsync(context, permission, resource, cancellationToken)
            .ConfigureAwait(false);

        if (decided.Grant is { Deny: false })
        {
            return Outstanding(permission);
        }

        // AUTHZ-DERIVE-002 AC1: a deny defeats a derived grant as it defeats a stored
        // one, so the host's relations are read only where nothing has decided yet.
        if (decided.Grant is null
            && decided.Organization is OrganizationId owner
            && (await AdmittedAsync(
                context,
                [permission],
                resource.Type,
                owner,
                sources,
                [resource.Id],
                cancellationToken).ConfigureAwait(false)).Contains(resource.Id.ToString()))
        {
            return Outstanding(permission);
        }

        return await RefusedAsync(
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
        if (await RestrictedAsync(context, permission, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(Error.From(ErrorCodes.Restricted));
        }

        CandidateGrant? decided = await HoldsAsync(context, permission, organization, cancellationToken)
            .ConfigureAwait(false);

        // AUTHZ-CONCEAL-005: nothing is concealed here, so what the caller answers is
        // that the operation is forbidden. The refusal is recorded the same way.
        if (decided is not { Deny: false })
        {
            return await RefusedAsync(
                context,
                permission,
                OrganizationWide,
                organization,
                cancellationToken).ConfigureAwait(false);
        }

        return Outstanding(permission);
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
        // exist, an explanation saying no grant matched says that it does. This is asked
        // before anything else, so that a concealing type answers one way whatever else
        // is true of it (AUTHZ-CONCEAL-003).
        if (Declared(resource.Type).Concealment is ConcealmentBehaviour.Conceal)
        {
            return Result.Failure<AccessExplanation>(Error.From(ErrorCodes.Denied));
        }

        // AUTHZ-GATE-004, D-161: an explanation read from the stored grants alone would
        // say that no grant matched for a record the filter admits, so a type whose
        // access follows in part from the host's own data is not explained here.
        if (await FollowsFromTheHostsDataAsync(resource.Type, [permission], cancellationToken)
            .ConfigureAwait(false))
        {
            return Result.Failure<AccessExplanation>(
                Error.From(ErrorCodes.DerivationSourcesMissing));
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
        if (await RestrictedAsync(context, permission, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success<Expression<Func<TResource, bool>>>(_ => false);
        }

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
        if (await RestrictedAsync(context, permission, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success(MatchesNothing);
        }

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

        if (await FollowsFromTheHostsDataAsync(type, permissions, cancellationToken)
            .ConfigureAwait(false))
        {
            return Result.Failure<IReadOnlyList<Capability>>(
                Error.From(ErrorCodes.DerivationSourcesMissing));
        }

        return await PageAsync(context, type, resources, permissions, NoneAdmitted, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<Capability>>> CapabilitiesAsync<TResource>(
        AccessContext context,
        ResourceType type,
        IReadOnlyList<ResourceId> resources,
        IReadOnlyList<Permission> permissions,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(sources);

        return await PageAsync(
            context,
            type,
            resources,
            permissions,
            (organization, token) => DerivedAsync(context, type, organization, permissions, resources, sources, token),
            cancellationToken).ConfigureAwait(false);
    }

    // AUTHZ-GATE-005 AC1: one query answers the whole page for the stored grants, and
    // one further query per permission a derivation confers answers the rest, so the
    // cost stands whatever the page's size.
    private async ValueTask<Result<IReadOnlyList<Capability>>> PageAsync(
        AccessContext context,
        ResourceType type,
        IReadOnlyList<ResourceId> resources,
        IReadOnlyList<Permission> permissions,
        Func<OrganizationId, CancellationToken,
            ValueTask<IReadOnlyDictionary<Permission, IReadOnlySet<string>>>> derivedBy,
        CancellationToken cancellationToken)
    {
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

        SubjectSet set = await subjects.OfAsync(context, cancellationToken).ConfigureAwait(false);

        var rule = new PermissionRule(permissions, type, first.Organization, set, time.GetUtcNow());

        IReadOnlyList<PageCapability> conferred = await evaluator
            .PageAsync(rule.ToPage(), resources, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyDictionary<Permission, IReadOnlySet<string>> derivedRows =
            await derivedBy(first.Organization, cancellationToken).ConfigureAwait(false);

        return Result.Success<IReadOnlyList<Capability>>(
        [
            .. resources.Select(resource =>
                Held(resource, conferred, derivedRows, set.Restricted)),
        ]);
    }

    // A derivation confers a role, and what that role allows is not what another
    // allows, so each permission is asked for on its own.
    private async ValueTask<IReadOnlyDictionary<Permission, IReadOnlySet<string>>> DerivedAsync<TResource>(
        AccessContext context,
        ResourceType type,
        OrganizationId organization,
        IReadOnlyList<Permission> permissions,
        IReadOnlyList<ResourceId> resources,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken)
    {
        var admitted = new Dictionary<Permission, IReadOnlySet<string>>();

        foreach (Permission permission in permissions)
        {
            IReadOnlySet<string> records = await AdmittedAsync(
                context,
                [permission],
                type,
                organization,
                sources,
                resources,
                cancellationToken).ConfigureAwait(false);

            if (records.Count > 0)
            {
                admitted.Add(permission, records);
            }
        }

        return admitted;
    }

    // The records of the page a derivation admits, read in one query over the rows the
    // host supplied (AUTHZ-DERIVE-001, LIB-HOST-002).
    private async ValueTask<IReadOnlySet<string>> AdmittedAsync<TResource>(
        AccessContext context,
        IReadOnlyList<Permission> permissions,
        ResourceType type,
        OrganizationId organization,
        FilterSources<TResource> sources,
        IReadOnlyList<ResourceId> resources,
        CancellationToken cancellationToken)
    {
        PermissionRule rule = await RuleAsync(
            context,
            permissions,
            type,
            organization,
            cancellationToken).ConfigureAwait(false);

        if (rule.ToAdmitted(sources, resources) is not IQueryable<string> admitted)
        {
            return NoRecords;
        }

        // LIB-HOST-002, D-161: the query was composed from the rows the host supplied
        // and carries the host's own provider, so reading it issues nothing of the
        // library's own against a host table and takes none of its connections.
        if (admitted is not IAsyncEnumerable<string> rows)
        {
            throw new InvalidOperationException(
                "The rows the host supplied are not read asynchronously, which the contract tables mapped into the host's own context are.");
        }

        var records = new HashSet<string>(StringComparer.Ordinal);

        await foreach (string record in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            records.Add(record);
        }

        return records;
    }

    // AUTHZ-DERIVE-001, D-161: what a path answers without the host's rows is what the
    // stored grants alone say, which on a type a derivation reaches is not the answer.
    private async ValueTask<bool> FollowsFromTheHostsDataAsync(
        ResourceType type,
        IReadOnlyList<Permission> permissions,
        CancellationToken cancellationToken)
    {
        Declared(type);

        return (await derived.ReachingAsync(type, permissions, cancellationToken)
            .ConfigureAwait(false)).Count > 0;
    }

    private static ValueTask<IReadOnlyDictionary<Permission, IReadOnlySet<string>>> NoneAdmitted(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyDictionary<Permission, IReadOnlySet<string>>>(
            new Dictionary<Permission, IReadOnlySet<string>>());

    // AUTHZ-GATE-005: a capability is permitted by grants; what it still requires is
    // what the per-row query does not evaluate. Nothing is outstanding on a permission
    // the grants do not confer at all, so only the conferred ones carry residuals.
    private Capability Held(
        ResourceId resource,
        IReadOnlyList<PageCapability> conferred,
        IReadOnlyDictionary<Permission, IReadOnlySet<string>> derivedRows,
        bool restricted)
    {
        string named = resource.ToString();

        IReadOnlyList<PageCapability> here =
        [
            .. conferred.Where(row =>
                string.Equals(row.Resource, named, StringComparison.Ordinal)),
        ];

        HashSet<Permission> can =
        [
            .. here.Where(row => !row.Denied).Select(row => Permission.Parse(row.Permission)),
        ];

        // AUTHZ-DERIVE-002 AC1: a deny on the record defeats a derived grant, and the
        // page query already carries one row per permission a deny reached.
        foreach ((Permission permission, IReadOnlySet<string> records) in derivedRows)
        {
            if (records.Contains(named)
                && !here.Any(row => row.Denied
                    && string.Equals(row.Permission, permission.ToString(), StringComparison.Ordinal)))
            {
                can.Add(permission);
            }
        }

        var requires = new Dictionary<Permission, IReadOnlySet<CapabilityResidual>>();

        foreach (Permission permission in can)
        {
            var outstanding = new HashSet<CapabilityResidual>();

            if (restricted && !model.IsReading(permission))
            {
                outstanding.Add(CapabilityResidual.Restricted);
            }

            if (gates.OutstandingOn(permission) is not null)
            {
                outstanding.Add(CapabilityResidual.StepUp);
            }

            if (outstanding.Count > 0)
            {
                requires.Add(permission, outstanding.ToFrozenSet());
            }
        }

        return new Capability(resource, can, requires.Count == 0 ? NoResiduals : requires);
    }

    // AUTHZ-GATE-005: what the grants confer is still subject to the session's gates,
    // so an action the grants allow and the gate does not is refused with what it is
    // waiting for rather than with a denial (AUTH-STEP-001).
    private Result Outstanding(Permission permission) =>
        gates.OutstandingOn(permission) is ErrorCode code
            ? Result.Failure(Error.From(code))
            : Result.Success();

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

    // AUTHZ-GATE-006: a restriction leaves the account's reading actions and refuses
    // every modifying one, wherever the gate is evaluated (D-160).
    private async ValueTask<bool> RestrictedAsync(
        AccessContext context,
        Permission permission,
        CancellationToken cancellationToken) =>
        !model.IsReading(permission)
        && (await subjects.OfAsync(context, cancellationToken).ConfigureAwait(false)).Restricted;

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
            time.GetUtcNow(),
            await derived.ReachingAsync(type, permissions, cancellationToken).ConfigureAwait(false));
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
