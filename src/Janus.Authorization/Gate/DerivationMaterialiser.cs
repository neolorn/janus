using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Authorization.Model;
using Janus.Authorization.Resources;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// A materialised derivation brought back into step with the host's own relation.
/// </summary>
/// <param name="model">The host's declaration, read for what a relationship confers.</param>
/// <param name="records">Where the records a container reaches are read.</param>
/// <param name="grants">Where the rows the derivation was precomputed into are read and written.</param>
/// <param name="declared">Where a relationship's rows are read through the source the host declared.</param>
/// <param name="audit">Where each grant a refresh writes or takes back is recorded.</param>
/// <param name="work">The caller's transaction, which the refresh joins.</param>
/// <param name="sessions">Whether a request is served on the calling flow, which a system principal's refresh is refused in.</param>
/// <param name="time">The clock liveness is read against.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-005, AUTHZ-DERIVE-002 and AUTHZ-GRANT-003 (D-161, D-166). The
/// grant is written on each record of the type the derivation is declared on that the
/// relationship's record reaches, so the rows confer exactly what evaluating the
/// derivation would confer and inheritance carries them no further than it carries the
/// derivation. The rows of the relationship are the host's: read through what the host
/// supplied where the host calls the refresh, and through the source the host declared
/// where the drift check does. A grant the host's refresh writes records the caller's acting
/// account as its granter, or the nil subject for a system principal, with the reason
/// <c>derivation:&lt;relationship&gt;</c>; one the drift check writes records the nil subject
/// and the reason <c>AUTHZ-DERIVE-005</c>. Each grant a refresh writes or takes back is
/// recorded in the trail in the refresh's transaction, naming who called it (D-187, D-207).
/// The refresh meets no gate, so a system principal's call the chapters do not admit is a
/// fault of the calling code, raised before anything is written (D-208, D-209).
/// </remarks>
internal sealed class DerivationMaterialiser(
    AuthorizationModel model,
    IResourceStore records,
    IGrantStore grants,
    IRelationshipSources declared,
    IAccessAudit audit,
    IUnitOfWork work,
    ISessionGates sessions,
    TimeProvider time) : IDerivationMaterialiser
{
    // AUTHZ-GRANT-003, AUTHZ-DERIVE-005: what a grant the drift check writes or takes
    // back records as its reason, as bootstrap's grants record OPS-BOOT-001.
    private const string DriftReason = "AUTHZ-DERIVE-005";

    /// <inheritdoc/>
    public async ValueTask<Result<DerivationRefresh>> RefreshAsync<TResource>(
        AccessContext context,
        string derivation,
        ResourceId resource,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(derivation);
        ArgumentNullException.ThrowIfNull(sources);

        Refreshing refreshing = Asked(context, derivation);

        if (!sources.Relationships.TryGetValue(derivation, out RelationshipRows? rows))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The rows of the relationship '{derivation}' were not supplied."),
                nameof(sources));
        }

        // The rows are the host's and carry the host's own provider, so reading them
        // issues nothing of the library's own against a host table (LIB-HOST-002, D-161).
        return await RefreshedAsync(
                refreshing,
                resource,
                rows.HeldBy(Substitution.HeldOn(refreshing.Relationship, resource), refreshing.Relationship.Holder),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The same refresh over the rows the source the host declared answers, which is how
    /// the drift check brings one record back into step.
    /// </summary>
    /// <param name="context">Who is asking, recorded on every grant the refresh writes.</param>
    /// <param name="derivation">The relationship the materialised derivations follow from.</param>
    /// <param name="resource">The record the relationship's rows are about.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What the refresh changed.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    /// <exception cref="ArgumentException">
    /// The relationship is undeclared, no materialised derivation follows from it, or no
    /// source is declared for it.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The context is a system principal's the refresh does not admit.
    /// </exception>
    public async ValueTask<Result<DerivationRefresh>> RefreshAsync(
        AccessContext context,
        string derivation,
        ResourceId resource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(derivation);

        Refreshing refreshing = Asked(context, derivation);

        if (!declared.Declares(derivation))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"No source is declared for the relationship '{derivation}'."),
                nameof(derivation));
        }

        return await RefreshedAsync(
                refreshing,
                resource,
                declared.HeldOn(refreshing.Relationship, resource),
                cancellationToken)
            .ConfigureAwait(false);
    }

    // The reason is the library's own and is never blank, so a refusal here is a fault
    // in this file rather than something a caller can be told about.
    private static void Revoked(Grant grant, SubjectId by, DateTimeOffset at, string reason) =>
        grant.Revoke(by, at, reason).Match(
            () => true,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private static async ValueTask<IReadOnlySet<SubjectId>> ReadAsync(
        IAsyncEnumerable<SubjectId> holders,
        CancellationToken cancellationToken)
    {
        var subjects = new HashSet<SubjectId>();

        await foreach (SubjectId holder in holders.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            subjects.Add(holder);
        }

        return subjects;
    }

    // AUTHZ-GRANT-003, AUTHZ-DERIVE-005: who a grant the refresh writes names as its
    // granter, and why it was granted. A grant a refresh writes was granted by a
    // derivation and by nothing anyone decided, so its reason is the derivation, in the
    // structured form a code crosses the boundary in (CONV-CONTENT-001), whoever calls
    // it; a system principal is no account, so its grants name the nil subject, and the
    // drift check's carry the item that requires the check (D-166, D-207).
    private static (SubjectId Granter, string Reason) Recorded(AccessContext context, string relationship)
    {
        if (context.Acting is SubjectId acting)
        {
            return (acting, "derivation:" + relationship);
        }

        return context.Principal is { Name: DerivationDriftCheck.Job } principal
            && principal.MayRun(SystemOperation.Reconciliation)
                ? (default, DriftReason)
                : (default, "derivation:" + relationship);
    }

    // What every refresh is asked before it reads: a caller it admits, a declared
    // relationship that a materialised derivation follows from, and a context its grants
    // can be recorded under.
    private Refreshing Asked(AccessContext context, string derivation)
    {
        Admitted(context);

        RelationshipDeclaration relationship = model.Relationship(derivation)
            ?? throw new ArgumentException(
                "The model declares no relationship of that name.",
                nameof(derivation));

        List<Materialised> following = Declared(derivation);

        if (following.Count == 0)
        {
            throw new ArgumentException(
                "No materialised derivation follows from that relationship.",
                nameof(derivation));
        }

        (SubjectId granter, string reason) = Recorded(context, derivation);

        return new Refreshing(relationship, following, context, granter, reason);
    }

    // AUTHZ-DERIVE-005, IDN-PRIN-001, CONV-DESIGN-007 (D-207, D-208): a person's refresh
    // comes from the host's own gated operation. A system principal's is background work,
    // so it is admitted only where no request is served on the calling flow, as the gate
    // judges that, and only within the limits the principal already has: an
    // organization-scoped one acting as a declared job, a deployment-scoped one able to
    // run reconciliation, the drift check's operation. Any other call is the calling
    // code's fault, since the refresh asks no permission a refusal could name.
    private void Admitted(AccessContext context)
    {
        if (context.Principal is not SystemPrincipal principal)
        {
            return;
        }

        if (sessions.ServesRequest)
        {
            throw new InvalidOperationException(
                "A system principal's refresh is background work and is not called while a request is served.");
        }

        if (principal.IsDeploymentScoped
            ? !principal.MayRun(SystemOperation.Reconciliation)
            : !model.DeclaresJob(principal.Name))
        {
            throw new InvalidOperationException(
                "A refresh is called by a system principal only where it names a declared job or may run reconciliation.");
        }
    }

    private async ValueTask<Result<DerivationRefresh>> RefreshedAsync(
        Refreshing refreshing,
        ResourceId resource,
        IAsyncEnumerable<SubjectId> held,
        CancellationToken cancellationToken)
    {
        var on = new ResourceReference(refreshing.Relationship.On, resource);

        if (await records.FindAsync(on, cancellationToken).ConfigureAwait(false)
            is not RegisteredResource registered)
        {
            return Result.Success(new DerivationRefresh(0, 0));
        }

        // IDN-PRIN-001 AC2, AUTHZ-DERIVE-005 (D-209): an organization-scoped principal acts
        // for one organization, so a declared job's refresh of another's record is the
        // calling code's fault, raised before anything is written.
        if (refreshing.Calling.Principal is { IsDeploymentScoped: false } job
            && !job.ActsFor(registered.Organization))
        {
            throw new InvalidOperationException(
                "A declared job's refresh is called only on a record of the organization it acts for.");
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<DerivationRefresh>(notBegun);
        }

        // D-166 X3: the rows already written and the records beneath are read with the
        // organization's tree held, which a move and a second refresh hold too, so two
        // refreshes at once never both write one grant or each revoke the other's.
        await records.HoldAsync(registered.Organization, cancellationToken).ConfigureAwait(false);

        IReadOnlySet<SubjectId> holders = await ReadAsync(held, cancellationToken).ConfigureAwait(false);

        int written = 0;
        int revoked = 0;

        foreach (Materialised each in refreshing.Following)
        {
            DerivationRefresh changed = await ReconcileAsync(
                each,
                on,
                registered.Organization,
                holders,
                refreshing,
                cancellationToken).ConfigureAwait(false);

            written += changed.Written;
            revoked += changed.Revoked;
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(
                () => Result.Success(new DerivationRefresh(written, revoked)),
                Result.Failure<DerivationRefresh>);
    }

    private async ValueTask<DerivationRefresh> ReconcileAsync(
        Materialised following,
        ResourceReference on,
        OrganizationId organization,
        IReadOnlySet<SubjectId> holders,
        Refreshing refreshing,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ResourceId> beneath = await records
            .BeneathAsync(on, following.Type, cancellationToken)
            .ConfigureAwait(false);

        if (beneath.Count == 0)
        {
            return new DerivationRefresh(0, 0);
        }

        DateTimeOffset at = time.GetUtcNow();

        IReadOnlyList<Grant> existing = await grants
            .MaterialisedAsync(following.Role, following.Type, beneath, organization, at, cancellationToken)
            .ConfigureAwait(false);

        var standing = new HashSet<(Guid Subject, ResourceId Resource)>();
        int revoked = 0;

        foreach (Grant grant in existing)
        {
            if (grant.Subject.Type == SubjectType.User
                && holders.Contains(new SubjectId(grant.Subject.Value))
                && grant.ResourceId is ResourceId record)
            {
                standing.Add((grant.Subject.Value, record));
                continue;
            }

            Revoked(grant, refreshing.Granter, at, refreshing.Reason);

            await grants.RecordAsync(grant, cancellationToken).ConfigureAwait(false);
            await CorrectedAsync(refreshing, grant, retracted: true, at, cancellationToken).ConfigureAwait(false);
            revoked++;
        }

        int written = 0;

        foreach (SubjectId holder in holders)
        {
            foreach (ResourceId record in beneath)
            {
                if (standing.Contains((holder.Value, record)))
                {
                    continue;
                }

                Grant grant = Written(following, organization, holder, record, refreshing, at);

                await grants.CreateAsync(grant, cancellationToken).ConfigureAwait(false);
                await CorrectedAsync(refreshing, grant, retracted: false, at, cancellationToken).ConfigureAwait(false);

                written++;
            }
        }

        return new DerivationRefresh(written, revoked);
    }

    // AUTHZ-GRANT-003 AC5 and AC6, AUTHZ-DERIVE-005: a grant a refresh writes or takes
    // back is recorded in the refresh's transaction, naming who called it: a person as
    // acting identity, a system principal by its name and stated reason (D-187, D-207).
    private async ValueTask CorrectedAsync(
        Refreshing refreshing,
        Grant grant,
        bool retracted,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        AccessContext calling = refreshing.Calling;

        await audit
            .RecordAsync(
                new CorrectedGrant(
                    AuditRecordId.New(time),
                    calling.Acting,
                    calling.Effective,
                    calling.BreakGlassReason,
                    calling.Principal,
                    grant.Organization,
                    grant.Subject,
                    grant.Id,
                    grant.Role,
                    retracted,
                    at),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private Grant Written(
        Materialised following,
        OrganizationId organization,
        SubjectId holder,
        ResourceId record,
        Refreshing refreshing,
        DateTimeOffset at) =>
        Grant.Create(
            GrantId.New(time),
            GrantSubject.Of(holder),
            following.Role,
            organization,
            new ResourceReference(following.Type, record),
            deny: false,
            GrantKind.Materialised,
            expiresAt: null,
            refreshing.Granter,
            at,
            refreshing.Reason)
            .Match(
                created => created,
                error => throw new InvalidOperationException(error.Code.ToString()));

    // AUTHZ-DERIVE-005 AC1: materialisation is declared per derivation, and a
    // relationship may be followed by a derivation on more than one type.
    private List<Materialised> Declared(string relationship)
    {
        var following = new List<Materialised>();

        foreach (ResourceTypeDeclaration type in model.ResourceTypes)
        {
            foreach (DerivationDeclaration derivation in type.Derivations)
            {
                if (derivation.Materialised
                    && string.Equals(derivation.Relationship, relationship, StringComparison.Ordinal))
                {
                    following.Add(new Materialised(type.Name, derivation.Role));
                }
            }
        }

        return following;
    }

    // One materialised derivation: the type it is declared on and the role it confers.
    private sealed record Materialised(ResourceType Type, RoleName Role);

    // One refresh as it was asked: the relationship, the materialised derivations that
    // follow from it, who called it, and who and why its grants record.
    private sealed record Refreshing(
        RelationshipDeclaration Relationship,
        IReadOnlyList<Materialised> Following,
        AccessContext Calling,
        SubjectId Granter,
        string Reason);
}
