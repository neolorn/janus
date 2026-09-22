using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// What the gate says about a decision, and what a refusal discloses
/// (AUTHZ-GATE-004, AUTHZ-CONCEAL-001 to AUTHZ-CONCEAL-005, AUTHZ-IMP-001).
/// </summary>
[Trait("kind", "integration")]
public sealed class ExplanationTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Document = ResourceType.Parse("document");
    private static readonly ResourceType Note = ResourceType.Parse("note");
    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    // The role the host's declaration says a reviewer holds on what they review.
    private static readonly RoleName Reviewer = RoleName.Parse("reviewer");

    /// <summary>
    /// AUTHZ-GATE-004 AC1: a refusal names what was asked for and says that no grant
    /// matched.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GATE_004_AC1_ADenialNamesThePermissionAndStatesNoGrantMatchedAsync()
    {
        Deployed deployed = await DeployAsync(granted: false);

        AccessExplanation explanation = await ExplainedAsync(deployed, deployed.Note);

        Assert.Equal(AccessOutcome.Denied, explanation.Outcome);
        Assert.Equal(HostPermissions.ReadNote, explanation.Permission);
        Assert.Equal(deployed.Account, explanation.Principal.Acting);
        Assert.Equal(deployed.Account, explanation.Principal.Effective);
        Assert.Null(explanation.Grant);
    }

    /// <summary>
    /// AUTHZ-GATE-004 AC2: an approval names the grant that decided and the container
    /// the grant sits on.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GATE_004_AC2_AnApprovalNamesTheGrantAndWhatItWasInheritedFromAsync()
    {
        Deployed deployed = await DeployAsync(granted: true);

        AccessExplanation explanation = await ExplainedAsync(deployed, deployed.Note);

        Assert.Equal(AccessOutcome.Allowed, explanation.Outcome);

        ExplainedGrant grant = Assert.IsType<ExplainedGrant>(explanation.Grant);

        Assert.Equal(deployed.Grant, grant.Id);
        Assert.Equal(GrantKind.Stored, grant.Kind);
        Assert.Equal(SubjectType.User, grant.SubjectType);
        Assert.Equal(deployed.Account.Value, grant.SubjectId);
        Assert.Equal(deployed.Role, grant.Role);
        Assert.False(grant.Deny);
        Assert.Equal(deployed.Container, grant.InheritedFrom);
    }

    /// <summary>
    /// AUTHZ-GATE-004 AC2, AUTHZ-DERIVE-001, AUTHZ-DERIVE-005 (D-162): an explanation
    /// asked with the rows the derivation is evaluated over names the grant the fact
    /// produced. It carries no identifier, because no row holds it; it says it is
    /// derived; and it names the role the derivation confers and the container the
    /// relationship is declared on.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GATE_004_AC2_AnApprovalNamesTheGrantAFactProducedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Deployed deployed = await DeployAsync(granted: false);

        await deployed.Deployment.NamedRoleAsync(
            Reviewer,
            [HostPermissions.ReadNote],
            cancellationToken);

        await deployed.Deployment.ReviewAsync(deployed.Container, deployed.Account, cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();

        AccessExplanation explanation = Explained(
            await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .ExplainAsync(
                    AccessContext.Of(deployed.Account),
                    HostPermissions.ReadNote,
                    deployed.Note,
                    Sources(reading),
                    cancellationToken));

        Assert.Equal(AccessOutcome.Allowed, explanation.Outcome);

        ExplainedGrant grant = Assert.IsType<ExplainedGrant>(explanation.Grant);

        Assert.Null(grant.Id);
        Assert.Equal(GrantKind.Derived, grant.Kind);
        Assert.Equal(SubjectType.User, grant.SubjectType);
        Assert.Equal(deployed.Account.Value, grant.SubjectId);
        Assert.Equal(Reviewer, grant.Role);
        Assert.False(grant.Deny);
        Assert.Equal(deployed.Container, grant.InheritedFrom);
    }

    /// <summary>
    /// AUTHZ-GATE-004 AC1, AUTHZ-DERIVE-002 AC1 (D-162): where no fact of the host's
    /// admits the record either, the explanation asked with the rows says that no grant
    /// matched rather than refusing the operation.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GATE_004_AC1_ADenialWithTheHostsRowsStatesNoGrantMatchedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Deployed deployed = await DeployAsync(granted: false);

        await deployed.Deployment.NamedRoleAsync(
            Reviewer,
            [HostPermissions.ReadNote],
            cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();

        AccessExplanation explanation = Explained(
            await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .ExplainAsync(
                    AccessContext.Of(deployed.Account),
                    HostPermissions.ReadNote,
                    deployed.Note,
                    Sources(reading),
                    cancellationToken));

        Assert.Equal(AccessOutcome.Denied, explanation.Outcome);
        Assert.Null(explanation.Grant);
    }

    /// <summary>
    /// AUTHZ-GATE-004 AC3: a type whose refusal answers as a record that does not exist
    /// has no self-service explanation, because saying that no grant matched says that
    /// the record is there.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GATE_004_AC3_OnlyATypeThatDisclosesExplainsToTheCallerAsync()
    {
        Deployed deployed = await DeployAsync(granted: false);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();
        IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();

        Result<AccessExplanation> concealed = await gate.ExplainAsync(
            AccessContext.Of(deployed.Account),
            HostPermissions.Read,
            deployed.Record,
            Sources(reading),
            TestContext.Current.CancellationToken);

        Result<AccessExplanation> disclosed = await gate.ExplainAsync(
            AccessContext.Of(deployed.Account),
            HostPermissions.ReadNote,
            deployed.Note,
            Sources(reading),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.Denied, Refusal(concealed).Code);
        Assert.Empty(Refusal(concealed).Details);
        Assert.Equal(AccessOutcome.Denied, Explained(disclosed).Outcome);
    }

    /// <summary>
    /// AUTHZ-GATE-004 AC4: the identifier a concealed refusal carries resolves for a
    /// role holding <c>audit:read</c>, and for nobody else.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GATE_004_AC4_ACorrelationIdentifierResolvesOnlyForASupportRoleAsync()
    {
        Deployed deployed = await DeployAsync(granted: false);

        AuditRecordId correlation = await RefusedAsync(deployed, deployed.Record, HostPermissions.Read);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();

        Result<AccessExplanation> withoutTheRole = await gate.ResolveAsync(
            AccessContext.Of(deployed.Account),
            deployed.Deployment.Organization,
            correlation,
            TestContext.Current.CancellationToken);

        Result<AccessExplanation> asSupport = await gate.ResolveAsync(
            AccessContext.Of(deployed.Support),
            deployed.Deployment.Organization,
            correlation,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.Denied, Refusal(withoutTheRole).Code);
        Assert.Equal(AccessOutcome.Denied, Explained(asSupport).Outcome);
    }

    /// <summary>
    /// AUTHZ-CONCEAL-004 AC1: the identifier is the audit record's own, so it resolves
    /// to the entry the refusal wrote, naming the permission that was asked for and the
    /// principal who asked.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_004_AC1_TheIdentifierResolvesToTheEntryItWroteAsync()
    {
        Deployed deployed = await DeployAsync(granted: false);

        AuditRecordId correlation = await RefusedAsync(deployed, deployed.Record, HostPermissions.Read);

        AccessExplanation resolved = await ResolvedAsync(deployed, correlation);

        Assert.Equal(1, await RecordedAsync(correlation));
        Assert.Equal(AccessOutcome.Denied, resolved.Outcome);
        Assert.Equal(HostPermissions.Read, resolved.Permission);
        Assert.Equal(deployed.Account, resolved.Principal.Acting);
        Assert.Equal(deployed.Account, resolved.Principal.Effective);
        Assert.Null(resolved.Grant);
    }

    /// <summary>
    /// AUTHZ-CONCEAL-004 AC2: the identifier is new at every refusal and derived from
    /// nothing the caller named, so holding two of them says nothing about which record
    /// was there.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_004_AC2_TheIdentifierSaysNothingAboutTheRecordAsync()
    {
        Deployed deployed = await DeployAsync(granted: false);

        ResourceReference absent = Reference(Document);

        AuditRecordId present = await RefusedAsync(deployed, deployed.Record, HostPermissions.Read);
        AuditRecordId missing = await RefusedAsync(deployed, absent, HostPermissions.Read);
        AuditRecordId again = await RefusedAsync(deployed, deployed.Record, HostPermissions.Read);

        Assert.NotEqual(present, missing);
        Assert.NotEqual(present, again);

        foreach (AuditRecordId identifier in new[] { present, missing, again })
        {
            Assert.DoesNotContain(
                deployed.Record.Id.ToString(),
                identifier.Value.ToString(),
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                absent.Id.ToString(),
                identifier.Value.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// AUTHZ-IMP-001 AC2: the record the gate writes carries both identities, whether
    /// or not anybody is acting for anybody.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_IMP_001_AC2_BothIdentitiesAreWrittenToTheAuditRecordAsync()
    {
        Deployed deployed = await DeployAsync(granted: false);

        AuditRecordId correlation = await RefusedAsync(deployed, deployed.Record, HostPermissions.Read);

        Assert.Equal(
            new Identified(deployed.Account.Value, deployed.Account.Value),
            await IdentifiedAsync(correlation));
    }

    /// <summary>
    /// AUTHZ-CONCEAL-002 AC1: a refusal about a record that is there and a refusal
    /// about one that is not are the same answer.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_002_AC1_ARefusalIsTheSameAnswerWhetherTheRecordIsThereAsync()
    {
        Deployed deployed = await DeployAsync(granted: false);

        Error present = await ErrorAsync(deployed, deployed.Record, HostPermissions.Read);
        Error absent = await ErrorAsync(
            deployed,
            new ResourceReference(Document, ResourceId.Parse(Guid.NewGuid().ToString())),
            HostPermissions.Read);

        Assert.Equal(present.Code, absent.Code);
        Assert.Equal(present.Details.Keys.Order(StringComparer.Ordinal), absent.Details.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(JsonValueKind.String, present.Details["correlation"].ValueKind);
        Assert.Equal(JsonValueKind.String, absent.Details["correlation"].ValueKind);
    }

    /// <summary>
    /// AUTHZ-CONCEAL-003 AC1: two records of one type are refused the same way, so
    /// nothing varies per record.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_003_AC1_TwoRecordsOfOneTypeProduceTheSameRefusalAsync()
    {
        Deployed deployed = await DeployAsync(granted: false);

        Error one = await ErrorAsync(deployed, deployed.Record, HostPermissions.Read);
        Error other = await ErrorAsync(deployed, deployed.Sibling, HostPermissions.Read);

        Assert.Equal(one.Code, other.Code);
        Assert.Equal(one.Details.Keys.Order(StringComparer.Ordinal), other.Details.Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AUTHZ-CONCEAL-005 AC1: a permission that is not about one record is refused as a
    /// permission the caller does not hold, with no record to conceal.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_005_AC1_APermissionTiedToNoRecordIsRefusedAsForbiddenAsync()
    {
        Deployed deployed = await DeployAsync(granted: false);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();

        Result withoutTheRole = await gate.RequireAsync(
            AccessContext.Of(deployed.Account),
            Permissions.AuditRead,
            deployed.Deployment.Organization,
            TestContext.Current.CancellationToken);

        Result asSupport = await gate.RequireAsync(
            AccessContext.Of(deployed.Support),
            Permissions.AuditRead,
            deployed.Deployment.Organization,
            TestContext.Current.CancellationToken);

        Error refusal = withoutTheRole.Match(
            () => throw new InvalidOperationException("The permission was not held."),
            error => error);

        Assert.Equal(ErrorCodes.Denied, refusal.Code);
        Assert.True(asSupport.Match(() => true, _ => false));
    }

    /// <summary>
    /// AUTHZ-GRANT-001 AC2: a grant naming the whole organization confers the
    /// permission there, and one naming a record confers nothing over the organization
    /// the record sits in.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_001_AC2_AGrantOnARecordConfersNothingOverTheOrganizationAsync()
    {
        Deployed deployed = await DeployAsync(granted: true);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Result outcome = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .RequireAsync(
                AccessContext.Of(deployed.Account),
                HostPermissions.ReadNote,
                deployed.Deployment.Organization,
                TestContext.Current.CancellationToken);

        Assert.False(outcome.Match(() => true, _ => false));
    }

    private static Error Refusal<TValue>(Result<TValue> outcome) => outcome.Match(
        _ => throw new InvalidOperationException("The operation succeeded."),
        error => error);

    private static AccessExplanation Explained(Result<AccessExplanation> outcome) => outcome.Match(
        explanation => explanation,
        error => throw new InvalidOperationException(error.Code.ToString()));

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    private static FilterSources<HostDocument> Sources(HostContext reading) =>
        new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, document => document.Id)
            .Relationship("reviewer", reading.Reviewers);

    // The type a note sits in declares a derivation, so the explanation is asked with
    // the rows that derivation is evaluated over (AUTHZ-DERIVE-001, D-162).
    private async Task<AccessExplanation> ExplainedAsync(Deployed deployed, ResourceReference resource)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();

        return Explained(await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .ExplainAsync(
                AccessContext.Of(deployed.Account),
                HostPermissions.ReadNote,
                resource,
                Sources(reading),
                TestContext.Current.CancellationToken));
    }

    private async Task<Error> ErrorAsync(
        Deployed deployed,
        ResourceReference resource,
        Permission permission)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Result outcome = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .RequireAsync(
                AccessContext.Of(deployed.Account),
                permission,
                resource,
                TestContext.Current.CancellationToken);

        return outcome.Match(
            () => throw new InvalidOperationException("The permission was not refused."),
            error => error);
    }

    private async Task<AccessExplanation> ResolvedAsync(Deployed deployed, AuditRecordId correlation)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        return Explained(await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .ResolveAsync(
                AccessContext.Of(deployed.Support),
                deployed.Deployment.Organization,
                correlation,
                TestContext.Current.CancellationToken));
    }

    // The trail itself, read without the gate: an identifier that resolves through the
    // gate alone would prove only that the gate remembers it.
    private async Task<int> RecordedAsync(AuditRecordId correlation)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM janus.audit_records WHERE id = @id AND action = @action;",
            new { id = correlation.Value, action = "authz.access.denied" },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private async Task<Identified> IdentifiedAsync(AuditRecordId correlation)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<Identified>(new CommandDefinition(
            """
            SELECT acting_subject AS "Acting", effective_subject AS "Effective"
            FROM janus.audit_records
            WHERE id = @id;
            """,
            new { id = correlation.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private async Task<AuditRecordId> RefusedAsync(
        Deployed deployed,
        ResourceReference resource,
        Permission permission)
    {
        Error refusal = await ErrorAsync(deployed, resource, permission);

        return new AuditRecordId(refusal.Details["correlation"].GetGuid());
    }

    // The two identity columns of one record, read as the row holds them.
    private sealed record Identified(Guid Acting, Guid Effective);

    private async Task<Deployed> DeployAsync(bool granted)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync(
            [HostPermissions.Read, HostPermissions.ReadNote],
            cancellationToken);

        SubjectId account = await deployment.AccountAsync(cancellationToken);
        SubjectId support = await deployment.AccountAsync(cancellationToken);

        ResourceReference container = Reference(Workspace);
        ResourceReference record = Reference(Document);
        ResourceReference sibling = Reference(Document);
        ResourceReference note = Reference(Note);

        await deployment.RegisterAsync(container, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, container, cancellationToken);
        await deployment.RegisterAsync(sibling, container, cancellationToken);
        await deployment.RegisterAsync(note, container, cancellationToken);

        RoleName supporting = await deployment.RoleAsync([Permissions.AuditRead], cancellationToken);

        await deployment.GrantAsync(
            GrantSubject.Of(support), supporting, null, false, null, null, cancellationToken);

        GrantId grant = default;

        if (granted)
        {
            grant = await deployment.GrantAsync(
                GrantSubject.Of(account), role, container, false, null, null, cancellationToken);
        }

        return new Deployed(deployment, account, support, role, grant, container, record, sibling, note);
    }

    // One case's rows: the organization, the two accounts, the role the grant confers,
    // the container everything sits in, and the records of each kind.
    private sealed record Deployed(
        Deployment Deployment,
        SubjectId Account,
        SubjectId Support,
        RoleName Role,
        GrantId Grant,
        ResourceReference Container,
        ResourceReference Record,
        ResourceReference Sibling,
        ResourceReference Note);
}
