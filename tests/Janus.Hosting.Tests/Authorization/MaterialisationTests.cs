using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A materialised derivation: the refresh the host runs inside its own write, the rows
/// it leaves behind, and what a second refresh does when the relation has changed
/// underneath them (AUTHZ-DERIVE-005).
/// </summary>
/// <param name="host">The deployment the rows are written to.</param>
[Trait("kind", "integration")]
public sealed class MaterialisationTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");
    private static readonly ResourceType Document = ResourceType.Parse("document");
    private static readonly ResourceType Note = ResourceType.Parse("note");
    private static readonly RoleName Reviewer = RoleName.Parse("reviewer");

    /// <summary>
    /// AUTHZ-DERIVE-005 AC3: the refresh runs in the caller's own transaction, so a
    /// write that is rolled back leaves neither the fact nor the rows computed from it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_005_AC3_ARefreshRolledBackLeavesNoGrantAsync()
    {
        Reviewed reviewed = await ReviewedAsync();

        await using ServiceProvider deployment = Materialised();
        await using (AsyncServiceScope scope = deployment.CreateAsyncScope())
        {
            await using HostContext reading = host.Context();

            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await work.BeginAsync(TestContext.Current.CancellationToken);

            DerivationRefresh refreshed = Rendered(await scope.ServiceProvider
                .GetRequiredService<IDerivationMaterialiser>()
                .RefreshAsync(
                    AccessContext.Of(reviewed.Deployment.Granter),
                    "reviewer",
                    reviewed.Workspace.Id,
                    Sources(reading),
                    TestContext.Current.CancellationToken));

            Assert.Equal(1, refreshed.Written);

            // The operation ends without a commit, which is the write the host
            // abandoned partway through.
        }

        Assert.False(await AdmitsAsync(deployment, reviewed));
    }

    /// <summary>
    /// AUTHZ-DERIVE-005 AC1, AC3, AUTHZ-DERIVE-002: the rows the refresh wrote confer
    /// what the derivation conferred, and a deny on the record defeats them as it
    /// defeats any other grant.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_005_AC1_TheRowsTheRefreshWroteConferTheRoleAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Reviewed reviewed = await ReviewedAsync();

        await using ServiceProvider deployment = Materialised();

        Assert.False(await AdmitsAsync(deployment, reviewed));

        DerivationRefresh refreshed = await RefreshAsync(deployment, reviewed);

        Assert.Equal(new DerivationRefresh(1, 0), refreshed);
        Assert.True(refreshed.Drifted);
        Assert.True(await AdmitsAsync(deployment, reviewed));

        await reviewed.Deployment.GrantAsync(
            GrantSubject.Of(reviewed.Account),
            reviewed.Role,
            reviewed.Record,
            true,
            null,
            null,
            cancellationToken);

        Assert.False(await AdmitsAsync(deployment, reviewed));
    }

    /// <summary>
    /// AUTHZ-DERIVE-005 AC2: the row a derivation was precomputed into says so where
    /// an explanation names it, so it is never taken for one someone wrote. The type
    /// asked about is the disclosing one, because a concealing type is explained to
    /// nobody (AUTHZ-CONCEAL-003).
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_005_AC2_AnExplanationNamesTheGrantAsMaterialisedAsync()
    {
        Reviewed reviewed = await ReviewedAsync();

        await using ServiceProvider deployment = Materialised();
        await RefreshAsync(deployment, reviewed);

        await using AsyncServiceScope scope = deployment.CreateAsyncScope();

        AccessExplanation explained = Rendered(await scope.ServiceProvider
            .GetRequiredService<IAccessGate>()
            .ExplainAsync(
                AccessContext.Of(reviewed.Account),
                HostPermissions.ReadNote,
                reviewed.Note,
                TestContext.Current.CancellationToken));

        Assert.Equal(AccessOutcome.Allowed, explained.Outcome);
        Assert.Equal(GrantKind.Materialised, explained.Grant?.Kind);
        Assert.Equal(Reviewer, explained.Grant?.Role);
        Assert.Equal(reviewed.Workspace, explained.Grant?.InheritedFrom);
    }

    /// <summary>
    /// AUTHZ-DERIVE-007: a materialised derivation's grants are rows, so who can access a
    /// record is answered without the host's rows and reports them as materialised,
    /// each with its identifier and the container the relationship is declared on.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_AMaterialisedGrantIsReportedAsARowAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Reviewed reviewed = await ReviewedAsync();
        SubjectId administrator = await reviewed.Deployment.AccountAsync(cancellationToken);
        RoleName administering = await reviewed.Deployment.RoleAsync([Permissions.GrantRead], cancellationToken);

        await reviewed.Deployment.GrantAsync(
            GrantSubject.Of(administrator), administering, null, false, null, null, cancellationToken);

        await using ServiceProvider deployment = Materialised();
        await RefreshAsync(deployment, reviewed);

        await using AsyncServiceScope scope = deployment.CreateAsyncScope();

        ResourceAccess access = Rendered(await scope.ServiceProvider
            .GetRequiredService<IAccessGate>()
            .WhoCanAccessAsync(AccessContext.Of(administrator), reviewed.Note, cancellationToken));

        ExplainedGrant materialised = Assert.Single(
            access.Grants,
            grant => grant.Kind == GrantKind.Materialised);

        Assert.NotNull(materialised.Id);
        Assert.Equal(reviewed.Account.Value, materialised.SubjectId);
        Assert.Equal(Reviewer, materialised.Role);
        Assert.Equal(reviewed.Workspace, materialised.InheritedFrom);
        Assert.False(access.Partial);
        Assert.Empty(access.Unevaluated);
    }

    /// <summary>
    /// AUTHZ-DERIVE-005 AC3: where the relation changed without the refresh that
    /// should have followed it, the next refresh finds the difference, reports it and
    /// corrects it in the same run.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_005_AC3_ARefreshFindsTheDriftAndCorrectsItAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Reviewed reviewed = await ReviewedAsync();

        await using ServiceProvider deployment = Materialised();
        await RefreshAsync(deployment, reviewed);

        Assert.True(await AdmitsAsync(deployment, reviewed));

        // The host takes the fact away and does not refresh, which is the drift the
        // check exists to find.
        await reviewed.Deployment.UnreviewAsync(
            reviewed.Workspace,
            reviewed.Account,
            cancellationToken);

        Assert.True(await AdmitsAsync(deployment, reviewed));

        DerivationRefresh corrected = await RefreshAsync(deployment, reviewed);

        Assert.Equal(new DerivationRefresh(0, 1), corrected);
        Assert.True(corrected.Drifted);
        Assert.False(await AdmitsAsync(deployment, reviewed));

        Assert.False((await RefreshAsync(deployment, reviewed)).Drifted);
    }

    /// <summary>
    /// AUTHZ-DERIVE-005 AC1: materialisation is per derivation, so a refresh asked for
    /// a relationship no materialised derivation follows from has nothing to do and
    /// says so rather than writing rows a deployment did not ask for.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_005_AC1_ARefreshOfANonMaterialisedDerivationIsRefusedAsync()
    {
        Reviewed reviewed = await ReviewedAsync();

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();

        IDerivationMaterialiser materialiser =
            scope.ServiceProvider.GetRequiredService<IDerivationMaterialiser>();

        await Assert.ThrowsAsync<ArgumentException>(async () => await materialiser.RefreshAsync(
            AccessContext.Of(reviewed.Deployment.Granter),
            "reviewer",
            reviewed.Workspace.Id,
            Sources(reading),
            TestContext.Current.CancellationToken));
    }

    // What the host supplies from its own context, the same object every path on a type
    // with a derivation takes (D-161).
    private static FilterSources<HostDocument> Sources(HostContext reading) =>
        new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, document => document.Id)
            .Relationship("reviewer", reading.Reviewers);

    private static TRendering Rendered<TRendering>(Result<TRendering> outcome) =>
        outcome.Match(
            rendering => rendering,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    // The same deployment with the one derivation precomputed into grant rows.
    private ServiceProvider Materialised()
    {
        var services = new ServiceCollection();

        services.AddSingleton<TimeProvider>(new FixedTime(Deployment.Noon));
        services.AddJanus(
            host.ConnectionString,
            new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
            new FingerprintKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
            new byte[16],
            Encoding.UTF8.GetBytes(host.MaintenanceConnectionString),
            HostFixture.Declaration(materialised: true),
            ApplicationKind.Public);

        return services.BuildServiceProvider();
    }

    // The host's own call, from the operation that changed the relationship, inside the
    // unit of work that operation runs in (AUTHZ-DERIVE-005 AC3, D-161).
    private async Task<DerivationRefresh> RefreshAsync(IServiceProvider deployment, Reviewed reviewed)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = deployment.CreateAsyncScope();
        await using HostContext reading = host.Context();

        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);

        DerivationRefresh refreshed = Rendered(await scope.ServiceProvider
            .GetRequiredService<IDerivationMaterialiser>()
            .RefreshAsync(
                AccessContext.Of(reviewed.Deployment.Granter),
                "reviewer",
                reviewed.Workspace.Id,
                Sources(reading),
                cancellationToken));

        await work.CommitAsync(cancellationToken);

        return refreshed;
    }

    private static async Task<bool> AdmitsAsync(IServiceProvider deployment, Reviewed reviewed)
    {
        await using AsyncServiceScope scope = deployment.CreateAsyncScope();

        Result outcome = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .RequireAsync(
                AccessContext.Of(reviewed.Account),
                HostPermissions.Read,
                reviewed.Record,
                TestContext.Current.CancellationToken);

        return outcome.Match(() => true, _ => false);
    }

    // A workspace with a document in it, an account reviewing the workspace, and the
    // role the derivation names.
    private async Task<Reviewed> ReviewedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        ResourceReference workspace = Reference(Workspace);
        ResourceReference record = Reference(Document);
        ResourceReference note = Reference(Note);

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, workspace, cancellationToken);
        await deployment.RegisterAsync(note, workspace, cancellationToken);

        await deployment.NamedRoleAsync(
            Reviewer,
            [HostPermissions.Read, HostPermissions.ReadNote],
            cancellationToken);

        await deployment.ReviewAsync(workspace, account, cancellationToken);

        return new Reviewed(deployment, account, role, workspace, record, note);
    }

    private sealed record Reviewed(
        Deployment Deployment,
        SubjectId Account,
        RoleName Role,
        ResourceReference Workspace,
        ResourceReference Record,
        ResourceReference Note);
}
