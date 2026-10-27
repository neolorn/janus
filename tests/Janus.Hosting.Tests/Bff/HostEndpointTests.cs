using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Recovery;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// What the browser profile does for the endpoints a host maps after it: each needs a
/// live sign-in session unless it is marked as answering anyone, and a marked one is
/// held to every other stage (BFF-HOST-002, BFF-CSRF-005a, BFF-ORDER-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class HostEndpointTests : IAsyncDisposable
{
    private const string Link = "the-approver-sent-this-one";

    private const string Replacement = "quincejellyonasaucer";

    private readonly HostEndpoints _host = new();

    private readonly Deployment _deployment;

    /// <summary>
    /// A deployment whose host maps its own endpoints beside the library's, which a
    /// browser can register against to get a session.
    /// </summary>
    public HostEndpointTests()
    {
        _deployment = new Deployment(host: _host.Map);
        Flow.Prepare(_deployment);
    }

    /// <summary>
    /// BFF-HOST-002 AC1: a request carrying no session to a host endpoint left unmarked
    /// is answered 401 <c>auth.session.expired</c> with the code alone, before the
    /// endpoint runs.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_HOST_002_AC1_AnUnmarkedHostEndpointRefusesABrowserHoldingNoSessionAsync()
    {
        Answer answered = await new Browser(_deployment).SendAsync("GET", HostEndpoints.Workspaces);

        Assert.Equal(StatusCodes.Status401Unauthorized, answered.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), answered.Text("code"));
        Assert.Empty(answered.Json().GetProperty("details").EnumerateObject());
        Assert.Empty(_host.Reached);
    }

    /// <summary>
    /// BFF-HOST-002 AC1: a request whose sign-in session has ended is answered 401
    /// <c>auth.session.expired</c> with <c>details.reauthenticate</c>, before the
    /// endpoint runs, where the same session reached it while it was live.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_HOST_002_AC1_AnUnmarkedHostEndpointAnswersAnEndedSessionWithWhatMustBeRedoneAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer live = await browser.SendAsync("GET", HostEndpoints.Workspaces);

        _deployment.Clock.Advance(TimeSpan.FromDays(100));

        Answer ended = await browser.SendAsync("GET", HostEndpoints.Workspaces);

        Assert.Equal(StatusCodes.Status200OK, live.Status);
        Assert.Equal(StatusCodes.Status401Unauthorized, ended.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), ended.Text("code"));
        Assert.NotEmpty(ended.Json().GetProperty("details").GetProperty("reauthenticate").GetString()!);
        Assert.Equal(["GET " + HostEndpoints.Workspaces], _host.Reached);
    }

    /// <summary>
    /// BFF-HOST-002 AC2: a host endpoint marked as answering anyone is reached by a
    /// <c>GET</c> carrying no session, which is issued the pre-authentication session,
    /// and by a state-changing request carrying that session and its token; one without
    /// the token is refused at stage 6 and does not reach it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_HOST_002_AC2_AMarkedHostEndpointAnswersAnyoneAndIsHeldToTheTokenAsync()
    {
        var browser = new Browser(_deployment);

        Answer read = await browser.SendAsync("GET", HostEndpoints.Catalogue);
        Answer untokened = await browser.SendAsync("POST", HostEndpoints.Views, token: false);
        Answer recorded = await browser.SendAsync("POST", HostEndpoints.Views);

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Contains(BrowserCookies.PreAuthentication, browser.Cookies.Keys);
        Assert.Equal(StatusCodes.Status403Forbidden, untokened.Status);
        Assert.Equal(ErrorCodes.SessionCsrfInvalid.ToString(), untokened.Text("code"));
        Assert.Equal(StatusCodes.Status204NoContent, recorded.Status);
        Assert.Equal(["GET " + HostEndpoints.Catalogue, "POST " + HostEndpoints.Views], _host.Reached);
    }

    /// <summary>
    /// BFF-HOST-002 AC4: with the host's endpoints mapped beside them, the library's own
    /// endpoints answer a request carrying no session as they answer it in a deployment
    /// whose host maps none: one marked as needing a session refuses it with the code
    /// alone, and those that need none answer it as they always have.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_HOST_002_AC4_TheLibrarysEndpointsAnswerARequestWithNoSessionAsBeforeAsync()
    {
        await using var before = new Deployment();

        Flow.Prepare(before);

        foreach (Deployment deployment in new[] { before, _deployment })
        {
            var browser = new Browser(deployment);

            Answer account = await browser.SendAsync("GET", "/account/");
            Answer notice = await browser.SendAsync("GET", "/privacy/notice");
            Answer begun = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));

            Assert.Equal(StatusCodes.Status401Unauthorized, account.Status);
            Assert.Equal(ErrorCodes.SessionExpired.ToString(), account.Text("code"));
            Assert.Empty(account.Json().GetProperty("details").EnumerateObject());
            Assert.Equal(StatusCodes.Status404NotFound, notice.Status);
            Assert.NotEqual(ErrorCodes.SessionExpired.ToString(), notice.Text("code"));
            Assert.Equal(StatusCodes.Status200OK, begun.Status);
            Assert.NotEmpty(begun.Text("challengeId"));
        }
    }

    /// <summary>
    /// BFF-HOST-002 AC5 (D-209): with the host's routing added after the browser profile,
    /// a request carrying no session to a host endpoint left unmarked is answered 401
    /// <c>auth.session.expired</c> with the code alone, before the endpoint runs, and one
    /// to an endpoint marked as answering anyone reaches it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_HOST_002_AC5_WithRoutingAfterTheProfileAnUnmarkedHostEndpointStillRefusesABrowserHoldingNoSessionAsync()
    {
        var host = new HostEndpoints();
        await using var deployment = new Deployment(host: host.Map, routing: mount => _ = mount.UseRouting());

        Flow.Prepare(deployment);
        var browser = new Browser(deployment);

        Answer held = await browser.SendAsync("GET", HostEndpoints.Workspaces);
        Answer open = await browser.SendAsync("GET", HostEndpoints.Catalogue);

        Assert.Equal(StatusCodes.Status401Unauthorized, held.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), held.Text("code"));
        Assert.Empty(held.Json().GetProperty("details").EnumerateObject());
        Assert.Equal(StatusCodes.Status200OK, open.Status);
        Assert.Equal(["GET " + HostEndpoints.Catalogue], host.Reached);
    }

    /// <summary>
    /// BFF-HOST-002 AC5: with the host's routing added after the browser profile, a
    /// request whose sign-in session has ended is answered 401
    /// <c>auth.session.expired</c> with <c>details.reauthenticate</c> before an unmarked
    /// host endpoint runs, where the same session reached it while it was live.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_HOST_002_AC5_WithRoutingAfterTheProfileAnEndedSessionIsAnsweredWithWhatMustBeRedoneAsync()
    {
        var host = new HostEndpoints();
        await using var deployment = new Deployment(host: host.Map, routing: mount => _ = mount.UseRouting());

        Flow.Prepare(deployment);
        Browser browser = await Flow.SignedInAsync(deployment);

        Answer live = await browser.SendAsync("GET", HostEndpoints.Workspaces);

        deployment.Clock.Advance(TimeSpan.FromDays(100));

        Answer ended = await browser.SendAsync("GET", HostEndpoints.Workspaces);

        Assert.Equal(StatusCodes.Status200OK, live.Status);
        Assert.Equal(StatusCodes.Status401Unauthorized, ended.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), ended.Text("code"));
        Assert.NotEmpty(ended.Json().GetProperty("details").GetProperty("reauthenticate").GetString()!);
        Assert.Equal(["GET " + HostEndpoints.Workspaces], host.Reached);
    }

    /// <summary>
    /// BFF-HOST-002 AC6 (D-211): with the host's routing before the browser profile and
    /// after it, an unmarked controller action reached through
    /// <c>MapFallbackToController</c>, the fallback itself marked as answering anyone, is
    /// refused 401 <c>auth.session.expired</c> with the code alone for a browser holding
    /// no session, before it runs, and reached by a signed-in one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_HOST_002_AC6_AnUnmarkedActionAFallbackResolvesToRefusesABrowserHoldingNoSessionWhereverRoutingIsAddedAsync()
    {
        foreach (Action<IApplicationBuilder>? routing in Routings())
        {
            (Answer held, Answer reached) = await FallenBackAsync(routing, nameof(ArchiveController.Shelf), "Archive");

            Assert.Equal(StatusCodes.Status401Unauthorized, held.Status);
            Assert.Equal(ErrorCodes.SessionExpired.ToString(), held.Text("code"));
            Assert.Empty(held.Json().GetProperty("details").EnumerateObject());
            Assert.Equal(StatusCodes.Status200OK, reached.Status);
            Assert.Equal(ArchiveController.Shelved, reached.Body);
        }
    }

    /// <summary>
    /// BFF-HOST-002 AC6 (D-211): with the host's routing before the browser profile and
    /// after it, a controller action reached through <c>MapFallbackToController</c> and
    /// marked with <c>[AllowNoSession]</c>, on the action or on its controller, is reached
    /// by a browser holding no session.
    /// </summary>
    /// <param name="action">The action the fallback resolves to.</param>
    /// <param name="controller">Its controller.</param>
    /// <param name="answered">What the action answers with.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(nameof(ArchiveController.Notice), "Archive", ArchiveController.Noticed)]
    [InlineData(nameof(NoticeboardController.Board), "Noticeboard", NoticeboardController.Boarded)]
    public async Task BFF_HOST_002_AC6_AnActionMarkedWithAllowNoSessionIsReachedWithoutASessionWhereverRoutingIsAddedAsync(
        string action,
        string controller,
        string answered)
    {
        foreach (Action<IApplicationBuilder>? routing in Routings())
        {
            (Answer held, _) = await FallenBackAsync(routing, action, controller);

            Assert.Equal(StatusCodes.Status200OK, held.Status);
            Assert.Equal(answered, held.Body);
        }
    }

    /// <summary>
    /// BFF-HOST-002 AC6 (D-211): with the host's routing before the browser profile and
    /// after it, a request carrying no session with a method a served host path does not
    /// take, a path marked as answering anyone and an unmarked one alike, is answered 404
    /// <c>authz.resource.notfound</c>, as no path is, and no host endpoint runs.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_HOST_002_AC6_AMethodAServedHostPathDoesNotTakeIsNotFoundWhereverRoutingIsAddedAsync()
    {
        foreach (Action<IApplicationBuilder>? routing in Routings())
        {
            var host = new HostEndpoints();
            await using var deployment = new Deployment(host: host.Map, routing: routing);

            Flow.Prepare(deployment);
            var browser = new Browser(deployment);

            foreach (string path in new[] { HostEndpoints.Views, HostEndpoints.Sessions + "/unknown" })
            {
                Answer answered = await browser.SendAsync("GET", path);

                Assert.Equal(StatusCodes.Status404NotFound, answered.Status);
                Assert.Equal(ErrorCodes.ResourceNotFound.ToString(), answered.Text("code"));
            }

            Assert.Empty(host.Reached);
        }
    }

    /// <summary>
    /// BFF-HOST-002 AC6 (D-211): the endpoints ASP.NET Core's routing makes itself for a
    /// method and for a content type a matched path does not take carry no metadata and
    /// the display names stage 8 recognises them by, so they are held to no session; an
    /// endpoint alike in all but its name is held to one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_HOST_002_AC6_TheFrameworksOwnRejectionsAreTheEndpointsStageEightHoldsToNoSessionAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var selected = new List<Endpoint?>();

        using var server = new ServerInMemory();
        using IHost routed = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseServer(server)
                .ConfigureServices(services => services.AddRouting())
                .Configure(application => application
                    .Use(next => context =>
                    {
                        // A request carrying no content type is not judged by its type.
                        context.Request.ContentType = "text/plain";

                        return next(context);
                    })
                    .UseRouting()
                    .Use(next => context =>
                    {
                        selected.Add(context.GetEndpoint());

                        return next(context);
                    })
                    .UseEndpoints(endpoints => endpoints
                        .MapPost("/parcels", () => Results.NoContent())
                        .Accepts<string>("application/json"))))
            .Build();

        await routed.StartAsync(cancellationToken);

        _ = await server.SendAsync("GET", "/parcels");
        _ = await server.SendAsync("POST", "/parcels");

        await routed.StopAsync(cancellationToken);

        Assert.Collection(
            selected,
            method => Assert.Equal(SessionRequired.MethodNotTaken, method?.DisplayName),
            media => Assert.Equal(SessionRequired.MediaTypeNotTaken, media?.DisplayName));
        Assert.All(selected, rejection => Assert.Empty(rejection!.Metadata));
        Assert.All(selected, rejection => Assert.False(SessionRequired.Asks(rejection)));
        Assert.True(SessionRequired.Asks(new Endpoint(
            context => Task.CompletedTask,
            EndpointMetadataCollection.Empty,
            "405 HTTP Method Not Allowed")));
    }

    /// <summary>
    /// BFF-ORDER-001 AC2 (D-209): where the host's routing comes after the browser
    /// profile, the host's middleware between the two runs before the endpoint is known,
    /// the endpoint routing then selects is judged before it runs, and the host's
    /// middleware after that routing runs for a request whose endpoint was replaced with
    /// the refusal, no host endpoint running for it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AC2_MiddlewareAfterLateRoutingRunsForTheRefusalAndNoHostEndpointRunsAsync()
    {
        var host = new HostEndpoints();
        var between = new List<Endpoint?>();
        var after = new List<Endpoint?>();

        await using var deployment = new Deployment(
            host: host.Map,
            routing: mount =>
            {
                _ = mount.Use(next => context =>
                {
                    between.Add(context.GetEndpoint());

                    return next(context);
                });
                _ = mount.UseRouting();
                _ = mount.Use(next => context =>
                {
                    after.Add(context.GetEndpoint());

                    return next(context);
                });
            });

        Flow.Prepare(deployment);
        var browser = new Browser(deployment);

        Answer held = await browser.SendAsync("GET", HostEndpoints.Workspaces);
        Answer open = await browser.SendAsync("GET", HostEndpoints.Catalogue);

        Assert.Equal(StatusCodes.Status401Unauthorized, held.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), held.Text("code"));
        Assert.Equal(StatusCodes.Status200OK, open.Status);
        Assert.Equal([null, null], between);
        Assert.Equal(2, after.Count);
        Assert.NotNull(after[0]);
        Assert.IsNotType<RouteEndpoint>(after[0]);
        Assert.Equal(HostEndpoints.Catalogue, Assert.IsType<RouteEndpoint>(after[1]).RoutePattern.RawText);
        Assert.Equal(["GET " + HostEndpoints.Catalogue], host.Reached);
    }

    /// <summary>
    /// BFF-ORDER-001 AC2 (D-210): with the host's routing added after the browser
    /// profile, an enrolment route answers a browser holding the enrolment session an
    /// approved recovery opened as it answers one that carried no enrolment session, and
    /// the enrolment is left standing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AC2_WithRoutingAfterTheProfileAnEnrolmentRouteAnswersAsForNoEnrolmentSessionAsync()
    {
        await using var deployment = new Deployment(host: new HostEndpoints().Map, routing: mount => _ = mount.UseRouting());

        Flow.Prepare(deployment);
        _ = await Flow.SignedInAsync(deployment);
        Browser enrolling = await EnrollingAsync(deployment);
        var carryingNone = new Browser(deployment);

        _ = await carryingNone.SendAsync("GET", "/auth/session");

        Answer enrolment = await enrolling.SendAsync("POST", "/account/password", ("password", Replacement));
        Answer none = await carryingNone.SendAsync("POST", "/account/password", ("password", Replacement));

        Assert.Equal(StatusCodes.Status401Unauthorized, none.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), none.Text("code"));
        Assert.Equal(none.Status, enrolment.Status);
        Assert.Equal(none.Text("code"), enrolment.Text("code"));
        Assert.Equal(
            none.Json().GetProperty("details").GetRawText(),
            enrolment.Json().GetProperty("details").GetRawText());
        Assert.NotEmpty(deployment.Links.Held);
    }

    /// <summary>
    /// BFF-ORDER-001 AC2 (D-210), API-CONV-002: with the host's routing added after the
    /// browser profile, a request the framework cannot bind (a route value that does not
    /// read as its type, a body the reader cannot turn into the endpoint's, a host
    /// endpoint's query value that does not read) is refused 400
    /// <c>api.request.malformed</c> with the details it carries with routing before the
    /// profile.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AC2_WithRoutingAfterTheProfileAnUnboundRequestIsMalformedAsBeforeAsync()
    {
        await using var before = new Deployment(host: new HostEndpoints().Map);
        await using var after = new Deployment(host: new HostEndpoints().Map, routing: mount => _ = mount.UseRouting());
        var answered = new List<Answer[]>();

        foreach (Deployment deployment in new[] { before, after })
        {
            Flow.Prepare(deployment);
            Browser browser = await Flow.SignedInAsync(deployment);

            answered.Add(
            [
                await browser.SendAsync("DELETE", "/account/sessions/not-an-identifier"),
                await browser.SendAsync("PATCH", "/account/credentials/" + Guid.NewGuid(), "{ \"label\": 7 }"),
                await browser.SendAsync("GET", HostEndpoints.Unlisted + "?typed=perhaps"),
            ]);
        }

        Assert.Equal("id", answered[0][0].Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Equal("label", answered[0][1].Json().GetProperty("details").GetProperty("member").GetString());

        for (int asked = 0; asked < answered[0].Length; asked++)
        {
            Answer early = answered[0][asked];
            Answer late = answered[1][asked];

            Assert.Equal(StatusCodes.Status400BadRequest, early.Status);
            Assert.Equal(ErrorCodes.RequestMalformed.ToString(), early.Text("code"));
            Assert.Equal(early.Status, late.Status);
            Assert.Equal(early.Text("code"), late.Text("code"));
            Assert.NotEmpty(late.Text("correlationId"));
            Assert.Equal(
                early.Json().GetProperty("details").GetRawText(),
                late.Json().GetProperty("details").GetRawText());
        }
    }

    /// <summary>
    /// BFF-CSRF-005a AC1: first contact with a host endpoint marked as answering anyone
    /// issues the pre-authentication session, as first contact with any pre-session
    /// endpoint does.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005a_AC1_FirstContactWithAMarkedHostEndpointIssuesAPreAuthenticationSessionAsync()
    {
        var browser = new Browser(_deployment);

        Answer read = await browser.SendAsync("GET", HostEndpoints.Catalogue);

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Contains(read.SetCookie, written => written.StartsWith(BrowserCookies.PreAuthentication + "=", StringComparison.Ordinal));
        Assert.Contains(read.SetCookie, written => written.StartsWith(BrowserCookies.Csrf + "=", StringComparison.Ordinal));
        Assert.DoesNotContain(BrowserCookies.Session, browser.Cookies.Keys);
    }

    /// <summary>
    /// BFF-ORDER-001 AC4: the cookie of an ended session reaches a host endpoint that
    /// requires no session, and a host endpoint that requires one answers 401
    /// <c>auth.session.expired</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AC4_AnEndedSessionReachesAMarkedHostEndpointAndIsRefusedByAnUnmarkedOneAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Clock.Advance(TimeSpan.FromDays(100));

        Answer open = await browser.SendAsync("GET", HostEndpoints.Catalogue);
        Answer held = await browser.SendAsync("GET", HostEndpoints.Workspaces);

        Assert.Equal(StatusCodes.Status200OK, open.Status);
        Assert.Equal(StatusCodes.Status401Unauthorized, held.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), held.Text("code"));
        Assert.Equal(["GET " + HostEndpoints.Catalogue], _host.Reached);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _deployment.DisposeAsync();

    // Where the host adds its routing: before the profiles, and after the browser profile.
    private static IEnumerable<Action<IApplicationBuilder>?> Routings() =>
        [null, mount => _ = mount.UseRouting()];

    // What a browser holding no session and a signed-in one are answered at an action the
    // host reaches only through a fallback to it, the fallback marked as answering anyone,
    // with routing where the host adds it.
    private static async Task<(Answer Held, Answer Reached)> FallenBackAsync(
        Action<IApplicationBuilder>? routing,
        string action,
        string controller)
    {
        await using var deployment = new Deployment(
            host: endpoints => _ = endpoints.MapFallbackToController(action, controller).AllowNoSession(),
            routing: routing,
            services: services => _ = services
                .AddControllers()
                .ConfigureApplicationPartManager(parts =>
                {
                    parts.ApplicationParts.Clear();
                    parts.ApplicationParts.Add(new AssemblyPart(typeof(ArchiveController).Assembly));
                }));

        Flow.Prepare(deployment);

        Answer held = await new Browser(deployment).SendAsync("GET", "/archive/shelf");
        Browser signedIn = await Flow.SignedInAsync(deployment);

        return (held, await signedIn.SendAsync("GET", "/archive/shelf"));
    }

    // A browser holding the enrolment session an approved recovery opened for the person
    // last signed in, the first person registered having approved it.
    private static async Task<Browser> EnrollingAsync(Deployment deployment)
    {
        await deployment.Links.ReplaceAsync(
            RecoveryLink.Issue(
                OpaqueToken.Of(Link),
                deployment.Directory.Created[^1].Subject,
                RecoveryPurpose.Enrolment,
                deployment.Clock.GetUtcNow(),
                TimeSpan.FromHours(1),
                approver: deployment.Directory.Created[0].Subject,
                mailboxLost: false),
            TestContext.Current.CancellationToken);

        var browser = new Browser(deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        Assert.Equal(
            StatusCodes.Status200OK,
            (await browser.SendAsync("POST", "/enrol/begin", ("token", Link))).Status);

        return browser;
    }
}
