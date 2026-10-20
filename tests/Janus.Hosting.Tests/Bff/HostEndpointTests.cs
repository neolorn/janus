using System;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Authorization;
using Microsoft.AspNetCore.Http;
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
