using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Privacy.SubjectKeys;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// What a host's own endpoint is answered when it asks the gate with the access the
/// request carries: the session judged as the library's own endpoints have it judged, a
/// step-up refusal in the library's wire format, and a concealed refusal answered by
/// stage 11 whatever the endpoint wrote (BFF-HOST-001, BFF-HOST-002, BFF-HOST-003).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
[Trait("kind", "integration")]
public sealed class RequestAccessGateTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const string Traced = "0HN7GATE:00000001";

    private static readonly ResourceType Document = ResourceType.Parse("document");

    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    private static readonly Assurance Reached = new(AssuranceLevel.Aal2, PhishingResistant: true);

    /// <summary>
    /// BFF-HOST-001 AC6 (D-205): a gate asked from a host endpoint judges the session as it
    /// judges for the library's own endpoints. A session that reached <c>aal2</c> a minute
    /// ago meets the gate bound to the action; the same proof, downgraded since, does not
    /// meet it on the strength of the instant before the downgrade, and the host endpoint
    /// is answered exactly what the library's own judgement of that session answers.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_HOST_001_AC6_ASessionDowngradedSinceAal2DoesNotMeetTheGateFromAHostEndpointAsync()
    {
        (SubjectId caller, ResourceReference record) = await PublishingAsync();
        DateTimeOffset proved = Deployment.Noon - TimeSpan.FromMinutes(1);

        Answered met = await AskedAsync(Begun(caller, proved), HostPermissions.Publish, record);

        Session lowered = Begun(caller, proved);

        lowered.Downgrade(Deployment.Noon - TimeSpan.FromSeconds(30));

        Answered downgraded = await AskedAsync(lowered, HostPermissions.Publish, record);
        Error librarys = Assert.IsType<Error>(await LibrarysAsync(lowered, HostPermissions.Publish, record));

        Assert.Equal(StatusCodes.Status204NoContent, met.Status);
        Assert.Equal(ApiStatus.Of(ErrorCodes.StepUpRequired), downgraded.Status);
        Assert.Equal(ErrorCodes.StepUpRequired, librarys.Code);
        Assert.Equal(librarys.Code.ToString(), downgraded.Body.GetProperty("code").GetString());
        Assert.True(JsonElement.DeepEquals(Written(librarys), downgraded.Body.GetProperty("details")));
    }

    /// <summary>
    /// BFF-HOST-002 AC3: a host endpoint asking the gate for a permission bound to a gate
    /// the request's session does not meet, on a record its grants admit, is answered
    /// <c>auth.stepup.required</c> with the gate's <c>details</c>.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_HOST_002_AC3_AnUnmetGateIsAnsweredWithItsDetailsAsync()
    {
        (SubjectId caller, ResourceReference record) = await PublishingAsync();
        Session aged = Begun(caller, Deployment.Noon - TimeSpan.FromDays(1));

        Answered answered = await AskedAsync(aged, HostPermissions.Publish, record);
        Error gates = Assert.IsType<Error>(await LibrarysAsync(aged, HostPermissions.Publish, record));
        JsonElement details = answered.Body.GetProperty("details");

        Assert.Equal(ApiStatus.Of(ErrorCodes.StepUpRequired), answered.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), answered.Body.GetProperty("code").GetString());
        Assert.Equal(Traced, answered.Body.GetProperty("correlationId").GetString());
        Assert.Equal<string>(
            ["options", "outcome", "pendingUntil", "required"],
            details.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal));
        Assert.True(JsonElement.DeepEquals(Written(gates), details));
    }

    /// <summary>
    /// BFF-HOST-003 AC2: a gate refusal of a record of a concealing type in a host endpoint
    /// is answered as the record's absence, whatever the endpoint wrote: the header it set
    /// and the refusal it answered through <c>Answers</c> leave as the answer to a record
    /// nothing registered.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task BFF_HOST_003_AC2_AConcealedRefusalIsAnsweredAsTheAbsenceWhateverTheEndpointWroteAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        _ = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);

        SubjectId caller = await deployment.AccountAsync(cancellationToken);
        ResourceReference workspace = Reference(Workspace);
        ResourceReference held = Reference(Document);

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(held, workspace, cancellationToken);

        DateTimeOffset now = Deployment.Noon - TimeSpan.FromMinutes(1);
        Answered present = await AskedAsync(Begun(caller, now), HostPermissions.Read, held, writes: true);
        Answered absent = await AskedAsync(Begun(caller, now), HostPermissions.Read, Reference(Document), writes: false);

        Assert.Equal(StatusCodes.Status404NotFound, present.Status);
        Assert.Equal(absent.Status, present.Status);
        Assert.Equal(absent.Headers, present.Headers);
        Assert.DoesNotContain("X-Record-Owner", present.Headers, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.ResourceNotFound.ToString(), present.Body.GetProperty("code").GetString());
        Assert.Equal(Traced, present.Body.GetProperty("correlationId").GetString());
        Assert.Equal(
            ["correlation"],
            present.Body.GetProperty("details").EnumerateObject().Select(member => member.Name));
        Assert.Equal(
            absent.Body.GetProperty("details").EnumerateObject().Select(member => member.Name),
            present.Body.GetProperty("details").EnumerateObject().Select(member => member.Name));
    }

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    private static FilterSources<HostDocument> Sources(HostContext reading) =>
        new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, reading.Consented, document => document.Id)
            .Relationship("reviewer", reading.Reviewers);

    // A session of the caller's own that proved a phishing-resistant second factor at the
    // instant given.
    private static Session Begun(SubjectId caller, DateTimeOffset proved) =>
        Session.Begin(
            SessionId.New(TimeProvider.System),
            caller,
            Reached,
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            proved,
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

    // The details of a refusal as the wire carries them.
    private static JsonElement Written(Error refusal) => JsonSerializer.SerializeToElement(refusal.Details);

    // The session as the store keeps it, which is where the gate reads it from.
    private static async Task KeptAsync(IServiceProvider services, Session session)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);

        // A session is kept under its person's key, which an account written directly
        // does not have until its first session asks for it.
        ISubjectKeyStore keys = services.GetRequiredService<ISubjectKeyStore>();

        if (await keys.FindBySubjectAsync(session.Subject, cancellationToken) is null)
        {
            await keys.CreateAsync(session.Subject, cancellationToken);
        }

        await services.GetRequiredService<ISessionStore>().AddAsync(
            session,
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(32),
            cancellationToken);
        await work.CommitAsync(cancellationToken);
    }

    // A record whose publishing the caller's grant confers, publishing being bound to a
    // step-up gate.
    private async Task<(SubjectId Caller, ResourceReference Record)> PublishingAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);
        RoleName publishing = await deployment.BeginAsync([HostPermissions.Read, HostPermissions.Publish], cancellationToken);
        SubjectId caller = await deployment.AccountAsync(cancellationToken);
        ResourceReference workspace = Reference(Workspace);
        ResourceReference record = Reference(Document);

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, workspace, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(caller), publishing, workspace, false, null, null, cancellationToken);

        return (caller, record);
    }

    // One request on the session through stage 11 to the host's endpoint, which reads who
    // is asking, asks the gate and answers through the library's answers, having first
    // written a header of its own where the case says so.
    private async Task<Answered> AskedAsync(
        Session session,
        Permission permission,
        ResourceReference record,
        bool writes = false)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();

        IServiceProvider services = scope.ServiceProvider;

        await KeptAsync(services, session);

        // BFF-ORDER-001 stage 5: what resolution leaves for every stage after it.
        services.GetRequiredService<RequestSession>().Resolved(session);

        var context = new DefaultHttpContext
        {
            RequestServices = services,
            TraceIdentifier = Traced,
            Response = { Body = new MemoryStream() },
        };

        await services.GetRequiredService<Concealment>().InvokeAsync(context, async reached =>
        {
            services.GetRequiredService<ConcealedRefusals>().Reached(reached.Response.Headers);

            if (writes)
            {
                reached.Response.Headers["X-Record-Owner"] = "someone";
            }

            IResult answer = await HostsAsync(reached, permission, record, reading);

            await answer.ExecuteAsync(reached);
        });

        return new Answered(
            context.Response.StatusCode,
            string.Join(
                '\n',
                context.Response.Headers.Select(header => header.Key + ": " + header.Value).Order(StringComparer.Ordinal)),
            JsonDocument.Parse(Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray()) is { Length: > 0 } body
                ? body
                : "{}").RootElement.Clone());
    }

    // The host's endpoint, written with the public surface alone.
    private static Task<IResult> HostsAsync(
        HttpContext context,
        Permission permission,
        ResourceReference record,
        HostContext reading)
    {
        IAccessGate gate = context.RequestServices.GetRequiredService<IAccessGate>();

        return context.RequestServices.GetRequiredService<IRequestAccess>().Current().Match(
            async held => Answers.Of(
                await gate.RequireAsync(held.Context, permission, record, Sources(reading), context.RequestAborted),
                Results.NoContent()),
            error => Task.FromResult(Answers.Refused(error)));
    }

    // What the library's own endpoints are answered for the same session: the gate asked
    // with the context they pass, on what stage 5 left.
    private async Task<Error?> LibrarysAsync(Session session, Permission permission, ResourceReference record)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();

        IServiceProvider services = scope.ServiceProvider;
        RequestSession resolved = services.GetRequiredService<RequestSession>();

        resolved.Resolved(session);

        Result outcome = await services.GetRequiredService<IAccessGate>().RequireAsync(
            resolved.Asking,
            permission,
            record,
            Sources(reading),
            TestContext.Current.CancellationToken);

        return outcome.Match(() => (Error?)null, error => error);
    }

    // What one request was answered: its status, its headers and its body.
    private sealed record Answered(int Status, string Headers, JsonElement Body);
}
