using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
}
