using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Privacy.SubjectKeys;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// What a refusal's body carries over HTTP: the request's correlation identifier always,
/// and its refusal record's identifier in <c>details.correlation</c> only where the gate
/// recorded the refusal, which the explanation routes then resolve
/// (API-CONV-002, BFF-ERR-001, AUTHZ-CONCEAL-004, D-208).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
[Trait("kind", "integration")]
public sealed class RefusalRecordTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Document = ResourceType.Parse("document");

    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    private static readonly Assurance Reached = new(AssuranceLevel.Aal2, PhishingResistant: true);

    /// <summary>
    /// API-CONV-002 AC2, <c>10</c> section 1 <c>authz.denied</c> (D-208): a library route's
    /// refusal the gate records answers 403 <c>authz.denied</c> carrying the request's
    /// correlation identifier and, in <c>details.correlation</c> and nothing else, the
    /// identifier of the refusal record the trail holds.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task API_CONV_002_AC2_ARecordedRefusalCarriesItsRecordBesideTheRequestsIdentifierAsync()
    {
        Callers callers = await CallersAsync();

        await using ServedRequests served = await ServedRequests.StartMountedAsync(
            host,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        Answer refused = await AccessViewedAsync(served, callers);
        JsonElement body = refused.Json();
        Guid correlation = body.GetProperty("details").GetProperty("correlation").GetGuid();

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("correlationId").GetString()));
        Assert.NotEqual(correlation.ToString(), body.GetProperty("correlationId").GetString());
        Assert.Equal(["correlation"], body.GetProperty("details").EnumerateObject().Select(member => member.Name));
        Assert.Equal(1, await RecordedAsync(correlation, callers.Caller));
    }

    /// <summary>
    /// BFF-ERR-001 AC2, API-CONV-002 AC2, AUTHZ-CONCEAL-004 AC5 (D-208): a refusal the gate
    /// answers with a residual, a restriction or a step-up, and an <c>authz.denied</c> that
    /// refuses no permission asked of the gate, an identifier the trail holds no refusal
    /// for, each carry in their body the identifier the request is traced under and no
    /// <c>details.correlation</c>, and write no refusal record.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_ERR_001_AC2_AnUnrecordedRefusalCarriesTheRequestsIdentifierAndNoRecordAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Callers callers = await CallersAsync();
        string? traced = null;
        Permission asked = HostPermissions.Edit;

        await using ServedRequests served = await ServedRequests.StartMountedAsync(
            host,
            async context =>
            {
                traced = context.TraceIdentifier;

                await AskedAsync(context, asked, callers.Record);
            },
            cancellationToken);

        string cookie = await SignedInAsync(served, callers.Caller, Deployment.Noon - TimeSpan.FromDays(1));

        await callers.Deployment.RestrictAsync(callers.Caller, cancellationToken);

        Answer restricted = await served.SendAsync(ServedRequests.Browser, string.Empty, cookie);
        string? restrictedTrace = traced;

        await callers.Deployment.LiftAsync(callers.Caller, cancellationToken);

        asked = HostPermissions.Publish;

        Answer stepUp = await served.SendAsync(ServedRequests.Browser, string.Empty, cookie);
        string? stepUpTrace = traced;

        Answer unresolved = await served.SendAsync(
            "/admin/explanations/" + AuditRecordId.New(TimeProvider.System),
            string.Empty,
            await SignedInAsync(served, callers.Support, Deployment.Noon - TimeSpan.FromMinutes(1)));

        Assert.Equal(
            (StatusCodes.Status403Forbidden, ErrorCodes.Restricted.ToString(), restrictedTrace),
            (restricted.Status, restricted.Text("code"), restricted.Text("correlationId")));
        Assert.Equal(
            (ApiStatus.Of(ErrorCodes.StepUpRequired), ErrorCodes.StepUpRequired.ToString(), stepUpTrace),
            (stepUp.Status, stepUp.Text("code"), stepUp.Text("correlationId")));
        Assert.Equal(
            (StatusCodes.Status403Forbidden, ErrorCodes.Denied.ToString()),
            (unresolved.Status, unresolved.Text("code")));
        Assert.False(string.IsNullOrEmpty(unresolved.Text("correlationId")));

        foreach (Answer answered in new[] { restricted, stepUp, unresolved })
        {
            Assert.False(Carried(answered));
        }

        Assert.Equal(0, await DenialsAsync(callers.Caller));
        Assert.Equal(0, await DenialsAsync(callers.Support));
    }

    /// <summary>
    /// AUTHZ-CONCEAL-004 AC1, <c>09</c> <c>GET /account/explanations/{correlationId}</c>
    /// and <c>GET /admin/explanations/{correlationId}</c> (D-208): the identifier taken from
    /// <c>details.correlation</c> of a real refusal's body resolves through both routes,
    /// to the caller as their own refusal and to a holder of <c>audit:read</c> in the
    /// administrative organization, naming the permission refused and no grant.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_004_AC1_TheExplanationRoutesResolveTheIdentifierARefusalsBodyCarriedAsync()
    {
        Callers callers = await CallersAsync();

        await using ServedRequests served = await ServedRequests.StartMountedAsync(
            host,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        Answer refused = await AccessViewedAsync(served, callers);
        string correlation = refused.Json().GetProperty("details").GetProperty("correlation").GetString()
            ?? string.Empty;

        Answer own = await served.SendAsync(
            "/account/explanations/" + correlation,
            string.Empty,
            await SignedInAsync(served, callers.Caller, Deployment.Noon - TimeSpan.FromMinutes(1)));
        Answer supported = await served.SendAsync(
            "/admin/explanations/" + correlation,
            string.Empty,
            await SignedInAsync(served, callers.Support, Deployment.Noon - TimeSpan.FromMinutes(1)));

        foreach (Answer resolved in new[] { own, supported })
        {
            JsonElement explanation = resolved.Json();

            Assert.Equal(StatusCodes.Status200OK, resolved.Status);
            Assert.Equal("denied", explanation.GetProperty("outcome").GetString());
            Assert.Equal(Permissions.GrantRead.ToString(), explanation.GetProperty("permission").GetString());
            Assert.Equal(callers.Caller.Value, explanation.GetProperty("principal").GetProperty("acting").GetGuid());
            Assert.True(
                !explanation.TryGetProperty("grant", out JsonElement grant) || grant.ValueKind is JsonValueKind.Null);
        }
    }

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    private static FilterSources<HostDocument> Sources(HostContext reading) =>
        new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, reading.Consented, document => document.Id)
            .Relationship("reviewer", reading.Reviewers);

    // Whether a body carries a refusal record's identifier.
    private static bool Carried(Answer answered) =>
        answered.Json().TryGetProperty("details", out JsonElement details)
        && details.ValueKind is JsonValueKind.Object
        && details.TryGetProperty("correlation", out _);

    // The host's endpoint, written with the public surface alone: who is asking, the
    // gate asked with the access the request carries, and the library's answers.
    private async Task AskedAsync(HttpContext context, Permission permission, ResourceReference record)
    {
        await using HostContext reading = host.Context();

        IAccessGate gate = context.RequestServices.GetRequiredService<IAccessGate>();

        IResult answer = await context.RequestServices.GetRequiredService<IRequestAccess>().Current().Match(
            async held => Answers.Of(
                await gate.RequireAsync(held.Context, permission, record, Sources(reading), context.RequestAborted),
                Results.NoContent()),
            error => Task.FromResult(Answers.Refused(error)));

        await answer.ExecuteAsync(context);
    }

    // The view of who can access the caller's organization, which the caller holds no
    // grant:read for.
    private static async Task<Answer> AccessViewedAsync(ServedRequests served, Callers callers) =>
        await served.SendAsync(
            "/admin/access",
            "?resourceType=organization&resourceId=" + callers.Deployment.Organization.Value,
            await SignedInAsync(served, callers.Caller, Deployment.Noon - TimeSpan.FromMinutes(1)));

    // A session of the account's own that proved a phishing-resistant second factor at
    // the instant given, kept as the serving deployment's store keeps it, and the cookie
    // that carries it.
    private static async Task<string> SignedInAsync(ServedRequests served, SubjectId account, DateTimeOffset proved)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = served.Services.CreateAsyncScope();

        IServiceProvider services = scope.ServiceProvider;
        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();
        OpaqueToken secret;

        using (var randomness = RandomNumberGenerator.Create())
        {
            secret = OpaqueToken.Draw(randomness);
        }

        var session = Session.Begin(
            SessionId.New(TimeProvider.System),
            account,
            Reached,
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            proved,
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        await work.BeginAsync(cancellationToken);

        // A session is kept under its person's key, which an account written directly
        // does not have until its first session asks for it.
        ISubjectKeyStore keys = services.GetRequiredService<ISubjectKeyStore>();

        if (await keys.FindBySubjectAsync(account, cancellationToken) is null)
        {
            await keys.CreateAsync(account, cancellationToken);
        }

        await services.GetRequiredService<ISessionStore>().AddAsync(
            session,
            secret.Fingerprint(),
            RandomNumberGenerator.GetBytes(32),
            cancellationToken);
        await work.CommitAsync(cancellationToken);

        return BrowserCookies.Session + "=" + secret.Value;
    }

    // A caller granted the editing and the publishing of a record of their organization,
    // and a support role holding audit:read in the administrative organization.
    private async Task<Callers> CallersAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName editing = await deployment.BeginAsync(
            [HostPermissions.Read, HostPermissions.Edit, HostPermissions.Publish],
            cancellationToken);
        SubjectId caller = await deployment.AccountAsync(cancellationToken);
        SubjectId support = await deployment.AccountAsync(cancellationToken);
        ResourceReference workspace = Reference(Workspace);
        ResourceReference record = Reference(Document);

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, workspace, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(caller), editing, workspace, false, null, null, cancellationToken);

        RoleName supporting = await deployment.RoleAsync([Permissions.AuditRead], cancellationToken);
        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);

        // IDN-LIFE-009a, D-166: the support role confers there on a member only.
        await deployment.MemberAsync(support, administrative, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(support), supporting, null, false, null, administrative, cancellationToken);

        return new Callers(deployment, caller, support, record);
    }

    // The trail itself, read without the gate: the refusal records the identifier names
    // with the account given as acting.
    private async Task<int> RecordedAsync(Guid correlation, SubjectId acting)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM identity.audit_records
            WHERE id = @id AND action = 'authz.access.denied' AND acting_subject = @acting;
            """,
            new { id = correlation, acting = acting.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // The refusals recorded naming the account as acting, which is what its denial
    // alert counts (AUTHZ-CONCEAL-004, OPS-ALERT-001).
    private async Task<int> DenialsAsync(SubjectId account)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM identity.audit_records
            WHERE action = 'authz.access.denied' AND acting_subject = @account;
            """,
            new { account = account.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // One case's rows: the deployment, the caller and the support role, and the record
    // the caller's grant reaches.
    private sealed record Callers(
        Deployment Deployment,
        SubjectId Caller,
        SubjectId Support,
        ResourceReference Record);
}
