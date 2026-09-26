using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Conformance;

/// <summary>
/// The host's truth table for one resource type, each case written into the
/// deployment and asked of the single check and of the list filter.
/// </summary>
/// <typeparam name="TResource">The host's type of the records asked about.</typeparam>
/// <param name="services">The host's deployment, as it registered the library.</param>
/// <param name="library">Where the library's own rows of a case are written.</param>
/// <param name="rows">The host's own rows of the type.</param>
/// <remarks>
/// Implements LIB-TEST-001 AC2, AUTHZ-TEST-001 and AUTHZ-PRIN-001. A case that decides
/// otherwise than the table says, through either path, is a finding: a list screen
/// showing what a check would refuse is a silent leak rather than a crash.
/// </remarks>
internal sealed class TruthTable<TResource>(
    IServiceProvider services,
    CaseRows library,
    IConformanceRows<TResource> rows)
    where TResource : class
{
    private static readonly JsonSerializerOptions Named = new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// Runs every case of the table.
    /// </summary>
    /// <param name="cases">The table.</param>
    /// <param name="cancellationToken">Abandons the run.</param>
    /// <returns>A finding for each case that decided otherwise than it states.</returns>
    /// <exception cref="ArgumentException">
    /// The deployment declares no such type, or a case names a scenario the type's
    /// declaration does not place it in.
    /// </exception>
    public async ValueTask<ConformanceReport> RunAsync(
        IReadOnlyList<TruthTableCase> cases,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cases);

        IReadOnlyList<ResourceTypeDeclaration> chain =
            Chain(services.GetRequiredService<AuthorizationDeclaration>(), rows.Type);

        foreach (TruthTableCase row in cases)
        {
            ArgumentNullException.ThrowIfNull(row);

            if (!Writable(row.Scenario, chain))
            {
                throw new ArgumentException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The scenario {row.Scenario} cannot be written for the type {rows.Type}, whose declaration does not place it so."),
                    nameof(cases));
            }
        }

        var findings = new List<ConformanceFinding>();

        foreach (TruthTableCase row in cases)
        {
            Written written = await WriteAsync(row, chain, cancellationToken).ConfigureAwait(false);
            bool checks = await ChecksAsync(written, row.Permission, cancellationToken).ConfigureAwait(false);
            bool admits = await AdmitsAsync(written, row.Permission, cancellationToken).ConfigureAwait(false);

            if (checks != row.Allowed || admits != row.Allowed)
            {
                findings.Add(new ConformanceFinding(ConformanceCheck.TruthTable, Disagreement(row, checks, admits)));
            }
        }

        return new ConformanceReport(findings);
    }

    // The type asked about and every type containing it, nearest first, as the
    // declaration places them. The model refused a cycle before the deployment started.
    private static List<ResourceTypeDeclaration> Chain(AuthorizationDeclaration declaration, ResourceType type)
    {
        var chain = new List<ResourceTypeDeclaration>();

        for (ResourceType? next = type; next is ResourceType named;)
        {
            ResourceTypeDeclaration declared = declaration.ResourceTypes
                .FirstOrDefault(candidate => candidate.Name == named)
                ?? throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"The deployment declares no type {named}."),
                    nameof(type));

            chain.Add(declared);
            next = declared.ContainedIn;
        }

        return chain;
    }

    private static bool Writable(TruthTableScenario scenario, IReadOnlyList<ResourceTypeDeclaration> chain) =>
        scenario switch
        {
            TruthTableScenario.GrantOnContainer or TruthTableScenario.DenyOnContainerOverGrant => chain.Count > 1,
            TruthTableScenario.GrantAboveContainer => chain.Count > 2,
            TruthTableScenario.DerivedGrant => chain[0].Derivations.Count > 0,
            TruthTableScenario.DerivedGrantOnContainer => chain.Skip(1).Any(declared => declared.Derivations.Count > 0),
            TruthTableScenario.DenyOverDerivedGrant => chain.Any(declared => declared.Derivations.Count > 0),
            _ => true,
        };

    // The nearest level of the chain, from the one given outward, whose type declares
    // a derivation; the scenario was checked writable before anything was written.
    private static int Deriving(IReadOnlyList<ResourceTypeDeclaration> chain, int from)
    {
        for (int level = from; level < chain.Count; level++)
        {
            if (chain[level].Derivations.Count > 0)
            {
                return level;
            }
        }

        throw new InvalidOperationException("No type of the chain declares a derivation.");
    }

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    private async ValueTask<Written> WriteAsync(
        TruthTableCase row,
        IReadOnlyList<ResourceTypeDeclaration> chain,
        CancellationToken cancellationToken)
    {
        OrganizationId organization = await library.OrganizationAsync(cancellationToken).ConfigureAwait(false);
        SubjectId granter = await library.AccountAsync(cancellationToken).ConfigureAwait(false);
        SubjectId account = await library.AccountAsync(cancellationToken).ConfigureAwait(false);
        RoleName role = await library
            .RoleAsync(
                row.Scenario == TruthTableScenario.RoleWithoutPermission ? null : row.Permission,
                cancellationToken)
            .ConfigureAwait(false);

        // One record at every level of the chain, the outermost first so each is
        // registered after its container, and a sibling beside the record asked about.
        var placed = new ResourceReference[chain.Count];
        var registrations = new List<ResourceRegistration>(chain.Count + 1);
        ResourceReference? container = null;

        for (int level = chain.Count - 1; level >= 0; level--)
        {
            placed[level] = Reference(chain[level].Name);
            registrations.Add(new ResourceRegistration(placed[level], organization, container, null));

            if (level > 0)
            {
                container = placed[level];
            }
        }

        ResourceReference sibling = Reference(rows.Type);
        registrations.Add(new ResourceRegistration(sibling, organization, container, null));

        foreach (ResourceRegistration registration in registrations)
        {
            await rows.WriteAsync(
                registration.Resource,
                organization,
                registration.ContainedIn,
                cancellationToken).ConfigureAwait(false);
        }

        AsyncServiceScope registering = services.CreateAsyncScope();

        await using (registering.ConfigureAwait(false))
        {
            (await registering.ServiceProvider.GetRequiredService<IResources>()
                    .RegisterManyAsync(registrations, cancellationToken)
                    .ConfigureAwait(false))
                .Switch(
                    () => { },
                    error => throw new InvalidOperationException(error.Code.ToString()));
        }

        var standing = new Standing(organization, granter, account, role, placed, sibling);

        await StandAsync(row, chain, standing, cancellationToken).ConfigureAwait(false);

        return new Written(organization, account, placed[0]);
    }

    // AUTHZ-TEST-001 AC1: what places the person where the scenario says, written
    // after every record it names is registered.
    private async ValueTask StandAsync(
        TruthTableCase row,
        IReadOnlyList<ResourceTypeDeclaration> chain,
        Standing standing,
        CancellationToken cancellationToken)
    {
        var holder = GrantSubject.Of(standing.Account);
        ResourceReference record = standing.Placed[0];

        switch (row.Scenario)
        {
            case TruthTableScenario.GrantOnRecord:
            case TruthTableScenario.RoleWithoutPermission:
                await GrantAsync(standing.Grant(holder, record), cancellationToken).ConfigureAwait(false);
                break;

            case TruthTableScenario.GrantOnContainer:
                await GrantAsync(standing.Grant(holder, standing.Placed[1]), cancellationToken).ConfigureAwait(false);
                break;

            case TruthTableScenario.GrantAboveContainer:
                await GrantAsync(standing.Grant(holder, standing.Placed[2]), cancellationToken).ConfigureAwait(false);
                break;

            case TruthTableScenario.GrantOnOrganization:
                await GrantAsync(standing.Grant(holder, null), cancellationToken).ConfigureAwait(false);
                break;

            case TruthTableScenario.GrantOnSibling:
                await GrantAsync(standing.Grant(holder, standing.Sibling), cancellationToken).ConfigureAwait(false);
                break;

            case TruthTableScenario.NoGrant:
                break;

            case TruthTableScenario.GrantToGroup:
                GroupId team = await library
                    .GroupAsync(standing.Organization, holder, [(holder, 1)], cancellationToken)
                    .ConfigureAwait(false);
                await GrantAsync(standing.Grant(GrantSubject.Of(team), record), cancellationToken).ConfigureAwait(false);
                break;

            case TruthTableScenario.GrantToNestedGroup:
                GroupId inner = await library
                    .GroupAsync(standing.Organization, holder, [(holder, 1)], cancellationToken)
                    .ConfigureAwait(false);
                GroupId outer = await library
                    .GroupAsync(
                        standing.Organization,
                        GrantSubject.Of(inner),
                        [(GrantSubject.Of(inner), 1), (holder, 2)],
                        cancellationToken)
                    .ConfigureAwait(false);
                await GrantAsync(standing.Grant(GrantSubject.Of(outer), record), cancellationToken).ConfigureAwait(false);
                break;

            case TruthTableScenario.DenyOverGrant:
                await GrantAsync(standing.Grant(holder, null), cancellationToken).ConfigureAwait(false);
                await GrantAsync(standing.Grant(holder, record) with { Deny = true }, cancellationToken).ConfigureAwait(false);
                break;

            case TruthTableScenario.DenyOnContainerOverGrant:
                await GrantAsync(standing.Grant(holder, record), cancellationToken).ConfigureAwait(false);
                await GrantAsync(
                    standing.Grant(holder, standing.Placed[1]) with { Deny = true },
                    cancellationToken).ConfigureAwait(false);
                break;

            case TruthTableScenario.ExpiredGrant:
                await GrantAsync(standing.Grant(holder, record) with { Expired = true }, cancellationToken).ConfigureAwait(false);
                break;

            case TruthTableScenario.RevokedGrant:
                await GrantAsync(standing.Grant(holder, record) with { Revoked = true }, cancellationToken).ConfigureAwait(false);
                break;

            case TruthTableScenario.GrantInAnotherOrganization:
                OrganizationId elsewhere = await library.OrganizationAsync(cancellationToken).ConfigureAwait(false);
                await GrantAsync(
                    standing.Grant(holder, record) with { Organization = elsewhere },
                    cancellationToken).ConfigureAwait(false);
                break;

            case TruthTableScenario.DerivedGrant:
                await RelateAsync(chain, 0, standing, row.Permission, cancellationToken).ConfigureAwait(false);
                break;

            case TruthTableScenario.DerivedGrantOnContainer:
                await RelateAsync(chain, Deriving(chain, 1), standing, row.Permission, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case TruthTableScenario.DenyOverDerivedGrant:
                await RelateAsync(chain, Deriving(chain, 0), standing, row.Permission, cancellationToken)
                    .ConfigureAwait(false);
                await GrantAsync(standing.Grant(holder, record) with { Deny = true }, cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(row), row.Scenario, "No such scenario.");
        }
    }

    private async ValueTask GrantAsync(CaseGrant grant, CancellationToken cancellationToken) =>
        await library.GrantAsync(grant, cancellationToken).ConfigureAwait(false);

    // AUTHZ-DERIVE-001, AUTHZ-DERIVE-005: the fact is the host's own row; where the
    // derivation is materialised, the host's write refreshes it in the same unit of
    // work, which is what the suite does in its place.
    private async ValueTask RelateAsync(
        IReadOnlyList<ResourceTypeDeclaration> chain,
        int level,
        Standing standing,
        Permission permission,
        CancellationToken cancellationToken)
    {
        DerivationDeclaration derivation = chain[level].Derivations[0];
        ResourceReference on = standing.Placed[level];

        await library.AllowAsync(derivation.Role, permission, cancellationToken).ConfigureAwait(false);
        await rows.RelateAsync(derivation.Relationship, on, standing.Account, cancellationToken).ConfigureAwait(false);

        if (!derivation.Materialised)
        {
            return;
        }

        AsyncServiceScope scope = services.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await work.BeginAsync(cancellationToken).ConfigureAwait(false);

            (await scope.ServiceProvider.GetRequiredService<IDerivationMaterialiser>()
                    .RefreshAsync(
                        AccessContext.Of(standing.Granter),
                        derivation.Relationship,
                        on.Id,
                        rows.Sources,
                        cancellationToken)
                    .ConfigureAwait(false))
                .Switch(
                    _ => { },
                    error => throw new InvalidOperationException(error.Code.ToString()));

            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<bool> ChecksAsync(
        Written written,
        Permission permission,
        CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = services.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            Result outcome = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .RequireAsync(
                    AccessContext.Of(written.Account),
                    permission,
                    written.Record,
                    rows.Sources,
                    cancellationToken)
                .ConfigureAwait(false);

            return outcome.Match(() => true, _ => false);
        }
    }

    // The filter applied to the host's own rows in the host's own query, and asked
    // whether the record is among what it admits. A filter refused admits nothing.
    private async ValueTask<bool> AdmitsAsync(
        Written written,
        Permission permission,
        CancellationToken cancellationToken)
    {
        Expression<Func<TResource, bool>>? admitting;
        AsyncServiceScope scope = services.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            Result<Expression<Func<TResource, bool>>> filter = await scope.ServiceProvider
                .GetRequiredService<IAccessGate>()
                .FilterAsync(
                    AccessContext.Of(written.Account),
                    permission,
                    rows.Type,
                    written.Organization,
                    rows.Sources,
                    cancellationToken)
                .ConfigureAwait(false);

            admitting = filter.Match<Expression<Func<TResource, bool>>?>(admitted => admitted, _ => null);
        }

        if (admitting is null)
        {
            return false;
        }

        return await rows.Rows
            .Where(admitting)
            .Where(Identified(written.Record))
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private Expression<Func<TResource, bool>> Identified(ResourceReference record)
    {
        Expression<Func<TResource, string>> identifier = rows.Sources.Identifier;

        return Expression.Lambda<Func<TResource, bool>>(
            Expression.Equal(identifier.Body, Expression.Constant(record.Id.ToString())),
            identifier.Parameters);
    }

    private Error Disagreement(TruthTableCase row, bool checks, bool admits) =>
        new(
            ErrorCodes.TruthTableDisagreement,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["type"] = JsonSerializer.SerializeToElement(rows.Type.ToString()),
                ["scenario"] = JsonSerializer.SerializeToElement(row.Scenario, Named),
                ["permission"] = JsonSerializer.SerializeToElement(row.Permission.ToString()),
                ["expected"] = JsonSerializer.SerializeToElement(row.Allowed),
                ["check"] = JsonSerializer.SerializeToElement(checks),
                ["filter"] = JsonSerializer.SerializeToElement(admits),
            });

    // What a case wrote that its question is asked of.
    private sealed record Written(OrganizationId Organization, SubjectId Account, ResourceReference Record);

    // What a case wrote that the person is placed against: the record at every level
    // of the chain, the record asked about first, and the sibling beside it.
    private sealed record Standing(
        OrganizationId Organization,
        SubjectId Granter,
        SubjectId Account,
        RoleName Role,
        IReadOnlyList<ResourceReference> Placed,
        ResourceReference Sibling)
    {
        public CaseGrant Grant(GrantSubject subject, ResourceReference? on) =>
            new(subject, Role, Organization, on, Granter);
    }
}
