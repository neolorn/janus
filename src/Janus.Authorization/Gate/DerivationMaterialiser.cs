using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
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
/// <param name="time">The clock liveness is read against.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-005 and AUTHZ-DERIVE-002 (D-161). The grant is written on
/// each record of the type the derivation is declared on that the relationship's record
/// reaches, so the rows confer exactly what evaluating the derivation would confer and
/// inheritance carries them no further than it carries the derivation. The rows of the
/// relationship are the host's, read through what the host supplied.
/// </remarks>
internal sealed class DerivationMaterialiser(
    AuthorizationModel model,
    IResourceStore records,
    IGrantStore grants,
    TimeProvider time) : IDerivationMaterialiser
{
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

        RelationshipDeclaration relationship = model.Relationship(derivation)
            ?? throw new ArgumentException(
                "The model declares no relationship of that name.",
                nameof(derivation));

        List<Materialised> declared = Declared(derivation);

        if (declared.Count == 0)
        {
            throw new ArgumentException(
                "No materialised derivation follows from that relationship.",
                nameof(derivation));
        }

        SubjectId granter = context.Effective
            ?? throw new ArgumentException(
                "The grants a refresh writes are recorded against a subject, which this context names none of.",
                nameof(context));

        var on = new ResourceReference(relationship.On, resource);

        if (await records.FindAsync(on, cancellationToken).ConfigureAwait(false)
            is not RegisteredResource registered)
        {
            return Result.Success(new DerivationRefresh(0, 0));
        }

        IReadOnlySet<SubjectId> holders =
            await HoldersAsync(relationship, resource, sources, cancellationToken).ConfigureAwait(false);

        int written = 0;
        int revoked = 0;

        foreach (Materialised each in declared)
        {
            DerivationRefresh changed = await ReconcileAsync(
                each,
                on,
                registered.Organization,
                holders,
                granter,
                cancellationToken).ConfigureAwait(false);

            written += changed.Written;
            revoked += changed.Revoked;
        }

        return Result.Success(new DerivationRefresh(written, revoked));
    }

    // AUTHZ-GRANT-002: every grant records why it was granted. A materialised grant was
    // granted by a derivation and by nothing anyone decided, so what it records is the
    // derivation, in the structured form a code crosses the boundary in
    // (CONV-CONTENT-001).
    private static string Reason(string relationship) => "derivation:" + relationship;

    // The rows are the host's and carry the host's own provider, so reading them issues
    // nothing of the library's own against a host table (LIB-HOST-002, D-161).
    private static async ValueTask<IReadOnlySet<SubjectId>> HoldersAsync<TResource>(
        RelationshipDeclaration relationship,
        ResourceId resource,
        FilterSources<TResource> sources,
        CancellationToken cancellationToken)
    {
        if (!sources.Relationships.TryGetValue(relationship.Name, out RelationshipRows? rows))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The rows of the relationship '{relationship.Name}' were not supplied."),
                nameof(sources));
        }

        string named = resource.ToString();
        Expression<Func<string, bool>> names = value => value == named;
        ParameterExpression row = relationship.Resource.Parameters[0];

        IAsyncEnumerable<SubjectId> holders = rows.HeldBy(
            Expression.Lambda(
                new Substitution(names.Parameters[0], relationship.Resource.Body).Visit(names.Body),
                row),
            relationship.Holder);

        var subjects = new HashSet<SubjectId>();

        await foreach (SubjectId holder in holders.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            subjects.Add(holder);
        }

        return subjects;
    }

    // The reason is the library's own and is never blank, so a refusal here is a fault
    // in this file rather than something a caller can be told about.
    private static void Revoked(Grant grant, SubjectId by, DateTimeOffset at, string reason) =>
        grant.Revoke(by, at, reason).Match(
            () => true,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private async ValueTask<DerivationRefresh> ReconcileAsync(
        Materialised declared,
        ResourceReference on,
        OrganizationId organization,
        IReadOnlySet<SubjectId> holders,
        SubjectId granter,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ResourceId> beneath = await records
            .BeneathAsync(on, declared.Type, cancellationToken)
            .ConfigureAwait(false);

        if (beneath.Count == 0)
        {
            return new DerivationRefresh(0, 0);
        }

        DateTimeOffset at = time.GetUtcNow();

        IReadOnlyList<Grant> existing = await grants
            .MaterialisedAsync(declared.Role, declared.Type, beneath, organization, at, cancellationToken)
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

            Revoked(grant, granter, at, Reason(declared.Relationship));

            await grants.RecordAsync(grant, cancellationToken).ConfigureAwait(false);
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

                await grants
                    .CreateAsync(
                        Written(declared, organization, holder, record, granter, at),
                        cancellationToken)
                    .ConfigureAwait(false);

                written++;
            }
        }

        return new DerivationRefresh(written, revoked);
    }

    private Grant Written(
        Materialised declared,
        OrganizationId organization,
        SubjectId holder,
        ResourceId record,
        SubjectId granter,
        DateTimeOffset at) =>
        Grant.Create(
            GrantId.New(time),
            GrantSubject.Of(holder),
            declared.Role,
            organization,
            new ResourceReference(declared.Type, record),
            deny: false,
            GrantKind.Materialised,
            expiresAt: null,
            granter,
            at,
            Reason(declared.Relationship))
            .Match(
                created => created,
                error => throw new InvalidOperationException(error.Code.ToString()));

    // AUTHZ-DERIVE-005 AC1: materialisation is declared per derivation, and a
    // relationship may be followed by a derivation on more than one type.
    private List<Materialised> Declared(string relationship)
    {
        var declared = new List<Materialised>();

        foreach (ResourceTypeDeclaration type in model.ResourceTypes)
        {
            foreach (DerivationDeclaration derivation in type.Derivations)
            {
                if (derivation.Materialised
                    && string.Equals(derivation.Relationship, relationship, StringComparison.Ordinal))
                {
                    declared.Add(new Materialised(relationship, type.Name, derivation.Role));
                }
            }
        }

        return declared;
    }

    // One materialised derivation: the relationship it follows from, the type it is
    // declared on, and the role it confers.
    private sealed record Materialised(string Relationship, ResourceType Type, RoleName Role);
}
