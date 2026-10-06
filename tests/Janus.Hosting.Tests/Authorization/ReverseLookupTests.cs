using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
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
    /// The grant on the whole organization is reported after those on containers. The
    /// answer is the same whether the derivation is read through the relationship
    /// source the host declared, as the view's endpoint reads it, or over rows a caller
    /// of the library hands in (LIB-HOST-001, D-183).
    /// </summary>
    /// <param name="withRows">Whether the caller hands the rows in.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUTHZ_DERIVE_007_AC1_StoredAndDerivedGrantsAreReportedDistinctlyAsync(bool withRows)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Deployed deployed = await DeployAsync();

        await deployed.Deployment.ReviewAsync(deployed.Container, deployed.Reviewing, cancellationToken);

        ResourceAccess access = Answered(await LookedUpAsync(deployed, deployed.Note, withRows));

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
    /// derivation not evaluated, and the stored grants are still reported, through the
    /// declared source as over rows handed in.
    /// </summary>
    /// <param name="withRows">Whether the caller hands the rows in.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUTHZ_DERIVE_007_AC2_PastTheBudgetTheAnswerIsPartialAndNamesWhatWentUnevaluatedAsync(bool withRows)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Deployed deployed = await DeployAsync();

        await deployed.Deployment.ReviewAsync(deployed.Container, deployed.Reviewing, cancellationToken);
        await BudgetAsync("PT0S");

        try
        {
            ResourceAccess access = Answered(await LookedUpAsync(deployed, deployed.Note, withRows));

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
    /// AUTHZ-DERIVE-007, D-162: where no source is declared for the relationship and no
    /// rows are handed in, a record a derivation reaches is refused as a fault rather
    /// than answered from the stored grants alone, and the whole of the organization,
    /// which no derivation reaches, is answered.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_WithoutTheHostsRowsARecordADerivationReachesIsRefusedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Deployed deployed = await DeployAsync();
        var administrator = AccessContext.Of(deployed.Administrator);

        await using ServiceProvider undeclared = Undeclared();
        await using AsyncServiceScope scope = undeclared.CreateAsyncScope();
        IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();

        Result<ResourceAccess> record = await gate.WhoCanAccessAsync(administrator, deployed.Note, cancellationToken);
        ResourceAccess organization = Answered(await gate.WhoCanAccessAsync(
            administrator,
            new ResourceReference(OrganizationWide, ResourceId.Parse(deployed.Deployment.Organization.Value.ToString())),
            cancellationToken));

        Assert.Equal(
            ErrorCodes.DerivationSourcesMissing,
            record.Match(_ => throw new InvalidOperationException("It was answered."), error => error.Code));
        Assert.Contains(organization.Grants, grant => grant.Id == deployed.Reading);
        Assert.DoesNotContain(organization.Grants, grant => grant.Id == deployed.Stored);
    }

    /// <summary>
    /// AUTHZ-DERIVE-005 AC6, LIB-HOST-001: the view evaluates a derivation in one
    /// statement in the host's context, over the relationship's rows and the ancestry of
    /// one context instance.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_005_AC6_TheViewEvaluatesADerivationInOneStatementAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Deployed deployed = await DeployAsync();

        await deployed.Deployment.ReviewAsync(deployed.Container, deployed.Reviewing, cancellationToken);

        using var traced = new TracedStatements();

        ResourceAccess access = Answered(await LookedUpAsync(deployed, deployed.Note, withRows: false));

        string evaluated = Assert.Single(
            traced.Texts,
            text => text.Contains("host.reviewers", StringComparison.Ordinal));

        Assert.Contains("identity.ancestry", evaluated, StringComparison.Ordinal);
        Assert.Contains(access.Grants, grant => grant.Kind == GrantKind.Derived);
    }

    /// <summary>
    /// AUTHZ-DERIVE-007: a derived grant whose role allows nothing confers nothing and is
    /// not reported, as a stored grant of such a role is not.
    /// </summary>
    /// <param name="withRows">Whether the caller hands the rows in.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUTHZ_DERIVE_007_ADerivedGrantWhoseRoleAllowsNothingIsNotReportedAsync(bool withRows)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Deployed deployed = await DeployAsync();

        await deployed.Deployment.ReviewAsync(deployed.Container, deployed.Reviewing, cancellationToken);
        await AllowingNothingAsync(Reviewer);

        try
        {
            ResourceAccess access = Answered(await LookedUpAsync(deployed, deployed.Note, withRows));

            Assert.False(access.Partial);
            Assert.DoesNotContain(access.Grants, grant => grant.Kind == GrantKind.Derived);
            Assert.Contains(access.Grants, grant => grant.Id == deployed.Stored);
        }
        finally
        {
            await deployed.Deployment.NamedRoleAsync(Reviewer, [HostPermissions.ReadNote], cancellationToken);
        }
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
    /// AUTHZ-DERIVE-007, 09 section 8: a type the host did not declare, and an
    /// organization named by what is not a UUID, are requests that cannot be read and are
    /// refused as malformed, naming the member.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_AnUndeclaredTypeIsRefusedAsMalformedAsync()
    {
        Deployed deployed = await DeployAsync();

        Result<ResourceAccess> undeclared = await LookedUpAsync(
            deployed,
            Reference(ResourceType.Parse("ledger")),
            withRows: true);
        Result<ResourceAccess> unreadable = await LookedUpAsync(
            deployed,
            new ResourceReference(OrganizationWide, ResourceId.Parse("company")),
            withRows: false);

        Assert.Equal(ErrorCodes.RequestMalformed, Refusal(undeclared));
        Assert.Equal("resourceType", Member(undeclared));
        Assert.Equal(ErrorCodes.RequestMalformed, Refusal(unreadable));
        Assert.Equal("resourceId", Member(unreadable));
    }

    /// <summary>
    /// AUTHZ-DERIVE-007 AC3, AUTHZ-CONCEAL-004: a record of a declared type that the
    /// registry does not hold is refused exactly as a registered record is refused to a
    /// caller without <c>grant:read</c>: the same code and details, under an identifier
    /// the refusal was recorded as, against no organization and the type
    /// <c>organization</c>, and counted towards the caller's denials.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_AnUnregisteredRecordIsRefusedAsTheGateRefusesAsync()
    {
        Deployed deployed = await DeployAsync();
        var caller = AccessContext.Of(deployed.Account);

        Error registered = Refused(await LookedUpAsync(caller, deployed.Note, withRows: true));
        Error unregistered = Refused(await LookedUpAsync(caller, Reference(Note), withRows: true));
        Error unregisteredWithoutRows = Refused(await LookedUpAsync(caller, Reference(Note), withRows: false));

        foreach (Error refusal in new[] { registered, unregistered, unregisteredWithoutRows })
        {
            Assert.Equal(ErrorCodes.Denied, refusal.Code);
            Assert.Equal(["correlation"], refusal.Details.Keys);
        }

        foreach (Error refusal in new[] { unregistered, unregisteredWithoutRows })
        {
            (Guid? organization, string type) = await RecordedAsync(Correlation(refusal));

            Assert.Null(organization);
            Assert.Equal(OrganizationWide.ToString(), type);
        }

        Assert.Equal(3, await DenialsAsync(deployed.Account));
    }

    /// <summary>
    /// AUTHZ-SCOPE-001, AUTHZ-DERIVE-007 AC3: a record the registry does not hold
    /// belongs to no organization, so no grant reaches it, and a caller holding
    /// <c>grant:read</c> across the organization is refused it as anyone is.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_AnUnregisteredRecordIsRefusedEvenToAHolderOfGrantReadAsync()
    {
        Deployed deployed = await DeployAsync();

        Result<ResourceAccess> held = await LookedUpAsync(deployed, deployed.Note, withRows: true);
        Result<ResourceAccess> unregistered = await LookedUpAsync(deployed, Reference(Note), withRows: true);

        Assert.Equal(deployed.Note, Answered(held).Resource);
        Assert.Equal(ErrorCodes.Denied, Refusal(unregistered));
    }

    private static ResourceAccess Answered(Result<ResourceAccess> outcome) =>
        outcome.Match(
            access => access,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private static ErrorCode Refusal(Result<ResourceAccess> outcome) =>
        outcome.Match(_ => throw new InvalidOperationException("It was answered."), error => error.Code);

    private static Error Refused(Result<ResourceAccess> outcome) =>
        outcome.Match(_ => throw new InvalidOperationException("It was answered."), error => error);

    private static AuditRecordId Correlation(Error refusal) =>
        new(refusal.Details["correlation"].GetGuid());

    private static string? Member(Result<ResourceAccess> outcome) =>
        outcome.Match(
            _ => throw new InvalidOperationException("It was answered."),
            error => error.Details["member"].GetString());

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    // The same deployment as a host that declared no relationship source composes it,
    // which the startup check refuses and a container built by hand does not.
    private ServiceProvider Undeclared()
    {
        var services = new ServiceCollection();

        services.AddSingleton<TimeProvider>(new FixedTime(Deployment.Noon));
        services.AddSingleton<ISecretSource>(HostFixture.Secrets(host.MaintenanceConnectionString));
        services.AddJanus(host.ConnectionString, HostFixture.Declaration(), ApplicationKind.Public);

        return HostFixture.Started(services.BuildServiceProvider());
    }

    // A role the deployment holds, emptied of what it allows.
    private async Task AllowingNothingAsync(RoleName role)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM identity.role_permissions WHERE role = @role;",
            new { role = role.ToString() },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private static FilterSources<HostDocument> Sources(HostContext reading) =>
        new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, reading.Consented, document => document.Id)
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

    // The trail itself, read without the gate: the organization a refusal was recorded
    // against and the type it names.
    private async Task<(Guid? Organization, string Type)> RecordedAsync(AuditRecordId correlation)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<(Guid?, string)>(new CommandDefinition(
            """
            SELECT organization, details ->> 'resourceType'
            FROM identity.audit_records
            WHERE id = @id AND action = @action;
            """,
            new { id = correlation.Value, action = "authz.access.denied" },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // The refusals recorded against one actor, which is what the denial spike counts.
    private async Task<int> DenialsAsync(SubjectId acting)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int
            FROM identity.audit_records
            WHERE action = @action AND acting_subject = @acting;
            """,
            new { action = "authz.access.denied", acting = acting.Value },
            cancellationToken: TestContext.Current.CancellationToken));
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
