using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Authorization.Resources;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authorization.Gate;

/// <summary>
/// Who can access one record: the grants that reach it and the grants its derivations
/// produce.
/// </summary>
/// <param name="derived">Which derivations reach the record, with what each confers.</param>
/// <param name="records">Where what contains the record is read.</param>
/// <param name="grants">Where the grants on the record and its containers are read.</param>
/// <param name="sources">Where a relationship's rows are read through the source the host declared.</param>
/// <param name="configuration">Where the bound on evaluating the derivations is read.</param>
/// <param name="time">The clock liveness and the bound are read against.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-007, AUTHZ-DERIVE-005 AC6 and AUTHZ-GATE-004 (D-161, D-166,
/// D-183). Stored grants, materialised ones among them, are rows and are read by query.
/// A derivation has no row to look up, so each one reaching the record is evaluated
/// over the host's relation for the record and every container of the type the
/// relationship is declared on, inside <c>authz.reverselookup.budget</c>; a derivation
/// the bound stops is named rather than left out in silence. The relation is read
/// through the source the host declared for it, one statement in the host's context
/// for each derivation, or over the rows a caller of the library hands in. A derivation
/// whose role allows nothing confers nothing and is not reported, as a stored grant of
/// such a role is not.
/// </remarks>
internal sealed class ReverseLookup(
    Derivations derived,
    IResourceStore records,
    IGrantStore grants,
    IRelationshipSources sources,
    IConfigurationStore configuration,
    TimeProvider time)
{
    private static readonly ResourceType OrganizationWide = ResourceType.Parse("organization");

    /// <summary>
    /// The grants reaching a record, and the grants its derivations produce.
    /// </summary>
    /// <param name="resource">The record, or the organization itself.</param>
    /// <param name="organization">The organization the record sits in.</param>
    /// <param name="relationships">
    /// The host's relationship rows, by name, where the caller handed them in, or
    /// nothing where they are read through the sources the host declared.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The grants, those on the record first, then those on each container nearest
    /// first, then those on the whole organization, and the derived ones after them.
    /// </returns>
    /// <exception cref="ArgumentException">A relationship a derivation follows from was not supplied.</exception>
    /// <exception cref="InvalidOperationException">No source is declared for a relationship that is read through one.</exception>
    public async ValueTask<ResourceAccess> LookedUpAsync(
        ResourceReference resource,
        OrganizationId organization,
        IReadOnlyDictionary<string, RelationshipRows>? relationships,
        CancellationToken cancellationToken)
    {
        bool organizationWide = resource.Type == OrganizationWide;

        IReadOnlyList<ResourceReference> ancestry = organizationWide
            ? []
            : await records.AncestryAsync(resource, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Grant> stored = await grants
            .OnAsync(resource, organization, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        List<ExplainedGrant> reaching =
        [
            .. stored
                .OrderBy(grant => Nearness(grant, ancestry))
                .Select(grant => Explained(grant, resource)),
        ];

        if (organizationWide)
        {
            return new ResourceAccess(resource, reaching, Partial: false, Unevaluated: []);
        }

        List<string> unevaluated = await DerivedAsync(
            resource,
            organization,
            ancestry,
            relationships,
            reaching,
            cancellationToken).ConfigureAwait(false);

        return new ResourceAccess(resource, reaching, unevaluated.Count > 0, unevaluated);
    }

    /// <summary>
    /// Whether the host declared a source for every relationship a derivation reaching
    /// records of the type follows from, so the view can be answered in full without
    /// rows handed in.
    /// </summary>
    /// <param name="type">The kind of thing the record is.</param>
    /// <returns>Whether every one can be read.</returns>
    public bool Sourced(ResourceType type) =>
        derived.Following(type).All(relationship => sources.Declares(relationship.Name));

    // AUTHZ-GATE-004: the container is named as an explanation names it, and a grant on
    // the record itself or on the whole organization names none.
    private static ExplainedGrant Explained(Grant grant, ResourceReference resource)
    {
        ResourceReference? on = grant.ResourceType is ResourceType type && grant.ResourceId is ResourceId id
            ? new ResourceReference(type, id)
            : null;

        return new ExplainedGrant(
            grant.Id,
            grant.Kind,
            grant.Subject.Type,
            grant.Subject.Value,
            grant.Role,
            grant.Deny,
            on == resource ? null : on);
    }

    // AUTHZ-GATE-004 (D-162): a grant a fact produced has no row, so it carries no
    // identifier, names itself as derived, and names the record the relationship's row
    // names where that is not the record asked about.
    private static ExplainedGrant Explained(
        ConferredDerivation each,
        SubjectId holder,
        ResourceReference above,
        ResourceReference resource) =>
        new(
            Id: null,
            GrantKind.Derived,
            SubjectType.User,
            holder.Value,
            each.Role,
            Deny: false,
            above == resource ? null : above);

    private static int Nearness(Grant grant, IReadOnlyList<ResourceReference> ancestry)
    {
        for (int depth = 0; depth < ancestry.Count; depth++)
        {
            if (grant.ResourceType == ancestry[depth].Type && grant.ResourceId == ancestry[depth].Id)
            {
                return depth;
            }
        }

        return ancestry.Count;
    }

    private static RelationshipRows Supplied(
        IReadOnlyDictionary<string, RelationshipRows> relationships,
        RelationshipDeclaration relationship) =>
        relationships.TryGetValue(relationship.Name, out RelationshipRows? rows)
            ? rows
            : throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The rows of the relationship '{relationship.Name}' were not supplied."),
                nameof(relationships));

    private static void Unevaluated(List<string> unevaluated, ConferredDerivation each)
    {
        if (!unevaluated.Contains(each.Relationship.Name, StringComparer.Ordinal))
        {
            unevaluated.Add(each.Relationship.Name);
        }
    }

    // AUTHZ-DERIVE-001: over the rows a caller handed in, a derivation reaches the record
    // through every container of the type its relationship is declared on, the record
    // itself included, and each holder of a row on one of them holds the role the
    // derivation confers. Nothing is answered for a derivation the bound stopped.
    private static async ValueTask<List<ExplainedGrant>?> HeldAsync(
        ConferredDerivation each,
        ResourceReference resource,
        IReadOnlyList<ResourceReference> ancestry,
        RelationshipRows rows,
        CancellationToken bound,
        CancellationToken cancellationToken)
    {
        RelationshipDeclaration relationship = each.Relationship;
        var held = new List<ExplainedGrant>();

        foreach (ResourceReference above in ancestry.Where(entry => entry.Type == relationship.On))
        {
            if (bound.IsCancellationRequested)
            {
                return null;
            }

            IAsyncEnumerable<SubjectId> holders = rows.HeldBy(
                Substitution.HeldOn(relationship, above.Id),
                relationship.Holder);

            await foreach (SubjectId holder in holders.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (bound.IsCancellationRequested)
                {
                    return null;
                }

                held.Add(Explained(each, holder, above, resource));
            }
        }

        return held;
    }

    // AUTHZ-DERIVE-007 AC2: the bound runs across the derivations and is read between
    // rows, and one it stops in the middle contributes nothing, so a derivation is
    // either answered whole or named.
    private async ValueTask<List<string>> DerivedAsync(
        ResourceReference resource,
        OrganizationId organization,
        IReadOnlyList<ResourceReference> ancestry,
        IReadOnlyDictionary<string, RelationshipRows>? relationships,
        List<ExplainedGrant> reaching,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ConferredDerivation> conferring = await derived
            .ConferringAsync(resource.Type, organization, cancellationToken)
            .ConfigureAwait(false);

        var unevaluated = new List<string>();

        if (conferring.Count == 0)
        {
            return unevaluated;
        }

        TimeSpan budget = (await configuration
                .ReadAsync(Settings.AuthzReverseLookupBudget, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        using var bounded = new CancellationTokenSource(budget, time);

        // A grant that confers nothing is not reported (AUTHZ-DERIVE-007), and a
        // derivation whose role allows nothing produces only such grants.
        foreach (ConferredDerivation each in conferring.Where(each => each.Confers.Count > 0))
        {
            List<ExplainedGrant>? held = relationships is null
                ? await HeldAsync(each, resource, bounded.Token, cancellationToken).ConfigureAwait(false)
                : await HeldAsync(
                        each,
                        resource,
                        ancestry,
                        Supplied(relationships, each.Relationship),
                        bounded.Token,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (held is null)
            {
                Unevaluated(unevaluated, each);
            }
            else
            {
                reaching.AddRange(held);
            }
        }

        return unevaluated;
    }

    // AUTHZ-DERIVE-005 AC6, LIB-HOST-001: through the source the host declared, the
    // holders on the record and on every container of the relationship's type are one
    // statement in the host's context, over the rows and the ancestry of one instance of
    // it. Nothing is answered for a derivation the bound stopped.
    private async ValueTask<List<ExplainedGrant>?> HeldAsync(
        ConferredDerivation each,
        ResourceReference resource,
        CancellationToken bound,
        CancellationToken cancellationToken)
    {
        if (bound.IsCancellationRequested)
        {
            return null;
        }

        var held = new List<ExplainedGrant>();

        await foreach (HeldRelationship row in sources
            .HeldAbove(each.Relationship, resource)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            if (bound.IsCancellationRequested)
            {
                return null;
            }

            held.Add(Explained(
                each,
                row.Holder,
                new ResourceReference(each.Relationship.On, row.Resource),
                resource));
        }

        return held;
    }
}
