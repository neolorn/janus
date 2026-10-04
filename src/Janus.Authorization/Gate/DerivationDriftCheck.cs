using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Authorization.Model;
using Janus.Authorization.Resources;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// The drift check of materialised derivations: every one is evaluated again over the
/// rows the host's declared source answers, what its grants no longer match is brought
/// back into step, and the difference is reported.
/// </summary>
/// <param name="model">The host's declaration, read for the materialised derivations.</param>
/// <param name="sources">Where a relationship's rows are read through the source the host declared.</param>
/// <param name="grants">Where the grants a derivation was precomputed into are read.</param>
/// <param name="records">Where what contains a record is read.</param>
/// <param name="materialiser">The refresh that brings one record back into step.</param>
/// <param name="alerts">Where a difference is raised.</param>
/// <param name="work">The transaction a derivation's corrections and their alert are written in.</param>
/// <param name="time">The clock liveness is read against.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-005, AUTHZ-GRANT-003, INF-BG-001 and IDN-PRIN-001 (D-161,
/// D-166, D-183). Where the host's write and its refresh did not both commit, the grants
/// and the rows say different things until the next refresh or this check. A derivation
/// is evaluated in one statement in the host's context; a relationship whose grants
/// match changes nothing and takes no lock. Where they differ, each record the
/// difference touches is refreshed under this check's principal, which decides again
/// with the organization's tree held, and <c>degradation</c> is raised naming the
/// derivation in the transaction that corrects it.
/// </remarks>
internal sealed class DerivationDriftCheck(
    AuthorizationModel model,
    IRelationshipSources sources,
    IGrantStore grants,
    IResourceStore records,
    DerivationMaterialiser materialiser,
    IAccessAlerts alerts,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// The name of the job that runs the check, which is the principal it runs as
    /// (chapter 10 section 5.29).
    /// </summary>
    public const string Job = "derivation-driftcheck";

    /// <summary>
    /// Evaluates every materialised derivation once and corrects what drifted.
    /// </summary>
    /// <param name="context">The system principal the check runs as.</param>
    /// <param name="cancellationToken">Abandons the check.</param>
    /// <returns>
    /// How many grants were written or taken back, or the failure that stopped the
    /// check.
    /// </returns>
    /// <exception cref="ArgumentException">The context is not a principal that may reconcile.</exception>
    public async ValueTask<Result<int>> CheckAsync(AccessContext context, CancellationToken cancellationToken)
    {
        _ = Reconciling(context);

        int corrected = 0;

        foreach (RelationshipDeclaration relationship in Materialised())
        {
            IReadOnlyList<ResourceId> drifted =
                await DriftedAsync(relationship, cancellationToken).ConfigureAwait(false);

            if (drifted.Count == 0)
            {
                continue;
            }

            Error? failure = null;

            (await CorrectedAsync(context, relationship, drifted, cancellationToken).ConfigureAwait(false))
                .Switch(changed => corrected += changed, error => failure = error);

            if (failure is not null)
            {
                return Result.Failure<int>(failure);
            }
        }

        return Result.Success(corrected);
    }

    // INF-BG-002 AC1, IDN-PRIN-001 AC3 (D-166, 304): the check runs as a named
    // principal that may reconcile, and never as nobody.
    private static SystemPrincipal Reconciling(AccessContext context) =>
        context?.Principal is { } principal && principal.MayRun(SystemOperation.Reconciliation)
            ? principal
            : throw new ArgumentException(
                "The drift check runs as a system principal that may reconcile.",
                nameof(context));

    // Every relationship a materialised derivation follows from, each once, in the
    // order the model declares them.
    private List<RelationshipDeclaration> Materialised()
    {
        var following = new List<RelationshipDeclaration>();

        foreach (string name in model.ResourceTypes
            .SelectMany(type => type.Derivations)
            .Where(derivation => derivation.Materialised)
            .Select(derivation => derivation.Relationship)
            .Distinct(StringComparer.Ordinal))
        {
            if (model.Relationship(name) is RelationshipDeclaration relationship)
            {
                following.Add(relationship);
            }
        }

        return following;
    }

    // AUTHZ-DERIVE-005 AC6: each materialised derivation following from the relationship
    // is evaluated in one statement in the host's context and held against the live
    // grants it was precomputed into. What is answered is the records, of the type the
    // relationship is declared on, whose rows and grants differ, in one order whatever
    // process asks, so two checks at once hold what they correct in the same order.
    private async ValueTask<IReadOnlyList<ResourceId>> DriftedAsync(
        RelationshipDeclaration relationship,
        CancellationToken cancellationToken)
    {
        var drifted = new HashSet<ResourceId>();
        DateTimeOffset at = time.GetUtcNow();

        foreach (ResourceTypeDeclaration type in model.ResourceTypes)
        {
            foreach (DerivationDeclaration derivation in type.Derivations.Where(each =>
                each.Materialised
                && string.Equals(each.Relationship, relationship.Name, StringComparison.Ordinal)))
            {
                var conferred = new Dictionary<(Guid Holder, ResourceId Record), ResourceId>();

                await foreach (ConferredRecord each in sources
                    .Conferred(relationship, type.Name)
                    .WithCancellation(cancellationToken)
                    .ConfigureAwait(false))
                {
                    _ = conferred.TryAdd((each.Holder.Value, each.Record), each.Named);
                }

                IReadOnlyList<Grant> existing = await grants
                    .MaterialisedAsync(derivation.Role, type.Name, at, cancellationToken)
                    .ConfigureAwait(false);

                foreach (Grant grant in existing)
                {
                    if (grant.Subject.Type == SubjectType.User
                        && grant.ResourceId is ResourceId record
                        && conferred.Remove((grant.Subject.Value, record)))
                    {
                        continue;
                    }

                    await StaleAsync(relationship, type.Name, grant, drifted, cancellationToken)
                        .ConfigureAwait(false);
                }

                // What is left is conferred by a row and precomputed into no grant.
                drifted.UnionWith(conferred.Values);
            }
        }

        return [.. drifted.OrderBy(resource => resource.ToString(), StringComparer.Ordinal)];
    }

    // A grant no row supports sits on a record the relationship reached through the
    // record itself or one containing it, so each of those is where a refresh takes it
    // back.
    private async ValueTask StaleAsync(
        RelationshipDeclaration relationship,
        ResourceType type,
        Grant grant,
        HashSet<ResourceId> drifted,
        CancellationToken cancellationToken)
    {
        if (grant.ResourceId is not ResourceId record)
        {
            return;
        }

        IReadOnlyList<ResourceReference> ancestry = await records
            .AncestryAsync(new ResourceReference(type, record), cancellationToken)
            .ConfigureAwait(false);

        drifted.UnionWith(ancestry.Where(above => above.Type == relationship.On).Select(above => above.Id));
    }

    // The records are refreshed and the difference is raised in one unit of work, so a
    // correction is never committed without the alert that reports it. Each refresh
    // decides again with its organization's tree held, so what a host's own refresh
    // settled meanwhile is found settled and changes nothing; only a change is drift.
    private async ValueTask<Result<int>> CorrectedAsync(
        AccessContext context,
        RelationshipDeclaration relationship,
        IReadOnlyList<ResourceId> drifted,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<int>(notBegun);
        }

        int changed = 0;

        foreach (ResourceId resource in drifted)
        {
            Error? failure = null;

            (await materialiser
                    .RefreshAsync(context, relationship.Name, resource, cancellationToken)
                    .ConfigureAwait(false))
                .Switch(refreshed => changed += refreshed.Written + refreshed.Revoked, error => failure = error);

            if (failure is not null)
            {
                await work.RollbackAsync().ConfigureAwait(false);

                return Result.Failure<int>(failure);
            }
        }

        if (changed > 0
            && (await alerts
                    .RaiseAsync(
                        AlertCondition.Degradation,
                        scope: null,
                        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                        {
                            ["derivation"] = JsonSerializer.SerializeToElement(relationship.Name),
                        },
                        cancellationToken)
                    .ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error unraised)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<int>(unraised);
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => Result.Success(changed), Result.Failure<int>);
    }
}
