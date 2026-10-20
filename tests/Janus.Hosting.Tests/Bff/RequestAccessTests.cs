using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Recovery;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Accounts;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// What a host's own endpoint reads and answers with through the public surface: who is
/// asking, the library's answers and the request's source, each as the library's own
/// endpoints have them for the same request (BFF-HOST-001, BFF-HOST-003, BFF-HOST-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class RequestAccessTests : IAsyncDisposable
{
    private const string Reason = "The operator cannot be reached.";

    private const string Traced = "0HN7HOST:00000001";

    private const string Link = "the-approver-sent-this-one";

    private readonly HostEndpoints _host = new();

    private readonly Deployment _deployment;

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment whose host maps its own endpoints beside the library's, which a
    /// browser can register against to get a session.
    /// </summary>
    public RequestAccessTests()
    {
        _deployment = new Deployment(host: _host.Map);
        Flow.Prepare(_deployment);
    }

    /// <summary>
    /// BFF-HOST-001 AC1: in a request carrying a live sign-in session, the access holds
    /// the person's context and the session the library's own endpoints use for it, and
    /// an operation called with them answers as it answers the library's endpoint: the
    /// sessions listed, with the one asking marked, are the library's own list.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_HOST_001_AC1_TheAccessIsWhatTheLibrarysEndpointsUseAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId person = _deployment.Directory.Created[^1].Subject;

        Answer hosts = await browser.SendAsync("GET", HostEndpoints.Sessions);
        Answer librarys = await browser.SendAsync("GET", "/account/sessions");

        RequestAccess read = Assert.Single(_host.Read);
        (string Id, bool Current)[] listed = Listed(hosts);

        Assert.Equal(StatusCodes.Status200OK, hosts.Status);
        Assert.Equal(StatusCodes.Status200OK, librarys.Status);
        Assert.Equal(Listed(librarys), listed);
        Assert.Equal(read.Session.ToString(), Assert.Single(listed, session => session.Current).Id);
        Assert.Equal((person, person), (read.Context.Acting!.Value, read.Context.Effective!.Value));
        Assert.Null(read.Context.BreakGlassReason);
        Assert.False(read.Context.IsSystem);
    }

    /// <summary>
    /// BFF-HOST-001 AC2: there is no access for a request carrying no session, one that
    /// has ended, or only a pre-authentication, registration or enrolment session, and
    /// none in a job: each is answered <c>auth.session.expired</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_HOST_001_AC2_NoAccessIsAnsweredWithoutALiveSignInSessionAsync()
    {
        Browser ending = await Flow.SignedInAsync(_deployment);

        var arriving = new Browser(_deployment);
        Answer carryingNone = await arriving.SendAsync("GET", HostEndpoints.Visitor);
        Answer preAuthenticated = await arriving.SendAsync("GET", HostEndpoints.Visitor);

        Browser registering = await Flow.BegunAsync(_deployment);
        Answer registration = await registering.SendAsync("GET", HostEndpoints.Visitor);

        Browser enrolling = await EnrollingAsync();
        Answer enrolment = await enrolling.SendAsync("GET", HostEndpoints.Visitor);

        _deployment.Clock.Advance(TimeSpan.FromDays(100));

        Answer ended = await ending.SendAsync("GET", HostEndpoints.Visitor);

        await using AsyncServiceScope job = _deployment.Scope();

        Result<RequestAccess> inAJob = job.ServiceProvider.GetRequiredService<IRequestAccess>().Current();

        Assert.Contains(BrowserCookies.PreAuthentication, arriving.Cookies.Keys);
        Assert.All(
            new[] { carryingNone, preAuthenticated, registration, enrolment, ended },
            answered =>
            {
                Assert.Equal(StatusCodes.Status401Unauthorized, answered.Status);
                Assert.Equal(ErrorCodes.SessionExpired.ToString(), answered.Text("code"));
            });
        Assert.Equal(ErrorCodes.SessionExpired, inAJob.Match(_ => ErrorCodes.SystemFault, error => error.Code));
        Assert.Empty(_host.Read);
    }

    /// <summary>
    /// BFF-HOST-001 AC3: in a break-glass session the context carries the break-glass
    /// reason, and a record a host's call writes in it carries the reason.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_HOST_001_AC3_ABreakGlassSessionsReasonReachesWhatAHostsCallWritesAsync()
    {
        _ = await Flow.SignedInAsync(_deployment);
        SubjectId person = _deployment.Directory.Created[^1].Subject;
        Browser breaking = await BrokenAsync(person);

        Answer preferred = await breaking.SendAsync("PUT", HostEndpoints.Preferences);

        await using AsyncServiceScope scope = _deployment.Scope();

        var audit = (AccountAuditInMemory)scope.ServiceProvider.GetRequiredService<IAccountAudit>();
        RecordedChange written = audit.Recorded[^1];

        Assert.Equal(StatusCodes.Status204NoContent, preferred.Status);
        Assert.Equal(Reason, Assert.Single(_host.Read).Context.BreakGlassReason);
        Assert.Equal(AuditActions.PreferencesChanged, written.Action);
        Assert.Equal(person, written.Subject);
        Assert.Equal(Reason, written.BreakGlassReason);
    }

    /// <summary>
    /// BFF-HOST-001 AC5 (D-206): a step-up rotates the identifier the session's cookie
    /// carries and keeps the record, so the next request's access names the same
    /// session, and a request carrying the cookie from before the rotation has none.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_HOST_001_AC5_AStepUpKeepsTheSessionAndLeavesTheCookieBeforeItNoAccessAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _ = await browser.SendAsync("GET", HostEndpoints.Visitor);

        string before = browser.Cookies[BrowserCookies.Session];
        Answer stepped = await SteppedUpAsync(browser);

        _ = await browser.SendAsync("GET", HostEndpoints.Visitor);

        var planted = new Browser(_deployment);

        planted.Hold(BrowserCookies.Session, before);

        Answer carried = await planted.SendAsync("GET", HostEndpoints.Visitor);

        Assert.Equal(StatusCodes.Status200OK, stepped.Status);
        Assert.NotEqual(before, browser.Cookies[BrowserCookies.Session]);
        Assert.Equal(2, _host.Read.Count);
        Assert.Equal(_host.Read[0].Session, _host.Read[1].Session);
        Assert.Equal(StatusCodes.Status401Unauthorized, carried.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), carried.Text("code"));
    }

    /// <summary>
    /// BFF-HOST-003 AC1: a failure a host endpoint answers through <c>Answers</c> carries
    /// the code, its details, the status the reference gives it and the request's own
    /// correlation identifier, and is byte for byte what the library's own endpoint
    /// answers for the same failure.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_HOST_003_AC1_AFailureIsTheLibrarysOwnAnswerAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        string unheld = SessionId.New(TimeProvider.System).ToString();

        Answer librarys = await browser.SendAsync("DELETE", "/account/sessions/" + unheld, trace: Traced);
        Answer hosts = await browser.SendAsync("DELETE", HostEndpoints.Sessions + "/" + unheld, trace: Traced);

        Assert.Equal(StatusCodes.Status404NotFound, hosts.Status);
        Assert.Equal(ErrorCodes.ResourceNotFound.ToString(), hosts.Text("code"));
        Assert.Equal(Traced, hosts.Text("correlationId"));
        Assert.Equal(JsonValueKind.Object, hosts.Json().GetProperty("details").ValueKind);
        Assert.Equal(librarys.Status, hosts.Status);
        Assert.Equal(librarys.Header("Content-Type"), hosts.Header("Content-Type"));
        Assert.Equal(librarys.Body, hosts.Body);
    }

    /// <summary>
    /// BFF-HOST-003 AC3: a code the reference does not list is a fault of the calling
    /// code, answered 500 <c>system.fault</c> with nothing else, whether it is answered
    /// alone or as the failure of a result.
    /// </summary>
    /// <param name="typed">Whether the failure is a result's.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BFF_HOST_003_AC3_ACodeTheReferenceDoesNotListIsAFaultAsync(bool typed)
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer answered = await browser.SendAsync(
            "GET",
            HostEndpoints.Unlisted + "?typed=" + (typed ? "true" : "false"),
            trace: Traced);

        Assert.Equal(StatusCodes.Status500InternalServerError, answered.Status);
        Assert.Equal(ErrorCodes.SystemFault.ToString(), answered.Text("code"));
        Assert.Equal(Traced, answered.Text("correlationId"));
        Assert.Empty(answered.Json().GetProperty("details").EnumerateObject());
        Assert.DoesNotContain("workspace", answered.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// BFF-HOST-004 AC1: for the same request, the source a host passes is what the
    /// library's own endpoints pass: two addresses of one IPv6 /64 answer one source, an
    /// IPv6 address whose first three bits are 000 its whole address, an IPv4-mapped
    /// address its IPv4 address, and a request with no address <c>unknown</c>.
    /// </summary>
    /// <param name="address">The address the connection arrived on, or nothing.</param>
    /// <param name="source">The source it is keyed as.</param>
    [Theory]
    [InlineData("203.0.113.9", "203.0.113.9")]
    [InlineData("::ffff:203.0.113.9", "203.0.113.9")]
    [InlineData("2001:db8:1:2:aaaa::1", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2:bbbb:cccc:dddd:2", "2001:db8:1:2::/64")]
    [InlineData("1:2:3:4:5:6:7:8", "1:2:3:4:5:6:7:8")]
    [InlineData(null, "unknown")]
    public void BFF_HOST_004_AC1_TheSourceIsTheOneTheLibrarysEndpointsPass(string? address, string source)
    {
        var context = new DefaultHttpContext();

        context.Connection.RemoteIpAddress = address is null ? null : IPAddress.Parse(address);

        Assert.Equal(source, RequestSource.Of(context.Request));
        Assert.Equal(RequestOrigin.Source(context.Request), RequestSource.Of(context.Request));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _randomness.Dispose();
        await _deployment.DisposeAsync();
    }

    private static (string Id, bool Current)[] Listed(Answer answered) =>
    [
        .. answered.Json()
            .EnumerateArray()
            .Select(session => (session.GetProperty("id").GetString()!, session.GetProperty("current").GetBoolean()))
            .Order(),
    ];

    // A browser holding the enrolment session an approved recovery opened for the person
    // last signed in, which stage 5 resolves only on the routes chapter 09 lists for it.
    private async Task<Browser> EnrollingAsync()
    {
        SubjectId person = _deployment.Directory.Created[^1].Subject;

        await _deployment.Links.ReplaceAsync(
            RecoveryLink.Issue(
                OpaqueToken.Of(Link),
                person,
                RecoveryPurpose.Enrolment,
                _deployment.Clock.GetUtcNow(),
                TimeSpan.FromHours(1),
                approver: _deployment.Directory.Created[0].Subject,
                mailboxLost: false),
            TestContext.Current.CancellationToken);

        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        Assert.Equal(
            StatusCodes.Status200OK,
            (await browser.SendAsync("POST", "/enrol/begin", ("token", Link))).Status);

        return browser;
    }

    // A browser holding a break-glass session for the person, as the session store holds
    // one and the browser carries it.
    // A step-up with the password the flow set, on a challenge the browser opens for the
    // address the flow registered (09 POST /auth/step-up).
    private static async Task<Answer> SteppedUpAsync(Browser browser)
    {
        Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));

        Assert.Equal(StatusCodes.Status200OK, began.Status);

        return await browser.SendAsync(
            "POST",
            "/auth/step-up",
            ("challengeId", began.Text("challengeId")),
            ("factor", "password"),
            ("value", Flow.Password));
    }

    private async Task<Browser> BrokenAsync(SubjectId person)
    {
        var secret = OpaqueToken.Draw(_randomness);
        var token = OpaqueToken.Draw(_randomness);
        var browser = new Browser(_deployment);

        await _deployment.Sessions.AddAsync(
            Session.Begin(
                SessionId.New(TimeProvider.System),
                person,
                new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
                new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
                _deployment.Clock.GetUtcNow(),
                TimeSpan.FromHours(1),
                TimeSpan.FromHours(4),
                Reason),
            secret.Fingerprint(),
            token.Fingerprint(),
            TestContext.Current.CancellationToken);

        browser.Hold(BrowserCookies.Session, secret.Value);
        browser.Hold(BrowserCookies.Csrf, token.Value);

        return browser;
    }
}
