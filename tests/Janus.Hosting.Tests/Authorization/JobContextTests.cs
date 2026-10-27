using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// What a host's declared job is refused beside the check, the filter and the fragment:
/// the capability page and the explanation, which serve a person, and the view of who
/// can access a record, which reports grants a job never holds (IDN-PRIN-001, D-207).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
[Trait("kind", "integration")]
public sealed class JobContextTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Document = ResourceType.Parse("document");
    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    /// <summary>
    /// BFF-CAP-002 AC5, AUTHZ-GATE-005, IDN-PRIN-001 AC6 (D-207, D-211): the capability
    /// page asked with a declared job's context, for a permission its declaration names on
    /// a record of its organization, is refused <c>authz.denied</c> through both of its
    /// paths, and so is the page of its organization itself, in background work and while
    /// a request is served.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_CAP_002_AC5_CapabilitiesGivenAJobsContextAreRefusedInARequestAndOutsideOneAsync()
    {
        Written written = await WriteAsync();
        IReadOnlyList<Error?>? inRequest = null;

        await using (ServedRequests served = await ServedRequests.StartAsync(
            host,
            async context => inRequest = await CapabilitiesAsync(context.RequestServices, written),
            TestContext.Current.CancellationToken))
        {
            _ = await served.SendAsync(ServedRequests.Browser);
        }

        await using AsyncServiceScope background = host.Services.CreateAsyncScope();

        Assert.Equal<ErrorCode?>(
            [ErrorCodes.Denied, ErrorCodes.Denied, ErrorCodes.Denied],
            inRequest?.Select(refusal => refusal?.Code));
        Assert.Equal<ErrorCode?>(
            [ErrorCodes.Denied, ErrorCodes.Denied, ErrorCodes.Denied],
            (await CapabilitiesAsync(background.ServiceProvider, written)).Select(refusal => refusal?.Code));
    }

    /// <summary>
    /// AUTHZ-GATE-004, IDN-PRIN-001 AC6, OPS-OBS-001 (D-207): the explanation asked with a
    /// declared job's context, of a record of its organization on a type that discloses,
    /// is refused <c>authz.denied</c> through both of its paths, in background work and
    /// while a request is served.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_PRIN_001_AC6_TheExplanationGivenAJobsContextIsRefusedAsync()
    {
        Written written = await WriteAsync();
        IReadOnlyList<Error?>? inRequest = null;

        await using (ServedRequests served = await ServedRequests.StartAsync(
            host,
            async context => inRequest = await ExplainedAsync(context.RequestServices, written),
            TestContext.Current.CancellationToken))
        {
            _ = await served.SendAsync(ServedRequests.Browser);
        }

        await using AsyncServiceScope background = host.Services.CreateAsyncScope();

        Assert.Equal<ErrorCode?>(
            [ErrorCodes.Denied, ErrorCodes.Denied],
            inRequest?.Select(refusal => refusal?.Code));
        Assert.Equal<ErrorCode?>(
            [ErrorCodes.Denied, ErrorCodes.Denied],
            (await ExplainedAsync(background.ServiceProvider, written)).Select(refusal => refusal?.Code));
    }

    /// <summary>
    /// AUTHZ-DERIVE-007, IDN-PRIN-001 AC6 (D-207): the view of who can access a record
    /// asks <c>grant:read</c>, which a job's declaration never names, so a declared job's
    /// context is refused it; and the view a person reads lists the grants alone, the
    /// declared job whose declaration reaches the record holding none.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_AJobIsRefusedTheViewAndIsNotListedByItAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Written written = await WriteAsync();

        RoleName viewing = await written.Deployment.RoleAsync([Permissions.GrantRead], cancellationToken);
        SubjectId viewer = await written.Deployment.AccountAsync(cancellationToken);
        GrantId granted = await written.Deployment.GrantAsync(
            GrantSubject.Of(viewer), viewing, null, false, null, null, cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();
        IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();

        Result<ResourceAccess> refused = await gate.WhoCanAccessAsync(
            written.Job, written.Record, Sources(reading), cancellationToken);
        ResourceAccess viewed = (await gate.WhoCanAccessAsync(
                AccessContext.Of(viewer), written.Record, Sources(reading), cancellationToken))
            .Match(access => access, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(
            ErrorCodes.Denied,
            refused.Match(_ => throw new InvalidOperationException("It was answered."), error => error.Code));

        ExplainedGrant listed = Assert.Single(viewed.Grants);

        Assert.Equal(granted, listed.Id);
        Assert.Equal(viewer.Value, listed.SubjectId);
    }

    /// <summary>
    /// AUTHZ-CONCEAL-004 AC4, IDN-PRIN-001 AC6 (D-208): a declared job's refusal of a
    /// permission its declaration does not name, asked inside a unit of work the job then
    /// rolls back, carries its record's identifier, and the record stands, naming the
    /// job's principal and stated reason, so it counts toward the job's denial alert.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_004_AC4_AJobsRefusalInsideWorkItRollsBackStandsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Written written = await WriteAsync();
        Error refused;

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            await using HostContext reading = host.Context();

            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await work.BeginAsync(cancellationToken);

            refused = (await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                    .RequireAsync(written.Job, HostPermissions.Retain, written.Record, Sources(reading), cancellationToken))
                .Match(() => throw new InvalidOperationException("The permission was admitted."), error => error);

            await work.RollbackAsync();
        }

        Assert.Equal(ErrorCodes.Denied, refused.Code);
        Assert.Equal(
            new Recorded("authz.access.denied", HostFixture.Digest, "the nightly digest"),
            await RecordedAsync(refused.Details["correlation"].GetGuid()));
    }

    /// <summary>
    /// AUTHZ-CONCEAL-004, IDN-PRIN-001 AC6, CONV-DESIGN-002 (D-208): the refusals of a
    /// declared job that refuse no permission asked of the gate, the capability page and
    /// the explanation through both of their paths, and a library operation's own refusal
    /// at its gate step, are <c>authz.denied</c> carrying no refusal record's identifier,
    /// and write no refusal record naming the job, so they count nothing toward its
    /// denial alert.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_004_AJobsRefusalsAskingNoPermissionOfTheGateWriteNoRecordAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Written written = await WriteAsync();
        RoleName reading = await written.Deployment.RoleAsync([HostPermissions.Read], cancellationToken);
        SubjectId account = await written.Deployment.AccountAsync(cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Result<GrantId> granted = await scope.ServiceProvider.GetRequiredService<IGrants>().GrantAsync(
            written.Job,
            SessionId.New(TimeProvider.System),
            new GrantRequest(
                GrantSubject.Of(account),
                reading,
                new ResourceReference(
                    ResourceType.Parse("organization"),
                    ResourceId.Parse(written.Deployment.Organization.ToString())),
                Deny: false,
                ExpiresAt: null,
                "The reason the grant was asked for."),
            cancellationToken);

        Error?[] refusals =
        [
            .. await CapabilitiesAsync(scope.ServiceProvider, written),
            .. await ExplainedAsync(scope.ServiceProvider, written),
            Refusal(granted),
        ];

        Assert.All(refusals, refusal =>
        {
            Assert.Equal(ErrorCodes.Denied, refusal?.Code);
            Assert.False(refusal?.Details.ContainsKey("correlation"));
        });
        Assert.Equal(0, await JobDenialsAsync(written.Deployment.Organization));
    }

    // The refusals recorded naming the declared job in the organization, which is what
    // the job's denial alert counts (AUTHZ-CONCEAL-004, OPS-ALERT-001).
    private async Task<int> JobDenialsAsync(OrganizationId organization)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM identity.audit_records
            WHERE action = 'authz.access.denied' AND principal = @job AND organization = @organization
            """,
            new { job = HostFixture.Digest, organization = organization.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // The refusal record the identifier names: its action, and the principal and reason
    // it records.
    private async Task<Recorded> RecordedAsync(Guid correlation)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<Recorded>(new CommandDefinition(
            """
            SELECT action AS "Action", principal AS "Principal", principal_reason AS "Reason"
            FROM identity.audit_records WHERE id = @correlation
            """,
            new { correlation },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private static FilterSources<HostDocument> Sources(HostContext reading) =>
        new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, reading.Consented, document => document.Id)
            .Relationship("reviewer", reading.Reviewers);

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    // The page of the record asked under the job's context, through the path over the
    // stored grants and the path over the host's rows, and the page of its organization.
    private async Task<IReadOnlyList<Error?>> CapabilitiesAsync(IServiceProvider asking, Written written)
    {
        IAccessGate gate = asking.GetRequiredService<IAccessGate>();

        await using HostContext reading = host.Context();

        Result<IReadOnlyList<Capability>> stored = await gate.CapabilitiesAsync(
            written.Job,
            Document,
            [written.Record.Id],
            [HostPermissions.Read],
            CancellationToken.None);
        Result<IReadOnlyList<Capability>> sourced = await gate.CapabilitiesAsync(
            written.Job,
            Document,
            [written.Record.Id],
            [HostPermissions.Read],
            Sources(reading),
            CancellationToken.None);
        Result<IReadOnlyList<Capability>> organization = await gate.CapabilitiesAsync(
            written.Job,
            ResourceType.Parse("organization"),
            [ResourceId.Parse(written.Deployment.Organization.ToString())],
            [HostPermissions.Read],
            CancellationToken.None);

        return [Refusal(stored), Refusal(sourced), Refusal(organization)];
    }

    // The explanation of the job's reading of the record, through the path over the
    // stored grants and the path over the host's rows.
    private async Task<IReadOnlyList<Error?>> ExplainedAsync(IServiceProvider asking, Written written)
    {
        IAccessGate gate = asking.GetRequiredService<IAccessGate>();

        await using HostContext reading = host.Context();

        Result<AccessExplanation> stored = await gate.ExplainAsync(
            written.Job,
            HostPermissions.ReadNote,
            written.Note,
            CancellationToken.None);
        Result<AccessExplanation> sourced = await gate.ExplainAsync(
            written.Job,
            HostPermissions.ReadNote,
            written.Note,
            Sources(reading),
            CancellationToken.None);

        return [Refusal(stored), Refusal(sourced)];
    }

    private static Error? Refusal<T>(Result<T> outcome) =>
        outcome.Match<Error?>(_ => null, error => error);

    // A workspace of a fresh organization holding a document and a note, and the context
    // of the job the host declares, acting for that organization.
    private async Task<Written> WriteAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        _ = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);

        ResourceReference workspace = Reference(Workspace);
        ResourceReference record = Reference(Document);
        ResourceReference note = Reference(ResourceType.Parse("note"));

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, workspace, cancellationToken);
        await deployment.RegisterAsync(note, workspace, cancellationToken);

        var job = AccessContext.Of(SystemPrincipal.ForOrganization(
            HostFixture.Digest,
            "the nightly digest",
            deployment.Organization));

        return new Written(deployment, job, record, note);
    }

    private sealed record Recorded(string Action, string Principal, string Reason);

    private sealed record Written(
        Deployment Deployment,
        AccessContext Job,
        ResourceReference Record,
        ResourceReference Note);
}
