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
/// <param name="spikes">Where each recorded refusal is counted against its actor.</param>
/// <param name="subjects">Who the principal is, resolved once per operation.</param>
/// <param name="gates">What an action's step-up gate still asks of the session.</param>
/// <param name="exports">What an export operation asks beyond what the grants allow.</param>
/// <param name="derived">Which of the host's relationships confer what is being asked.</param>
/// <param name="lookup">Who can access a record, for the view that asks.</param>
/// <param name="consents">What the caller has consented to, for the purpose the action serves.</param>
/// <param name="administrative">Which organization a support role resolves a refusal in.</param>
/// <param name="time">The clock liveness is read against.</param>
/// <remarks>
/// Implements AUTHZ-SEAM-001, AUTHZ-PRIN-001, AUTHZ-PRIN-003, AUTHZ-GATE-002,
/// AUTHZ-GATE-004, AUTHZ-GATE-005, AUTHZ-SCOPE-001, AUTHZ-CONCEAL-004, AUTHZ-DERIVE-007,
/// PRIV-SENS-002, PRIV-SENS-002a, OPS-ALERT-006 and LIB-SEAM-001.
/// A check and a filter are the one rule rendered two ways, so neither can come to
/// answer what the other would refuse. Every path that cannot resolve what it needs
/// denies.
/// </remarks>
internal sealed class AccessGate(
    AuthorizationModel model,
    IResourceStore records,
    IAccessEvaluator evaluator,
    IAccessAudit audit,
    DenialSpikes spikes,
    SubjectSets subjects,
    StepUpGates gates,
    ExportOperations exports,
    Derivations derived,
    ReverseLookup lookup,
    IRecordedConsents consents,
    IAdministrativeOrganization administrative,
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
        if (FollowsFromTheHostsData(resource.Type))
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

        return await AllowedAsync(context, permission, resource, decided, cancellationToken)
            .ConfigureAwait(false);
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
            return await AllowedAsync(context, permission, resource, decided, cancellationToken)
                .ConfigureAwait(false);
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
            return await AllowedAsync(context, permission, resource, decided, cancellationToken)
                .ConfigureAwait(false);
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

        // An organization-wide check names no record, so it has no data subject; a
        // consent-based purpose is refused rather than admitted on nobody's consent.
        Result outstanding = await OutstandingAsync(context, dataSubject: null, permission, cancellationToken)
            .ConfigureAwait(false);

        return outstanding.Match(() => (Error?)null, error => error) is Error unmet
            ? Result.Failure(unmet)
            : await exports
                .AdmitAsync(context, permission, OrganizationWide, organization, record: null, cancellationToken)
                .ConfigureAwait(false);
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
        if (FollowsFromTheHostsData(resource.Type))
        {
            return Result.Failure<AccessExplanation>(
                Error.From(ErrorCodes.DerivationSourcesMissing));
        }

        Decision decided = await DecideAsync(context, permission, resource, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(Explanation(context, permission, resource, decided, derivedBy: null));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<AccessExplanation>> ExplainAsync<TResource>(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sources);

        // AUTHZ-GATE-004: concealment is read first, so a concealing type answers one
        // way whatever else is true of it (AUTHZ-CONCEAL-003).
        if (Declared(resource.Type).Concealment is ConcealmentBehaviour.Conceal)
        {
            return Result.Failure<AccessExplanation>(Error.From(ErrorCodes.Denied));
        }

        Decision decided = await DecideAsync(context, permission, resource, cancellationToken)
            .ConfigureAwait(false);

        // AUTHZ-DERIVE-002 AC1: a deny defeats a derived grant as it defeats a stored
        // one, so the host's relations are read only where nothing has decided yet.
        ExplainedGrant? derivedGrant = decided.Grant is null
            && decided.Organization is OrganizationId owner
            ? await DerivedAsync(context, permission, resource, owner, sources, cancellationToken)
                .ConfigureAwait(false)
            : null;

        return Result.Success(Explanation(context, permission, resource, decided, derivedGrant));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<ResourceAccess>> WhoCanAccessAsync(
        AccessContext context,
        ResourceReference resource,
        CancellationToken cancellationToken) =>
        await LookedUpAsync(context, resource, relationships: null, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<Result<ResourceAccess>> WhoCanAccessAsync<TResource>(
        AccessContext context,
        ResourceReference resource,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sources);

        return await LookedUpAsync(context, resource, sources.Relationships, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<AccessExplanation>> ResolveAsync(
        AccessContext context,
        AuditRecordId correlation,
        CancellationToken cancellationToken)
    {
        // AUTHZ-SCOPE-001: the trail is the deployment's, so the support role that
        // reads it is held in the administrative organization, and before bootstrap
        // has marked one nothing resolves.
        if (await administrative.FindAsync(cancellationToken).ConfigureAwait(false)
            is not OrganizationId organization)
        {
            return Result.Failure<AccessExplanation>(Error.From(ErrorCodes.Denied));
        }

        Result held = await RequireAsync(context, Permissions.AuditRead, organization, cancellationToken)
            .ConfigureAwait(false);

        if (!held.Match(() => true, _ => false))
        {
            return Result.Failure<AccessExplanation>(Error.From(ErrorCodes.Denied));
        }

        DeniedAccess? recorded = await audit.FindAsync(correlation, cancellationToken)
            .ConfigureAwait(false);

        return recorded is null
            ? Result.Failure<AccessExplanation>(Error.From(ErrorCodes.Denied))
            : Result.Success(Resolved(recorded));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<AccessExplanation>> ResolveOwnAsync(
        AccessContext context,
        AuditRecordId correlation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        DeniedAccess? recorded = context.Acting is null
            ? null
            : await audit.FindAsync(correlation, cancellationToken).ConfigureAwait(false);

        // AUTHZ-GATE-004 AC3: the refusal is of a type whose refusal already said the
        // operation is forbidden, and the caller's own under both identities; on a
        // concealing type, or one the model no longer declares, it answers as a
        // refusal would.
        return recorded is not null
            && Discloses(recorded.Type)
            && Resolved(recorded) is { } explained
            && explained.Principal == new ExplainedPrincipal(context.Acting, context.Effective)
                ? Result.Success(explained)
                : Result.Failure<AccessExplanation>(Error.From(ErrorCodes.Denied));
    }

    private static AccessExplanation Resolved(DeniedAccess recorded) => new(
        AccessOutcome.Denied,
        recorded.Permission,
        new ExplainedPrincipal(recorded.Acting, recorded.Effective),
        Grant: null);

    // AUTHZ-CONCEAL-005: a refusal tied to no record conceals nothing.
    private bool Discloses(ResourceType type) =>
        type == OrganizationWide
        || model.Find(type) is { Concealment: ConcealmentBehaviour.Disclose };

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

        // AUTH-STEP-001: a list exercises the permission as a check does, so the gate
        // bound to it is asked of the session before any row is admitted, and an export
        // is admitted under its limit before the rule is handed out (OPS-ALERT-006).
        if (await ExercisedAsync(context, permission, type, organization, cancellationToken).ConfigureAwait(false)
            is Error unmet)
        {
            return Result.Failure<Expression<Func<TResource, bool>>>(unmet);
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

        if (await ExercisedAsync(context, permission, type, organization, cancellationToken).ConfigureAwait(false)
            is Error unmet)
        {
            return Result.Failure<SqlFilter>(unmet);
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

        if (FollowsFromTheHostsData(type))
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
    // one further query over the host's rows answers what the derivations confer, so
    // the cost stands whatever the page's size and however many permissions are asked.
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

        // PRIV-SENS-002 AC1: what a consent is outstanding for is the data subject's
        // of each record, so the page is read here rather than only its first row.
        IReadOnlyList<RegisteredResource> registered = await records
            .FindManyAsync(type, resources, cancellationToken)
            .ConfigureAwait(false);

        if (registered.FirstOrDefault(row => row.Reference.Id == resources[0]) is not
            RegisteredResource first)
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

        // AUTHZ-GATE-005 AC1, PRIV-SENS-002 AC1: a consent belongs to the record's data
        // subject, so it is read once for each subject on the page rather than once per
        // row, and a record the library holds no row for has no subject to read.
        var whose = new Dictionary<ResourceId, SubjectId?>();

        foreach (RegisteredResource row in registered)
        {
            whose[row.Reference.Id] = row.Subject;
        }

        // A record the library holds no row for has no data subject, which reads no
        // consent and so asks the same of every row that is in that position.
        IReadOnlySet<Permission> nobody = await UnconsentedAsync(
            dataSubject: null, permissions, cancellationToken).ConfigureAwait(false);

        // AUTHZ-GATE-005 (D-160): a gate is the session's to meet whatever the record,
        // so it is judged once for the page rather than once per row.
        var unstepped = new HashSet<Permission>();

        foreach (Permission permission in permissions)
        {
            if (await UnsteppedAsync(context, permission, cancellationToken).ConfigureAwait(false)
                is not null)
            {
                unstepped.Add(permission);
            }
        }

        var unconsented = new Dictionary<SubjectId, IReadOnlySet<Permission>>();

        foreach (ResourceId resource in resources)
        {
            if (Whose(whose, resource) is SubjectId subject && !unconsented.ContainsKey(subject))
            {
                unconsented[subject] = await UnconsentedAsync(
                    subject, permissions, cancellationToken).ConfigureAwait(false);
            }
        }

        return Result.Success<IReadOnlyList<Capability>>(
        [
            .. resources.Select(resource => Held(
                resource,
                conferred,
                derivedRows,
                set.Restricted,
                unstepped,
                Whose(whose, resource) is SubjectId owner ? unconsented[owner] : nobody)),
        ]);
    }

    private static SubjectId? Whose(
        Dictionary<ResourceId, SubjectId?> held,
        ResourceId resource) =>
        held.TryGetValue(resource, out SubjectId? subject) ? subject : null;

    private async ValueTask<IReadOnlySet<Permission>> UnconsentedAsync(
        SubjectId? dataSubject,
        IReadOnlyList<Permission> permissions,
        CancellationToken cancellationToken)
    {
        var outstanding = new HashSet<Permission>();

        foreach (Permission permission in permissions)
        {
            if (await UnconsentedAsync(dataSubject, permission, cancellationToken)
                .ConfigureAwait(false) is not null)
            {
                _ = outstanding.Add(permission);
            }
        }

        return outstanding;
    }

    // AUTHZ-GATE-005 AC1, D-162: the page is one query over the host's rows, carrying
    // one clause per derivation reaching the type. What each derivation's role allows
    // is model data and is mapped here, so no permission costs a query of its own.
    private async ValueTask<IReadOnlyDictionary<Permission, IReadOnlySet<string>>> DerivedAsync<TResource>(
        AccessContext context,
        ResourceType type,
        OrganizationId organization,
        IReadOnlyList<Permission> permissions,
        IReadOnlyList<ResourceId> resources,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ConferredDerivation> conferring = await derived
            .ConferringAsync(type, organization, cancellationToken)
            .ConfigureAwait(false);

        if (conferring.Count == 0)
        {
            return new Dictionary<Permission, IReadOnlySet<string>>();
        }

        var rule = new PermissionRule(
            permissions,
            type,
            organization,
            await subjects.OfAsync(context, cancellationToken).ConfigureAwait(false),
            time.GetUtcNow(),
            [.. conferring.Select(one => one.Relationship)]);

        var admitted = new Dictionary<Permission, HashSet<string>>();

        foreach (AdmittedRecord record in await RowsAsync(
            rule.ToAdmittedRecords(sources, resources), cancellationToken).ConfigureAwait(false))
        {
            foreach (ConferredDerivation one in conferring.Where(one =>
                string.Equals(one.Relationship.Name, record.Relationship, StringComparison.Ordinal)))
            {
                foreach (Permission permission in permissions.Where(one.Confers.Contains))
                {
                    if (!admitted.TryGetValue(permission, out HashSet<string>? records))
                    {
                        records = new HashSet<string>(StringComparer.Ordinal);
                        admitted.Add(permission, records);
                    }

                    records.Add(record.Resource);
                }
            }
        }

        return admitted.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlySet<string>)entry.Value);
    }

    // LIB-HOST-002, D-161: the query was composed from the rows the host supplied and
    // carries the host's own provider, so reading it issues nothing of the library's
    // own against a host table and takes none of its connections.
    private static async ValueTask<IReadOnlyList<AdmittedRecord>> RowsAsync(
        IQueryable<AdmittedRecord>? admitted,
        CancellationToken cancellationToken)
    {
        if (admitted is null)
        {
            return [];
        }

        if (admitted is not IAsyncEnumerable<AdmittedRecord> rows)
        {
            throw new InvalidOperationException(
                "The rows the host supplied are not read asynchronously, which the contract tables mapped into the host's own context are.");
        }

        var records = new List<AdmittedRecord>();

        await foreach (AdmittedRecord record in rows
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            records.Add(record);
        }

        return records;
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
    // What the conferred role allows is not read here: a role is edited where it
    // stands, so a call admitted today because the role allowed nothing asked for
    // would fault on the next edit of the role instead of at the call site.
    private bool FollowsFromTheHostsData(ResourceType type)
    {
        Declared(type);

        return derived.Reaches(type);
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
        bool restricted,
        HashSet<Permission> unstepped,
        IReadOnlySet<Permission> unconsented)
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

            if (unstepped.Contains(permission))
            {
                outstanding.Add(CapabilityResidual.StepUp);
            }

            if (unconsented.Contains(permission))
            {
                outstanding.Add(CapabilityResidual.Consent);
            }

            if (outstanding.Count > 0)
            {
                requires.Add(permission, outstanding.ToFrozenSet());
            }
        }

        return new Capability(resource, can, requires.Count == 0 ? NoResiduals : requires);
    }

    // AUTHZ-GATE-005: what the grants confer is still subject to the session's gates
    // and to what the caller consented to, so an action the grants allow and one of
    // those does not is refused with what it is waiting for rather than with a denial
    // (AUTH-STEP-001, PRIV-SENS-002).
    private async ValueTask<Result> OutstandingAsync(
        AccessContext context,
        SubjectId? dataSubject,
        Permission permission,
        CancellationToken cancellationToken)
    {
        if (await UnsteppedAsync(context, permission, cancellationToken).ConfigureAwait(false)
            is Error unmet)
        {
            return Result.Failure(unmet);
        }

        return await UnconsentedAsync(dataSubject, permission, cancellationToken)
            .ConfigureAwait(false) is ErrorCode missing
            ? Result.Failure(Error.From(missing))
            : Result.Success();
    }

    // OPS-ALERT-006: an export the grants, its gate and its consent allow is admitted
    // under the hourly limit and recorded last, so that nothing refused is counted.
    private async ValueTask<Result> AllowedAsync(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        Decision decided,
        CancellationToken cancellationToken)
    {
        Result outstanding = await OutstandingAsync(context, decided.Subject, permission, cancellationToken)
            .ConfigureAwait(false);

        return outstanding.Match(() => (Error?)null, error => error) is Error unmet
            ? Result.Failure(unmet)
            : await exports
                .AdmitAsync(context, permission, resource.Type, decided.Organization, resource.Id, cancellationToken)
                .ConfigureAwait(false);
    }

    // AUTH-STEP-001, OPS-ALERT-006: what a list or a fragment asks before the rule is
    // handed out, which is the gate the action is bound to and, for an export, a place
    // under the limit. The host reads the rows the rule admits, so the export is
    // counted when the rule is handed out, whatever the query then returns.
    private async ValueTask<Error?> ExercisedAsync(
        AccessContext context,
        Permission permission,
        ResourceType type,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        if (await UnsteppedAsync(context, permission, cancellationToken).ConfigureAwait(false)
            is Error unmet)
        {
            return unmet;
        }

        Result admitted = await exports
            .AdmitAsync(context, permission, type, organization, record: null, cancellationToken)
            .ConfigureAwait(false);

        return admitted.Match(() => (Error?)null, error => error);
    }

    // AUTH-STEP-001, OPS-ALERT-006: the gate an action asks for is the one the host bound
    // it to, or, where it bound none, the one an export asks for of its own.
    private async ValueTask<Error?> UnsteppedAsync(
        AccessContext context,
        Permission permission,
        CancellationToken cancellationToken) =>
        await gates.OutstandingAsync(
            context,
            model.GateOf(permission) ?? await exports.GateOfAsync(permission, cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

    // PRIV-SENS-002 AC1, PRIV-SENS-002a: the consent read is the record's data
    // subject's, whoever the caller is, so staff and background work are gated exactly
    // as the subject's own request is. The action is refused for the one purpose it is
    // done for, and for no other purpose the record carries. A purpose resting on
    // another basis is nobody's to consent to, so it asks nothing here; the written
    // path admits no ordinary record.
    private async ValueTask<ErrorCode?> UnconsentedAsync(
        SubjectId? dataSubject,
        Permission permission,
        CancellationToken cancellationToken)
    {
        if (model.PurposeOf(permission) is not string purpose
            || model.Processing.Find(purpose) is not { Consent: ConsentKind required })
        {
            return null;
        }

        // A check with no record in hand has no data subject, and an action admitted
        // on nobody's consent is the one PRIV-SENS-002 AC1 forbids.
        if (dataSubject is not SubjectId subject)
        {
            return ErrorCodes.ConsentRequired;
        }

        ConsentRecord? held = await consents
            .OfAsync(subject, purpose, cancellationToken)
            .ConfigureAwait(false);

        return held switch
        {
            null or { WithdrawnAt: not null } => ErrorCodes.ConsentRequired,
            { SupersededAt: not null } => ErrorCodes.ConsentSuperseded,
            { Kind: ConsentKind.Ordinary } when required is ConsentKind.Written =>
                ErrorCodes.ConsentWrittenRequired,
            _ => null,
        };
    }

    private static Result<IReadOnlyList<Capability>> Nothing(IReadOnlyList<ResourceId> resources) =>
        Result.Success<IReadOnlyList<Capability>>(
        [
            .. resources.Select(resource =>
                new Capability(resource, new HashSet<Permission>(), NoResiduals)),
        ]);

    // AUTHZ-SCOPE-001, AUTHZ-CONCEAL-005, AUTHZ-DERIVE-007: the view is the grant:read
    // permission's in the organization the record sits in, read from the record, and
    // nothing is concealed from a caller without it. Without the host's rows, a type a
    // derivation reaches is refused as every other path refuses it, since the stored
    // grants alone are not who can access it (D-161, D-162).
    private async ValueTask<Result<ResourceAccess>> LookedUpAsync(
        AccessContext context,
        ResourceReference resource,
        IReadOnlyDictionary<string, RelationshipRows>? relationships,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        bool organizationWide = resource.Type == OrganizationWide;

        if (!organizationWide && model.Find(resource.Type) is null)
        {
            return Result.Failure<ResourceAccess>(Malformed("resourceType"));
        }

        if (await ScopeOfAsync(resource, organizationWide, cancellationToken).ConfigureAwait(false)
            is not OrganizationId organization)
        {
            return Result.Failure<ResourceAccess>(Malformed("resourceId"));
        }

        Result held = await RequireAsync(context, Permissions.GrantRead, organization, cancellationToken)
            .ConfigureAwait(false);

        if (held.Match(() => (Error?)null, error => error) is Error refused)
        {
            return Result.Failure<ResourceAccess>(refused);
        }

        if (relationships is null && !organizationWide && derived.Reaches(resource.Type))
        {
            return Result.Failure<ResourceAccess>(Error.From(ErrorCodes.DerivationSourcesMissing));
        }

        return Result.Success(await lookup
            .LookedUpAsync(resource, organization, relationships, cancellationToken)
            .ConfigureAwait(false));
    }

    private async ValueTask<OrganizationId?> ScopeOfAsync(
        ResourceReference resource,
        bool organizationWide,
        CancellationToken cancellationToken) =>
        organizationWide
            ? Guid.TryParse(resource.Id.ToString(), out Guid organization) ? new OrganizationId(organization) : null
            : (await records.FindAsync(resource, cancellationToken).ConfigureAwait(false))?.Organization;

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    // AUTHZ-GATE-004: one shape answers both paths, the derived grant being the one a
    // fact in the host's data produced rather than one somebody wrote.
    private static AccessExplanation Explanation(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        Decision decided,
        ExplainedGrant? derivedBy)
    {
        ExplainedGrant? grant = decided.Grant is null
            ? derivedBy
            : Explained(decided.Grant, resource);

        return new AccessExplanation(
            grant is { Deny: false } ? AccessOutcome.Allowed : AccessOutcome.Denied,
            permission,
            new ExplainedPrincipal(context.Acting, context.Effective),
            grant);
    }

    // AUTHZ-GATE-004, D-162: the grant a fact produced, as an explanation names it. It
    // holds no identifier, because no row holds it; the role is the one the derivation
    // confers, and the container is the one the relationship is declared on.
    private async ValueTask<ExplainedGrant?> DerivedAsync<TResource>(
        AccessContext context,
        Permission permission,
        ResourceReference resource,
        OrganizationId organization,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken)
    {
        if (context.Effective is not SubjectId subject)
        {
            return null;
        }

        IReadOnlyList<ConferredDerivation> conferring =
        [
            .. (await derived.ConferringAsync(resource.Type, organization, cancellationToken).ConfigureAwait(false))
                .Where(one => one.Confers.Contains(permission)),
        ];

        if (conferring.Count == 0)
        {
            return null;
        }

        var rule = new PermissionRule(
            [permission],
            resource.Type,
            organization,
            await subjects.OfAsync(context, cancellationToken).ConfigureAwait(false),
            time.GetUtcNow(),
            [.. conferring.Select(one => one.Relationship)]);

        IReadOnlyList<AdmittedRecord> admitted = await RowsAsync(
            rule.ToAdmittedRecords(sources, [resource.Id]), cancellationToken).ConfigureAwait(false);

        if (admitted.Count == 0)
        {
            return null;
        }

        AdmittedRecord first = admitted[0];

        ConferredDerivation deciding = conferring.First(one =>
            string.Equals(one.Relationship.Name, first.Relationship, StringComparison.Ordinal));

        var above = new ResourceReference(
            deciding.Relationship.On,
            ResourceId.Parse(first.Ancestor));

        return new ExplainedGrant(
            Id: null,
            GrantKind.Derived,
            SubjectType.User,
            subject.Value,
            deciding.Role,
            Deny: false,
            above == resource ? null : above);
    }

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
    // identifier it hands back is the row the refusal was recorded as. The same path
    // counts it towards its actor's denial spike (AUTHZ-GATE-004, OPS-ALERT-001). A request made
    // under no account is refused with an identifier like any other; the row names
    // nobody, and that absence is the recorded fact.
    private async ValueTask<Result> RefusedAsync(
        AccessContext context,
        Permission permission,
        ResourceType type,
        OrganizationId? organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlation = AuditRecordId.New(time);

        var denial = new DeniedAccess(
            correlation,
            context.Acting,
            context.Effective,
            organization,
            permission,
            type,
            time.GetUtcNow());

        await audit.RecordAsync(denial, cancellationToken).ConfigureAwait(false);
        await spikes.WatchAsync(denial, cancellationToken).ConfigureAwait(false);

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
            await derived.ReachingAsync(type, organization, permissions, cancellationToken).ConfigureAwait(false));
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
            return new Decision(Grant: null, Organization: null, Subject: null);
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

        return new Decision(
            PermissionRule.Decides(candidates),
            registered.Organization,
            registered.Subject);
    }

    // What an evaluation decided, and the organization it was scoped to, which is what
    // a refusal is recorded against.
    private sealed record Decision(
        CandidateGrant? Grant,
        OrganizationId? Organization,
        SubjectId? Subject);
}
