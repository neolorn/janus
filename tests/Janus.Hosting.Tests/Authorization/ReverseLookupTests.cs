using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// Who can access one record: the stored grants read by query and the derived ones
/// evaluated over the host's relation, inside the bound on evaluating them
/// (AUTHZ-DERIVE-007, AUTHZ-GATE-004).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
[Trait("kind", "integration")]
public sealed class ReverseLookupTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Note = ResourceType.Parse("note");
    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");
    private static readonly ResourceType OrganizationWide = ResourceType.Parse("organization");

    // The role the host's declaration says a reviewer holds on what they review.
    private static readonly RoleName Reviewer = RoleName.Parse("reviewer");

    /// <summary>
    /// AUTHZ-DERIVE-007 AC1, AUTHZ-GATE-004: the stored grants and the derived ones are
    /// reported distinctly. A stored grant carries its identifier and the container it
    /// sits on; a derived grant carries no identifier, says it is derived, and names the
    /// role the derivation confers and the container the relationship is declared on.
    /// The grant on the whole organization is reported after those on containers.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_AC1_StoredAndDerivedGrantsAreReportedDistinctlyAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Deployed deployed = await DeployAsync();

        await deployed.Deployment.ReviewAsync(deployed.Container, deployed.Reviewing, cancellationToken);

        ResourceAccess access = Answered(await LookedUpAsync(deployed, deployed.Note, withRows: true));

        Assert.Equal(deployed.Note, access.Resource);
        Assert.False(access.Partial);
        Assert.Empty(access.Unevaluated);
        Assert.Collection(
            access.Grants,
            stored =>
            {
                Assert.Equal(deployed.Stored, stored.Id);
                Assert.Equal(GrantKind.Stored, stored.Kind);
                Assert.Equal(deployed.Account.Value, stored.SubjectId);
                Assert.Equal(deployed.Container, stored.InheritedFrom);
            },
            organizationWide =>
            {
                Assert.Equal(deployed.Reading, organizationWide.Id);
                Assert.Equal(deployed.Administrator.Value, organizationWide.SubjectId);
                Assert.Null(organizationWide.InheritedFrom);
            },
            derived =>
            {
                Assert.Null(derived.Id);
                Assert.Equal(GrantKind.Derived, derived.Kind);
                Assert.Equal(SubjectType.User, derived.SubjectType);
                Assert.Equal(deployed.Reviewing.Value, derived.SubjectId);
                Assert.Equal(Reviewer, derived.Role);
                Assert.False(derived.Deny);
                Assert.Equal(deployed.Container, derived.InheritedFrom);
            });
    }

    /// <summary>
    /// AUTHZ-DERIVE-007 AC2: where evaluation runs past
    /// <c>authz.reverselookup.budget</c>, the answer carries <c>partial</c> and names the
    /// derivation not evaluated, and the stored grants are still reported.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_AC2_PastTheBudgetTheAnswerIsPartialAndNamesWhatWentUnevaluatedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Deployed deployed = await DeployAsync();

        await deployed.Deployment.ReviewAsync(deployed.Container, deployed.Reviewing, cancellationToken);
        await BudgetAsync("PT0S");

        try
        {
            ResourceAccess access = Answered(await LookedUpAsync(deployed, deployed.Note, withRows: true));

            Assert.True(access.Partial);
            Assert.Equal(["reviewer"], access.Unevaluated);
            Assert.DoesNotContain(access.Grants, grant => grant.Kind == GrantKind.Derived);
            Assert.Contains(access.Grants, grant => grant.Id == deployed.Stored);
        }
        finally
        {
            await BudgetAsync(budget: null);
        }
    }

    /// <summary>
    /// AUTHZ-DERIVE-007, D-162: without the host's rows, a record a derivation reaches
    /// is refused as a fault rather than answered from the stored grants alone, and the
    /// whole of the organization, which no derivation reaches, is answered.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_WithoutTheHostsRowsARecordADerivationReachesIsRefusedAsync()
    {
        Deployed deployed = await DeployAsync();

        Result<ResourceAccess> record = await LookedUpAsync(deployed, deployed.Note, withRows: false);
        ResourceAccess organization = Answered(await LookedUpAsync(
            deployed,
            new ResourceReference(OrganizationWide, ResourceId.Parse(deployed.Deployment.Organization.Value.ToString())),
            withRows: false));

        Assert.Equal(
            ErrorCodes.DerivationSourcesMissing,
            record.Match(_ => throw new InvalidOperationException("It was answered."), error => error.Code));
        Assert.Contains(organization.Grants, grant => grant.Id == deployed.Reading);
        Assert.DoesNotContain(organization.Grants, grant => grant.Id == deployed.Stored);
    }

    /// <summary>
    /// AUTHZ-DERIVE-007, AUTHZ-CONCEAL-005: the view is refused to a principal without
    /// <c>grant:read</c> in the organization the record sits in, and to everyone once the
    /// organization's deletion is requested.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_TheViewRequiresGrantReadInTheRecordsOrganizationAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Deployed deployed = await DeployAsync();

        Result<ResourceAccess> withoutIt = await LookedUpAsync(
            AccessContext.Of(deployed.Account),
            deployed.Note,
            withRows: true);

        await deployed.Deployment.SuspendAsync(cancellationToken);

        Result<ResourceAccess> suspended = await LookedUpAsync(deployed, deployed.Note, withRows: true);

        Assert.Equal(ErrorCodes.Denied, Refusal(withoutIt));
        Assert.Equal(ErrorCodes.Denied, Refusal(suspended));
    }

    /// <summary>
    /// AUTHZ-DERIVE-007: a grant of a role that allows nothing gives no access and is not
    /// reported, nor is a revoked or an expired one.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_AGrantThatConfersNothingIsNotReportedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Deployed deployed = await DeployAsync();
        RoleName empty = await deployed.Deployment.RoleAsync([], cancellationToken);

        GrantId nothing = await deployed.Deployment.GrantAsync(
            GrantSubject.Of(deployed.Account), empty, deployed.Container, false, null, null, cancellationToken);
        GrantId expired = await deployed.Deployment.GrantAsync(
            GrantSubject.Of(deployed.Account),
            deployed.Role,
            deployed.Note,
            false,
            Deployment.Noon - TimeSpan.FromMinutes(1),
            null,
            cancellationToken);

        await deployed.Deployment.RevokeAsync(deployed.Stored, cancellationToken);

        ResourceAccess access = Answered(await LookedUpAsync(deployed, deployed.Note, withRows: true));

        Assert.DoesNotContain(access.Grants, grant => grant.Id == nothing);
        Assert.DoesNotContain(access.Grants, grant => grant.Id == expired);
        Assert.DoesNotContain(access.Grants, grant => grant.Id == deployed.Stored);
    }

    /// <summary>
    /// AUTHZ-DERIVE-007: a record the registry does not hold, or a type the host did not
    /// declare, is refused as malformed, naming the member.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_AnUnknownRecordOrTypeIsRefusedAsMalformedAsync()
    {
        Deployed deployed = await DeployAsync();

        Result<ResourceAccess> unregistered = await LookedUpAsync(deployed, Reference(Note), withRows: true);
        Result<ResourceAccess> undeclared = await LookedUpAsync(
            deployed,
            Reference(ResourceType.Parse("invoice")),
            withRows: true);

        Assert.Equal(ErrorCodes.RequestMalformed, Refusal(unregistered));
        Assert.Equal("resourceId", Member(unregistered));
        Assert.Equal(ErrorCodes.RequestMalformed, Refusal(undeclared));
        Assert.Equal("resourceType", Member(undeclared));
    }

    private static ResourceAccess Answered(Result<ResourceAccess> outcome) =>
        outcome.Match(
            access => access,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private static ErrorCode Refusal(Result<ResourceAccess> outcome) =>
        outcome.Match(_ => throw new InvalidOperationException("It was answered."), error => error.Code);

    private static string? Member(Result<ResourceAccess> outcome) =>
        outcome.Match(
            _ => throw new InvalidOperationException("It was answered."),
            error => error.Details["member"].GetString());

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    private static FilterSources<HostDocument> Sources(HostContext reading) =>
        new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, document => document.Id)
            .Relationship("reviewer", reading.Reviewers);

    private Task<Result<ResourceAccess>> LookedUpAsync(
        Deployed deployed,
        ResourceReference resource,
        bool withRows) =>
        LookedUpAsync(AccessContext.Of(deployed.Administrator), resource, withRows);

    private async Task<Result<ResourceAccess>> LookedUpAsync(
        AccessContext context,
        ResourceReference resource,
        bool withRows)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();
        IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();

        return withRows
            ? await gate.WhoCanAccessAsync(context, resource, Sources(reading), cancellationToken)
            : await gate.WhoCanAccessAsync(context, resource, cancellationToken);
    }

    // The deployment's bound on evaluating derivations, or its default where nothing
    // is given.
    private async Task BudgetAsync(string? budget)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            budget is null
                ? "DELETE FROM identity.settings WHERE key = @key;"
                : "INSERT INTO identity.settings (key, value) VALUES (@key, @value);",
            new { key = Settings.AuthzReverseLookupBudget.Key.ToString(), value = budget },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private async Task<Deployed> DeployAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync([HostPermissions.ReadNote], cancellationToken);

        await deployment.NamedRoleAsync(Reviewer, [HostPermissions.ReadNote], cancellationToken);

        SubjectId account = await deployment.AccountAsync(cancellationToken);
        SubjectId reviewing = await deployment.AccountAsync(cancellationToken);
        SubjectId administrator = await deployment.AccountAsync(cancellationToken);

        ResourceReference container = Reference(Workspace);
        ResourceReference note = Reference(Note);

        await deployment.RegisterAsync(container, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(note, container, cancellationToken);

        RoleName administering = await deployment.RoleAsync([Permissions.GrantRead], cancellationToken);

        GrantId reading = await deployment.GrantAsync(
            GrantSubject.Of(administrator), administering, null, false, null, null, cancellationToken);
        GrantId stored = await deployment.GrantAsync(
            GrantSubject.Of(account), role, container, false, null, null, cancellationToken);

        return new Deployed(deployment, account, reviewing, administrator, role, stored, reading, container, note);
    }

    // One case's rows: the account holding a stored grant on the container, the account
    // reviewing the container, the account holding grant:read on the whole organization,
    // the role and the two grants, and the records.
    private sealed record Deployed(
        Deployment Deployment,
        SubjectId Account,
        SubjectId Reviewing,
        SubjectId Administrator,
        RoleName Role,
        GrantId Stored,
        GrantId Reading,
        ResourceReference Container,
        ResourceReference Note);
}
