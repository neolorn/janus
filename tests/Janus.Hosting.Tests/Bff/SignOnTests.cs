using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// This application establishing its own session from the one the authentication
/// application holds: the forwarding, the return, the back-channel exchange and what
/// is left afterwards (BFF-SESS-006, BFF-SESS-003, BFF-SESS-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class SignOnTests
{
    private const string Client = "this-application";

    private const string Return = "https://identity.example.test/auth/signon/return";

    private const string Secret = "a-secret-the-deployment-set";

    private const string Page = "/account/profile";

    /// <summary>
    /// BFF-SESS-006 AC1: a browser that holds no session here, whose person holds a
    /// live record at the authentication application, ends with a session of this
    /// application's own and is never asked anything.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC1_ALiveRecordEstablishesASessionWithNoInteractionAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer established = await SignedOnAsync(deployment, arriving, holder);

        Assert.Equal(StatusCodes.Status302Found, established.Status);
        Assert.Equal(Page, Where(established));
        Assert.Equal(
            StatusCodes.Status200OK,
            (await arriving.SendAsync("GET", "/auth/session")).Status);
    }

    /// <summary>
    /// BFF-SESS-006 AC2: the code is traded on this server's own connection, and
    /// nothing the exchange returned reaches the browser in a body, a header or a
    /// cookie.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC2_TheExchangeIsServerToServerAndHandsTheBrowserNoTokenAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer established = await SignedOnAsync(deployment, arriving, holder);

        Assert.Equal(
            ["/oidc/par", "/oidc/token"],
            deployment.Provider.Asked.Select(asked => asked.AbsolutePath));
        Assert.Empty(established.Body);
        Assert.DoesNotContain(
            "token",
            string.Join(' ', established.SetCookie),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "token",
            string.Join(' ', established.Headers.Select(header => header.Key)),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// BFF-SESS-006 AC3: a return presenting a state the browser was not sent out
    /// with establishes nothing and is recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC3_AMismatchedStateIsRejectedAndLoggedAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        Answer refused = await arriving.SendAsync(
            "GET",
            Local(Where(issued)).Replace(
                Parameter(Where(issued), "state"),
                "a-state-of-another-browser",
                StringComparison.Ordinal));

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(
            ErrorCodes.SessionCsrfInvalid.ToString(),
            refused.Json().GetProperty("code").GetString());
        Assert.Contains(
            deployment.SignOnLog.Entries,
            entry => entry.Level is LogLevel.Warning);
    }

    /// <summary>
    /// BFF-SESS-006 AC3: a code that has already been returned once is bound to
    /// nothing the second time, so a browser cannot present it again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC3_AReturnedCodeIsNotAcceptedTwiceAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        _ = await arriving.SendAsync("GET", Local(Where(issued)));

        Answer again = await arriving.SendAsync("GET", Local(Where(issued)));

        Assert.Equal(StatusCodes.Status403Forbidden, again.Status);
        _ = Assert.Single(
            deployment.Provider.Asked,
            asked => string.Equals(asked.AbsolutePath, "/oidc/token", StringComparison.Ordinal));
    }

    /// <summary>
    /// BFF-SESS-006 AC4: what is left when the exchange is done is one session record
    /// of this application's own, and no token anywhere.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC4_NothingButThePerApplicationSessionIsHeldAfterwardsAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);

        _ = await SignedOnAsync(deployment, arriving, holder);

        Session established = Assert.Single(
            deployment.Sessions.All,
            session => session.Type is SessionType.PerApp);

        Assert.Equal(Spine(deployment), established.Spine);
        Assert.DoesNotContain(deployment.Contacts.All, contact => contact.SignOn is not null);
    }

    /// <summary>
    /// BFF-SESS-006 AC5: the session this application holds stands on the record, so
    /// revoking the record ends it at the next request rather than at its own expiry.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC5_RevokingTheRecordEndsThePerApplicationSessionAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);

        _ = await SignedOnAsync(deployment, arriving, holder);

        await deployment.Sessions.EndSpineAsync(
            Spine(deployment),
            deployment.Clock.GetUtcNow(),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            (await arriving.SendAsync("GET", "/auth/session")).Status);
    }

    /// <summary>
    /// BFF-SESS-006, AUTH-SESS-012 AC3: a browser whose person holds no record is
    /// told `login_required` once, and the second attempt asks for a sign-in, which
    /// the provider answers by forwarding to the screen the deployment declared.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_ABrowserWithNoRecordIsSentToSignInAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer refused = await new Browser(deployment).SendAsync("GET", Local(Where(forwarded)));
        Answer again = await arriving.SendAsync("GET", Local(Where(refused)));

        Assert.Equal("login_required", Parameter(Where(refused), "error"));
        Assert.Equal(
            ["none", string.Empty],
            deployment.Provider.Carried.Select(carried => Parameter(carried, "prompt")));
        Assert.DoesNotContain(Inside, deployment.SignOnLog.Entries);

        Answer screen = await new Browser(deployment).SendAsync("GET", Local(Where(again)));

        Assert.Equal("https://identity.example.test/signin", Where(screen));
    }

    /// <summary>
    /// BFF-SESS-006, API-REDIR-001 AC1: the destination the browser returns to is the
    /// one the registry holds, and a request naming another one changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_TheDestinationIsTheRegisteredOneAndNeverAskedForAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Answer forwarded = await new Browser(deployment).SendAsync(
            "GET",
            Start + "&redirect_uri=" + Uri.EscapeDataString("https://attacker.test/collect"));

        Assert.Equal(Return, Parameter(Assert.Single(deployment.Provider.Carried), "redirect_uri"));
        Assert.Equal(string.Empty, Parameter(Where(forwarded), "redirect_uri"));
    }

    /// <summary>
    /// AUTH-OIDC-006 AC2: the authorization request is pushed on this server's own
    /// connection, and the browser is forwarded carrying the client and the reference
    /// it was answered with and nothing of the request itself.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC2_TheBrowserCarriesOnlyThePushedReferenceAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Answer forwarded = await new Browser(deployment).SendAsync("GET", Start);
        string where = Where(forwarded);

        Assert.Equal("/oidc/par", Assert.Single(deployment.Provider.Asked).AbsolutePath);
        Assert.Equal(
            ["client_id", "request_uri"],
            where[(where.IndexOf('?', StringComparison.Ordinal) + 1)..]
                .Split('&')
                .Select(pair => pair[..pair.IndexOf('=', StringComparison.Ordinal)])
                .Order(StringComparer.Ordinal));
        Assert.Equal(Client, Parameter(where, "client_id"));
        Assert.StartsWith(
            "urn:ietf:params:oauth:request_uri:",
            Parameter(where, "request_uri"),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// AUTH-OIDC-006 AC2, BFF-ERR-001 AC5 and chapter 09: a request the provider will
    /// not take when it is pushed sends the browser to no provider. What it refuses is
    /// the deployment's own request, so the answer is a fault and nothing is bound.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC2_ARequestThePushRefusesIsNotForwardedAsync()
    {
        await using var deployment = new Deployment();

        // The registry holds a destination no authorization request may name, one
        // carrying a fragment (RFC 6749 section 3.1.2), which registration refuses and
        // only a registry changed by hand holds.
        await deployment.Clients.AddAsync(
            new OidcClient(Client, Client, OidcClientKind.BrowserApplication, Return + "#fragment", ["openid"]),
            Encoding.UTF8.GetBytes(Secret),
            deployment.Clock.GetUtcNow(),
            TestContext.Current.CancellationToken);

        Answer faulted = await new Browser(deployment).SendAsync("GET", Start);

        Faulted(faulted);
        Assert.Equal("/oidc/par", Assert.Single(deployment.Provider.Asked).AbsolutePath);
        Assert.DoesNotContain(Inside, deployment.SignOnLog.Entries);
        Assert.DoesNotContain(deployment.Contacts.All, contact => contact.SignOn is not null);
    }

    /// <summary>
    /// BFF-SESS-006 and BFF-CSRF-005a AC1: a start from a browser that carries no
    /// pre-authentication session is issued one, and the sign-on is bound to it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005a_AC1_AStartWithNoPreAuthenticationSessionIsIssuedOneAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Answer forwarded = await new Browser(deployment).SendAsync("GET", Start);

        Assert.Equal(StatusCodes.Status302Found, forwarded.Status);
        Assert.Contains(
            forwarded.SetCookie,
            written => written.StartsWith(BrowserCookies.PreAuthentication + "=", StringComparison.Ordinal));
        _ = Assert.Single(deployment.Contacts.All, contact => contact.SignOn is not null);
    }

    /// <summary>
    /// BFF-SESS-006 and chapter 09: a start the deployment cannot make, its client
    /// being in no registry, returns the browser to where it was going with the code
    /// of a session that is not there, and answers no body.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AStartThatFailsReturnsTheBrowserExpiredAsync()
    {
        await using var deployment = new Deployment();

        Answer refused = await new Browser(deployment).SendAsync(
            "GET",
            "/auth/signon?returnTo=" + Uri.EscapeDataString(Page + "#keys"));

        Assert.Equal(StatusCodes.Status302Found, refused.Status);
        Assert.Equal(Page + "?error=" + ErrorCodes.SessionExpired + "#keys", refused.Location);
        Assert.Empty(refused.Body);
        Assert.Contains((LogLevel.Error, 12), deployment.SignOnLog.Entries);
        Assert.DoesNotContain(deployment.Contacts.All, contact => contact.SignOn is not null);
    }

    /// <summary>
    /// BFF-SESS-006 and chapter 09: a return whose state is the browser's and which
    /// fails otherwise, the provider refusing, no code coming back or the code not
    /// being one the provider trades, returns the browser to the stored return address
    /// with the code of a session that is not there, establishes nothing and answers
    /// no body.
    /// </summary>
    /// <param name="answered">What the provider returned the browser with, beside its state.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("error=access_denied")]
    [InlineData("error=access_denied&code=a-code")]
    [InlineData("")]
    [InlineData("code=a-code-the-provider-never-issued")]
    public async Task BFF_SESS_006_AReturnThatFailsReturnsTheBrowserExpiredAsync(string answered)
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        var arriving = new Browser(deployment);

        _ = await arriving.SendAsync("GET", Start);

        Answer refused = await arriving.SendAsync(
            "GET",
            "/auth/signon/return?state="
            + Uri.EscapeDataString(Parameter("?" + deployment.Provider.Carried[^1], "state"))
            + "&" + answered);

        Assert.Equal(StatusCodes.Status302Found, refused.Status);
        Assert.Equal(Page + "?error=" + ErrorCodes.SessionExpired, refused.Location);
        Assert.Empty(refused.Body);
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-SESS-006 AC5 and chapter 09: a record revoked while the browser was on its
    /// way back establishes nothing, and the browser is returned to the stored return
    /// address with the code of a session that is not there.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AReturnOnARevokedRecordReturnsTheBrowserExpiredAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        await deployment.Sessions.EndSpineAsync(
            Spine(deployment),
            deployment.Clock.GetUtcNow(),
            TestContext.Current.CancellationToken);

        Answer refused = await arriving.SendAsync("GET", Local(Where(issued)));

        Assert.Equal(StatusCodes.Status302Found, refused.Status);
        Assert.Equal(Page + "?error=" + ErrorCodes.SessionExpired, refused.Location);
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp && held.EndedAt is null);
    }

    /// <summary>
    /// BFF-ERR-002, BFF-CSRF-005a and chapter 09: a start for which no
    /// pre-authentication session can be issued is a fault, answered as the pipeline
    /// answers one: the browser is sent nowhere, no code is carried in a redirect and
    /// no request is pushed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_002_AStartNoPreAuthenticationSessionCanBeIssuedForIsAFaultAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        deployment.Work.RefusesBegin = Error.From(ErrorCodes.SystemFault);

        Answer faulted = await new Browser(deployment).SendAsync("GET", Start);

        Faulted(faulted);
        Assert.Empty(deployment.Contacts.All);
        Assert.Empty(deployment.Provider.Asked);
    }

    /// <summary>
    /// BFF-ERR-002 and chapter 09: a push the authentication application cannot be
    /// reached for is a fault, and nothing is bound to the browser.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_002_APushThatCannotReachTheAuthenticationApplicationIsAFaultAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        deployment.Provider.Reachable = false;

        Answer faulted = await new Browser(deployment).SendAsync("GET", Start);

        Faulted(faulted);
        Assert.DoesNotContain(deployment.Contacts.All, contact => contact.SignOn is not null);
    }

    /// <summary>
    /// BFF-ERR-002 and chapter 09: a push answered a 5xx, or with no answer in its
    /// protocol's shape (neither the reference nor a refusal naming its
    /// <c>error</c>), is a fault and never the code of a session that is not there.
    /// </summary>
    /// <param name="status">The status the push is answered.</param>
    /// <param name="body">The body it is answered with.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, """{"error":"server_error"}""")]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.BadGateway, "<html></html>")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
    [InlineData(HttpStatusCode.Created, "{}")]
    [InlineData(HttpStatusCode.Created, """{"expires_in":60}""")]
    [InlineData(HttpStatusCode.Created, "")]
    [InlineData(HttpStatusCode.Found, "")]
    [InlineData(HttpStatusCode.BadRequest, "")]
    [InlineData(HttpStatusCode.BadRequest, "<html></html>")]
    [InlineData(HttpStatusCode.BadRequest, "{}")]
    public async Task BFF_ERR_002_APushAnsweredA5xxOrOutsideItsProtocolsShapeIsAFaultAsync(
        HttpStatusCode status,
        string body)
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        deployment.Provider.Answers["/oidc/par"] = (status, body);

        Answer faulted = await new Browser(deployment).SendAsync("GET", Start);

        Faulted(faulted);
        Assert.DoesNotContain(deployment.Contacts.All, contact => contact.SignOn is not null);
    }

    /// <summary>
    /// BFF-ERR-001 AC5 and chapter 09: every error the authentication application
    /// answers a push with is a fault, since it refuses the deployment's own client or
    /// request and nothing the person did: the browser is sent nowhere, nothing is
    /// bound to it and no refusal is recorded.
    /// </summary>
    /// <param name="status">The status the push is answered.</param>
    /// <param name="body">The body it is answered with.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"invalid_client"}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"invalid_request"}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""")]
    public async Task BFF_ERR_001_AC5_AnErrorThePushReadsIsAFaultAsync(
        HttpStatusCode status,
        string body)
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        deployment.Provider.Answers["/oidc/par"] = (status, body);

        Answer faulted = await new Browser(deployment).SendAsync("GET", Start);

        Faulted(faulted);
        Assert.DoesNotContain(Inside, deployment.SignOnLog.Entries);
        Assert.DoesNotContain(deployment.Contacts.All, contact => contact.SignOn is not null);
    }

    /// <summary>
    /// BFF-ERR-002 and chapter 09: an exchange the authentication application cannot
    /// be reached for is a fault, and no session is established.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_002_AnExchangeThatCannotReachTheAuthenticationApplicationIsAFaultAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        deployment.Provider.Reachable = false;

        Answer faulted = await arriving.SendAsync("GET", Local(Where(issued)));

        Faulted(faulted);
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-ERR-002 and chapter 09: an exchange answered a 5xx, or with no answer in
    /// its protocol's shape (neither the identity token nor a refusal naming its
    /// <c>error</c>), is a fault and never the code of a session that is not there.
    /// </summary>
    /// <param name="status">The status the exchange is answered.</param>
    /// <param name="body">The body it is answered with.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, """{"error":"server_error"}""")]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.BadGateway, "<html></html>")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.OK, """{"access_token":"a"}""")]
    [InlineData(HttpStatusCode.OK, "")]
    [InlineData(HttpStatusCode.Found, "")]
    [InlineData(HttpStatusCode.BadRequest, "")]
    [InlineData(HttpStatusCode.BadRequest, "<html></html>")]
    [InlineData(HttpStatusCode.BadRequest, "{}")]
    public async Task BFF_ERR_002_AnExchangeAnsweredA5xxOrOutsideItsProtocolsShapeIsAFaultAsync(
        HttpStatusCode status,
        string body)
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        deployment.Provider.Answers["/oidc/token"] = (status, body);

        Answer faulted = await arriving.SendAsync("GET", Local(Where(issued)));

        Faulted(faulted);
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-ERR-001 AC5, BFF-LOG-001 AC2 and chapter 09: an exchange answered a 400
    /// whose <c>error</c> is <c>invalid_grant</c> is a refusal: the browser is returned
    /// to the stored return address with the code of a session that is not there, and
    /// the <c>error</c> it carried inside is recorded at Information beside it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_001_AC5_AnExchangeRefusedInvalidGrantReturnsTheBrowserExpiredAndIsLoggedAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        deployment.Provider.Answers["/oidc/token"] =
            (HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");

        Answer refused = await arriving.SendAsync("GET", Local(Where(issued)));

        Assert.Equal(StatusCodes.Status302Found, refused.Status);
        Assert.Equal(Page + "?error=" + ErrorCodes.SessionExpired, refused.Location);
        Assert.Empty(refused.Body);
        Logged(deployment, "invalid_grant");
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-ERR-001 AC5 and chapter 09: every error of the exchange but a 400
    /// <c>invalid_grant</c> is a fault, a refusal of the deployment's own client among
    /// them: the browser is sent nowhere, no session is established and no refusal is
    /// recorded.
    /// </summary>
    /// <param name="status">The status the exchange is answered.</param>
    /// <param name="body">The body it is answered with.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"invalid_client"}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"invalid_request"}""")]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"unauthorized_client"}""")]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":"invalid_grant"}""")]
    public async Task BFF_ERR_001_AC5_AnErrorOfTheExchangeOtherThanInvalidGrantIsAFaultAsync(
        HttpStatusCode status,
        string body)
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        deployment.Provider.Answers["/oidc/token"] = (status, body);

        Answer faulted = await arriving.SendAsync("GET", Local(Where(issued)));

        Faulted(faulted);
        Assert.DoesNotContain(Inside, deployment.SignOnLog.Entries);
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-ERR-001 AC5 and chapter 09: an authorization response whose <c>error</c> is
    /// the provider's own failure (RFC 6749 section 4.1.2.1) is a fault, whatever
    /// else it carries: the browser is sent nowhere and no code is traded.
    /// </summary>
    /// <param name="answered">What the provider returned the browser with, beside its state.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("error=server_error")]
    [InlineData("error=temporarily_unavailable")]
    [InlineData("error=server_error&code=a-code")]
    public async Task BFF_ERR_001_AC5_AnAuthorizationResponseOfTheProvidersOwnFailureIsAFaultAsync(
        string answered)
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        var arriving = new Browser(deployment);

        _ = await arriving.SendAsync("GET", Start);

        Answer faulted = await arriving.SendAsync(
            "GET",
            "/auth/signon/return?state="
            + Uri.EscapeDataString(Parameter("?" + deployment.Provider.Carried[^1], "state"))
            + "&" + answered);

        Faulted(faulted);
        Assert.DoesNotContain(Inside, deployment.SignOnLog.Entries);
        Assert.DoesNotContain(
            deployment.Provider.Asked,
            asked => string.Equals(asked.AbsolutePath, "/oidc/token", StringComparison.Ordinal));
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-ERR-001 AC5, BFF-LOG-001 AC2 and chapter 09: an authorization response
    /// carrying any other <c>error</c> is a refusal: the browser is returned to the
    /// stored return address with the code of a session that is not there, and the
    /// <c>error</c> it carried inside is recorded at Information beside it.
    /// </summary>
    /// <param name="error">The error the provider returned the browser with.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("access_denied")]
    [InlineData("invalid_request")]
    [InlineData("interaction_required")]
    public async Task BFF_ERR_001_AC5_AnAuthorizationResponsesOtherErrorReturnsTheBrowserExpiredAndIsLoggedAsync(
        string error)
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        var arriving = new Browser(deployment);

        _ = await arriving.SendAsync("GET", Start);

        Answer refused = await arriving.SendAsync(
            "GET",
            "/auth/signon/return?state="
            + Uri.EscapeDataString(Parameter("?" + deployment.Provider.Carried[^1], "state"))
            + "&error=" + error);

        Assert.Equal(StatusCodes.Status302Found, refused.Status);
        Assert.Equal(Page + "?error=" + ErrorCodes.SessionExpired, refused.Location);
        Assert.Empty(refused.Body);
        Logged(deployment, error);
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-ERR-001 AC5, BFF-LOG-001 AC2 and chapter 09: a return whose session the
    /// library refuses to derive with a code of its own returns the browser to the
    /// stored return address with the code of a session that is not there, never with
    /// that code, which is recorded at Information beside it, and establishes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_001_AC5_ADerivationRefusedReturnsTheBrowserExpiredAndItsCodeIsLoggedAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        // The opening the derivation makes, the first after the exchange is answered,
        // is refused with a code that is not the ended record's.
        deployment.Provider.Answered = address =>
        {
            if (address.AbsolutePath is "/oidc/token")
            {
                deployment.Work.RefusesBegin = Error.From(ErrorCodes.Restricted);
            }
        };

        Answer refused = await arriving.SendAsync("GET", Local(Where(issued)));

        Assert.Null(deployment.Work.RefusesBegin);
        Assert.Equal(StatusCodes.Status302Found, refused.Status);
        Assert.Equal(Page + "?error=" + ErrorCodes.SessionExpired, refused.Location);
        Assert.Empty(refused.Body);
        Logged(deployment, ErrorCodes.Restricted.ToString());
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-SESS-006: the browser is sent back onto this application, so a return
    /// address naming another site is not one it is sent to.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AReturnAddressOffThisApplicationIsNotFollowedAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync(
            "GET",
            "/auth/signon?returnTo=" + Uri.EscapeDataString("https://attacker.test/collect"));
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));
        Answer established = await arriving.SendAsync("GET", Local(Where(issued)));

        Assert.Equal("/", Where(established));
    }

    private static string Start => "/auth/signon?returnTo=" + Uri.EscapeDataString(Page);

    // BFF-LOG-001: the entry that records what a refusal carried inside.
    private static (LogLevel Level, int EventId) Inside => (LogLevel.Information, 26);

    // BFF-LOG-001 AC2: one entry at Information names the code the browser was
    // returned with and, beside it, what the refusal carried inside.
    private static void Logged(Deployment deployment, string inside)
    {
        int at = deployment.SignOnLog.Entries.ToList().IndexOf(Inside);

        Assert.True(at >= 0, "No entry recorded what the refusal carried inside.");
        Assert.Equal(1, deployment.SignOnLog.Entries.Count(entry => entry == Inside));
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), deployment.SignOnLog.Carried[at]["Returned"]);
        Assert.Equal(inside, deployment.SignOnLog.Carried[at]["Code"]);
    }

    // BFF-ERR-002: the pipeline's answer to a fault, which sends the browser nowhere.
    private static void Faulted(Answer answered)
    {
        Assert.Equal(StatusCodes.Status500InternalServerError, answered.Status);
        Assert.Equal(ErrorCodes.SystemFault.ToString(), answered.Text("code"));
        Assert.Null(answered.Location);
    }

    private static string Where(Answer answered) =>
        answered.Location ?? throw new InvalidOperationException("The answer forwarded nowhere.");

    // The deployment is one origin here, so what a browser would follow across two
    // applications is followed as a path of the one it is talking to.
    private static string Local(string where) =>
        where.StartsWith("https://identity.example.test", StringComparison.Ordinal)
            ? where["https://identity.example.test".Length..]
            : where;

    private static string Parameter(string where, string name)
    {
        int at = where.IndexOf('?', StringComparison.Ordinal);

        foreach (string pair in where[(at + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);

            if (string.Equals(pair[..equals], name, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(pair[(equals + 1)..]);
            }
        }

        return string.Empty;
    }

    private static SessionId Spine(Deployment deployment)
    {
        foreach (Session held in deployment.Sessions.All)
        {
            if (held.Type is SessionType.Auth)
            {
                return held.Id;
            }
        }

        throw new InvalidOperationException("The deployment holds no record.");
    }

    // A browser at the authentication application holding the record every other
    // application's session stands on (AUTH-SESS-004).
    private static async Task<Browser> HolderAsync(Deployment deployment)
    {
        Flow.Prepare(deployment);

        return await Flow.SignedInAsync(deployment);
    }

    private static async Task RegisteredAsync(Deployment deployment) =>
        await deployment.Clients.AddAsync(
            new OidcClient(
                Client,
                Client,
                OidcClientKind.BrowserApplication,
                Return,
                ["openid"]),
            Encoding.UTF8.GetBytes(Secret),
            deployment.Clock.GetUtcNow(),
            TestContext.Current.CancellationToken);

    // The three legs one browser makes across two applications: this application
    // forwards it, the authentication application issues the code against the record
    // it holds, and this application trades the code on its own connection.
    private static async Task<Answer> SignedOnAsync(
        Deployment deployment,
        Browser arriving,
        Browser holder)
    {
        _ = deployment;

        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        return await arriving.SendAsync("GET", Local(Where(issued)));
    }
}
