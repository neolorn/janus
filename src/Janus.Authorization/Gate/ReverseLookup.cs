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
using Janus.Core.Configuration;

namespace Janus.Authorization.Gate;

/// <summary>
/// Who can access one record: the grants that reach it and, where the host supplied its
/// rows, the grants its derivations produce.
/// </summary>
/// <param name="model">The host's declaration, read for which derivations reach the record.</param>
/// <param name="records">Where what contains the record is read.</param>
/// <param name="grants">Where the grants on the record and its containers are read.</param>
/// <param name="configuration">Where the bound on evaluating the derivations is read.</param>
/// <param name="time">The clock liveness and the bound are read against.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-007 and AUTHZ-GATE-004 (D-161). Stored grants, materialised
/// ones among them, are rows and are read by query. A derivation has no row to look
/// up, so each one reaching the record is evaluated over the host's relation for the
/// record and every container of the type the relationship is declared on, inside
/// <c>authz.reverselookup.budget</c>; a derivation the bound stops is named rather
/// than left out in silence.
/// </remarks>
internal sealed class ReverseLookup(
    AuthorizationModel model,
    IResourceStore records,
    IGrantStore grants,
    IConfigurationStore configuration,
    TimeProvider time)
{
    private static readonly ResourceType OrganizationWide = ResourceType.Parse("organization");

    /// <summary>
    /// The grants reaching a record, and the grants its derivations produce where the
    /// host's rows are given.
    /// </summary>
    /// <param name="resource">The record, or the organization itself.</param>
    /// <param name="organization">The organization the record sits in.</param>
    /// <param name="relationships">
    /// The host's relationship rows, by name, or nothing where only the stored grants
    /// are asked for.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The grants, those on the record first, then those on each container nearest
    /// first, then those on the whole organization, and the derived ones after them.
    /// </returns>
    /// <exception cref="ArgumentException">A relationship a derivation follows from was not supplied.</exception>
    public async ValueTask<ResourceAccess> LookedUpAsync(
        ResourceReference resource,
        OrganizationId organization,
        IReadOnlyDictionary<string, RelationshipRows>? relationships,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ResourceReference> ancestry = resource.Type == OrganizationWide
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

        if (relationships is null)
        {
            return new ResourceAccess(resource, reaching, Partial: false, Unevaluated: []);
        }

        List<string> unevaluated = await DerivedAsync(
            resource,
            ancestry,
            relationships,
            reaching,
            cancellationToken).ConfigureAwait(false);

        return new ResourceAccess(resource, reaching, unevaluated.Count > 0, unevaluated);
    }

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

    // AUTHZ-DERIVE-007 AC2: the bound runs across the derivations and is read between
    // rows, and one it stops in the middle contributes nothing, so a derivation is
    // either answered whole or named.
    private async ValueTask<List<string>> DerivedAsync(
        ResourceReference resource,
        IReadOnlyList<ResourceReference> ancestry,
        IReadOnlyDictionary<string, RelationshipRows> relationships,
        List<ExplainedGrant> reaching,
        CancellationToken cancellationToken)
    {
        TimeSpan budget = (await configuration
                .ReadAsync(Settings.AuthzReverseLookupBudget, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, _ => Settings.AuthzReverseLookupBudget.Default);

        using var bounded = new CancellationTokenSource(budget, time);

        var unevaluated = new List<string>();

        foreach (ReachingDerivation each in model.Derivations(resource.Type))
        {
            if (each.Derivation.Materialised)
            {
                continue;
            }

            RelationshipRows rows = Supplied(relationships, each.Relationship);

            if (await HeldAsync(each, resource, ancestry, rows, bounded.Token, cancellationToken)
                    .ConfigureAwait(false)
                is List<ExplainedGrant> held)
            {
                reaching.AddRange(held);
            }
            else
            {
                Unevaluated(unevaluated, each);
            }
        }

        return unevaluated;
    }

    private static void Unevaluated(List<string> unevaluated, ReachingDerivation each)
    {
        if (!unevaluated.Contains(each.Relationship.Name, StringComparer.Ordinal))
        {
            unevaluated.Add(each.Relationship.Name);
        }
    }

    // AUTHZ-DERIVE-001: a derivation reaches the record through every container of the
    // type its relationship is declared on, the record itself included, and each
    // holder of a row on one of them holds the role the derivation confers. Nothing
    // is answered for a derivation the bound stopped.
    private static async ValueTask<List<ExplainedGrant>?> HeldAsync(
        ReachingDerivation each,
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

                held.Add(new ExplainedGrant(
                    Id: null,
                    GrantKind.Derived,
                    SubjectType.User,
                    holder.Value,
                    each.Derivation.Role,
                    Deny: false,
                    above == resource ? null : above));
            }
        }

        return held;
    }
}
