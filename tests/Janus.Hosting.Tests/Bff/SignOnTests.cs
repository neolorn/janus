using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Oidc;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
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

    private const string Described = "a-description-never-logged";

    private const string Forged = "forged";

    // What every category the library logs under begins with.
    private static readonly string Library = typeof(Result).Namespace!.Split('.')[0] + ".";

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
    /// BFF-ERR-001 AC5 and chapter 09: a start by an application whose client is in no
    /// registry is the deployment's own state and so a fault: the browser is sent
    /// nowhere, no refusal is recorded and nothing is pushed or bound.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_001_AC5_AStartWhoseClientIsInNoRegistryIsAFaultAsync()
    {
        await using var deployment = new Deployment();

        Answer faulted = await new Browser(deployment).SendAsync(
            "GET",
            "/auth/signon?returnTo=" + Uri.EscapeDataString(Page + "#keys"));

        Faulted(faulted);
        Assert.Equal([Unregistered], deployment.SignOnLog.Entries);
        Assert.Empty(deployment.Provider.Asked);
        Assert.DoesNotContain(deployment.Contacts.All, contact => contact.SignOn is not null);
    }

    /// <summary>
    /// BFF-ERR-001 AC5 and chapter 09: a return to an application whose client has left
    /// the registry since the start is a fault: the browser is sent nowhere, no code is
    /// traded and no refusal is recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_001_AC5_AReturnWhoseClientIsInNoRegistryIsAFaultAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        deployment.Clients.Remove(Client);

        Answer faulted = await arriving.SendAsync("GET", Local(Where(issued)));

        Faulted(faulted);
        Assert.Equal([Unregistered], deployment.SignOnLog.Entries);
        Assert.DoesNotContain(
            deployment.Provider.Asked,
            asked => string.Equals(asked.AbsolutePath, "/oidc/token", StringComparison.Ordinal));
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-ERR-001 AC5 and chapter 09: a push whose client secret cannot be read is a
    /// fault: the browser is sent nowhere, nothing is pushed or bound and no refusal is
    /// recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_001_AC5_APushWhoseSecretCannotBeReadIsAFaultAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        deployment.Configuration.Unread = Settings.OidcAccessTokenLifetime.Key;

        Answer faulted = await new Browser(deployment).SendAsync("GET", Start);

        Faulted(faulted);
        Assert.Empty(deployment.SignOnLog.Entries);
        Assert.Contains(
            typeof(CodedFault).FullName + " " + ErrorCodes.StartupDeclarationMissing,
            Kept(deployment, faulted),
            StringComparison.Ordinal);
        Assert.Empty(deployment.Provider.Asked);
        Assert.DoesNotContain(deployment.Contacts.All, contact => contact.SignOn is not null);
    }

    /// <summary>
    /// BFF-ERR-001 AC5 and chapter 09: an exchange whose client secret cannot be read is
    /// a fault: the browser is sent nowhere, no code is traded and no refusal is
    /// recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_001_AC5_AnExchangeWhoseSecretCannotBeReadIsAFaultAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        deployment.Configuration.Unread = Settings.OidcAccessTokenLifetime.Key;

        Answer faulted = await arriving.SendAsync("GET", Local(Where(issued)));

        Faulted(faulted);
        Assert.Empty(deployment.SignOnLog.Entries);
        Assert.Contains(
            typeof(CodedFault).FullName + " " + ErrorCodes.StartupDeclarationMissing,
            Kept(deployment, faulted),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            deployment.Provider.Asked,
            asked => string.Equals(asked.AbsolutePath, "/oidc/token", StringComparison.Ordinal));
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-SESS-006 AC1: an identity token signed under a published key, issued by the
    /// authentication application to this client, unexpired and naming a live record,
    /// establishes the session. It is the token each case of the next test departs
    /// from in one respect.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_006_AC1_AnIdentityTokenThatHoldsUpEstablishesTheSessionAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        _ = await HolderAsync(deployment);

        var arriving = new Browser(deployment);

        _ = await arriving.SendAsync("GET", Start);

        deployment.Provider.Answers["/oidc/token"] =
            (HttpStatusCode.OK, await ExchangedAsync(deployment, fails: null));

        Answer established = await arriving.SendAsync("GET", Returned(deployment, "code=a-code"));

        Assert.Equal(StatusCodes.Status302Found, established.Status);
        Assert.Equal(Page, established.Location);
        _ = Assert.Single(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-ERR-001 AC5 and chapter 09: an identity token from the authentication
    /// application that does not hold up, in its signature under the published keys,
    /// its issuer, its audience or its expiry, is a fault: the browser is sent nowhere,
    /// no session is established and no refusal is recorded.
    /// </summary>
    /// <param name="fails">The one respect the token does not hold up in.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expiry")]
    public async Task BFF_ERR_001_AC5_AnIdentityTokenThatDoesNotHoldUpIsAFaultAsync(string fails)
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        _ = await HolderAsync(deployment);

        var arriving = new Browser(deployment);

        _ = await arriving.SendAsync("GET", Start);

        deployment.Provider.Answers["/oidc/token"] =
            (HttpStatusCode.OK, await ExchangedAsync(deployment, fails));

        Answer faulted = await arriving.SendAsync("GET", Returned(deployment, "code=a-code"));

        // BFF-ERR-002 AC2: the validation's own fault is kept beneath the sign-on's, by
        // its type, which names the check that failed, and never by its message.
        Type check = fails switch
        {
            "signature" => typeof(SecurityTokenInvalidSignatureException),
            "issuer" => typeof(SecurityTokenInvalidIssuerException),
            "audience" => typeof(SecurityTokenInvalidAudienceException),
            _ => typeof(SecurityTokenInvalidLifetimeException),
        };
        string kept = Kept(deployment, faulted);

        Faulted(faulted);
        Assert.Empty(deployment.SignOnLog.Entries);
        Assert.Contains(typeof(InvalidOperationException).FullName!, kept, StringComparison.Ordinal);
        Assert.Contains("inner " + check.FullName, kept, StringComparison.Ordinal);
        Assert.DoesNotContain("IDX", kept, StringComparison.Ordinal);
        Assert.DoesNotContain("did not hold up", kept, StringComparison.Ordinal);
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-ERR-001 AC5, BFF-SESS-006 and chapter 09: an identity token that holds up in
    /// its signature, its issuer, its audience and its expiry and carries no session
    /// identifier in <c>sid</c>, the claim absent or holding no identifier, is a fault:
    /// the browser is sent nowhere, no session is established, no refusal is recorded,
    /// and one entry at Error names it, the fault carrying no code.
    /// </summary>
    /// <param name="sid">What the token carries in the claim, or nothing where it carries none.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a-session-that-is-no-identifier")]
    public async Task BFF_ERR_001_AC5_AnIdentityTokenCarryingNoSessionIdentifierIsAFaultAsync(string? sid)
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        _ = await HolderAsync(deployment);

        var arriving = new Browser(deployment);

        _ = await arriving.SendAsync("GET", Start);

        deployment.Provider.Answers["/oidc/token"] =
            (HttpStatusCode.OK, await IssuedAsync(deployment, fails: null, sid));

        Answer faulted = await arriving.SendAsync("GET", Returned(deployment, "code=a-code"));

        Faulted(faulted);
        Assert.Equal([Unnamed], deployment.SignOnLog.Entries);
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-ERR-001 AC5 and chapter 09: an exchange after which the authentication
    /// application's published keys cannot be read is a fault: the browser is sent
    /// nowhere, no session is established and no refusal is recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_001_AC5_PublishedKeysThatCannotBeReadAreAFaultAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        // The cadence the keys are read under cannot be read once the exchange has
        // been answered, so the token came back and the keys to judge it do not.
        deployment.Provider.Answered = address =>
        {
            if (address.AbsolutePath is "/oidc/token")
            {
                deployment.Configuration.Unread = Settings.TokenSigningRotation.Key;
            }
        };

        Answer faulted = await arriving.SendAsync("GET", Local(Where(issued)));

        Assert.Equal(Settings.TokenSigningRotation.Key, deployment.Configuration.Unread);
        Faulted(faulted);
        Assert.Empty(deployment.SignOnLog.Entries);
        Assert.Contains(
            typeof(CodedFault).FullName + " " + ErrorCodes.StartupDeclarationMissing,
            Kept(deployment, faulted),
            StringComparison.Ordinal);
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-ERR-001 AC5 and BFF-ERR-002 AC2: a client secret that cannot be read, here
    /// one whose replacement is due and whose unit of work cannot begin, makes a fault
    /// carrying the code and the details its read answered, which the fault's log entry
    /// keeps under the correlation identifier, and no other entry of the sign-on.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_002_AC2_ASecretThatCannotBeReadFaultsWithTheCodeAndDetailsItsReadAnsweredAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        deployment.Clock.Advance(Settings.TokenSigningRotation.Default);

        // The browser is issued its pre-authentication session first, so the opening
        // refused next is the one the secret's replacement makes.
        var arriving = new Browser(deployment);

        _ = await arriving.SendAsync("GET", "/auth/session");

        deployment.Work.RefusesBegin = Unbegun;

        Answer faulted = await arriving.SendAsync("GET", Start);

        Assert.Null(deployment.Work.RefusesBegin);
        Faulted(faulted);
        Assert.Empty(deployment.SignOnLog.Entries);
        Assert.Contains(
            typeof(CodedFault).FullName + " " + ErrorCodes.SystemFault + " unit=\"begin\"",
            Kept(deployment, faulted),
            StringComparison.Ordinal);
        Assert.Empty(deployment.Provider.Asked);
    }

    /// <summary>
    /// BFF-ERR-001 AC5 and BFF-ERR-002 AC2: the authentication application's published
    /// keys that cannot be read, here a set whose change is due and whose unit of work
    /// cannot begin, make a fault carrying the code and the details their read
    /// answered, which the fault's log entry keeps under the correlation identifier,
    /// and no other entry of the sign-on.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_002_AC2_PublishedKeysThatCannotBeReadFaultWithTheCodeAndDetailsTheirReadAnsweredAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        // The set's change falls due once the exchange has been answered, and the
        // opening it makes, the first after that answer, is refused.
        deployment.Provider.Answered = address =>
        {
            if (address.AbsolutePath is "/oidc/token")
            {
                deployment.Clock.Advance(Settings.TokenSigningRotation.Default);
                deployment.Work.RefusesBegin = Unbegun;
            }
        };

        Answer faulted = await arriving.SendAsync("GET", Local(Where(issued)));

        Assert.Null(deployment.Work.RefusesBegin);
        Faulted(faulted);
        Assert.Empty(deployment.SignOnLog.Entries);
        Assert.Contains(
            typeof(CodedFault).FullName + " " + ErrorCodes.SystemFault + " unit=\"begin\"",
            Kept(deployment, faulted),
            StringComparison.Ordinal);
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-ERR-001 AC5, chapter 09 and chapter 10 section 6: a derivation that fails
    /// with a code whose row names a fault, or that no row names, is a fault as it is
    /// everywhere: the browser is sent nowhere, no refusal is recorded and no session
    /// is established.
    /// </summary>
    /// <param name="code">The code the derivation fails with.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("system.fault")]
    [InlineData("authz.policy.unregistered")]
    [InlineData("authz.derivation.sourcesmissing")]
    [InlineData("auth.session.unlisted")]
    public async Task BFF_ERR_001_AC5_ADerivationFailingWithAFaultsCodeIsAFaultAsync(string code)
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));

        deployment.Provider.Answered = address =>
        {
            if (address.AbsolutePath is "/oidc/token")
            {
                deployment.Work.RefusesBegin = Error.From(ErrorCode.Parse(code));
            }
        };

        Answer faulted = await arriving.SendAsync("GET", Local(Where(issued)));

        Assert.Null(deployment.Work.RefusesBegin);
        Faulted(faulted);
        Assert.DoesNotContain(Beside, deployment.SignOnLog.Entries);
        Assert.DoesNotContain(Inside, deployment.SignOnLog.Entries);
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-ERR-001 AC5, BFF-LOG-001 AC2 and chapter 09: a return carrying neither a
    /// code nor an error is a refusal: the browser is returned to the stored return
    /// address with the code of a session that is not there, and the code of a request
    /// that cannot be read is recorded once at Information as what it carried inside.
    /// </summary>
    /// <param name="answered">What the return carried beside its state.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("code=")]
    [InlineData("code=&error=")]
    public async Task BFF_ERR_001_AC5_AReturnCarryingNeitherCodeNorErrorReturnsTheBrowserExpiredAndIsLoggedAsync(
        string answered)
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        var arriving = new Browser(deployment);

        _ = await arriving.SendAsync("GET", Start);

        Answer refused = await arriving.SendAsync("GET", Returned(deployment, answered));

        Assert.Equal(StatusCodes.Status302Found, refused.Status);
        Assert.Equal(Page + "?error=" + ErrorCodes.SessionExpired, refused.Location);
        Assert.Empty(refused.Body);
        Logged(deployment, ErrorCodes.RequestMalformed.ToString());
        Assert.DoesNotContain(
            deployment.Provider.Asked,
            asked => string.Equals(asked.AbsolutePath, "/oidc/token", StringComparison.Ordinal));
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-ERR-001 AC5, BFF-LOG-001 AC2 and chapter 09: an identity token whose session
    /// has ended since it was issued is a refusal: the browser is returned to the
    /// stored return address with the code of a session that is not there, and that
    /// same code is recorded once at Information as what the refusal carried inside.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_001_AC5_ATokenWhoseSessionHasEndedReturnsTheBrowserExpiredAndIsLoggedAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);
        Answer forwarded = await arriving.SendAsync("GET", Start);
        Answer issued = await holder.SendAsync("GET", Local(Where(forwarded)));
        SessionId spine = Spine(deployment);

        // The record ends before the return and the token names it all the same, as
        // one issued while the record was live and read after it ended does.
        await deployment.Sessions.EndSpineAsync(
            spine,
            deployment.Clock.GetUtcNow(),
            TestContext.Current.CancellationToken);

        deployment.Provider.Answers["/oidc/token"] =
            (HttpStatusCode.OK, await ExchangedAsync(deployment, fails: null, spine));

        Answer refused = await arriving.SendAsync("GET", Local(Where(issued)));

        Assert.Equal(StatusCodes.Status302Found, refused.Status);
        Assert.Equal(Page + "?error=" + ErrorCodes.SessionExpired, refused.Location);
        Assert.Empty(refused.Body);
        Logged(deployment, ErrorCodes.SessionExpired.ToString());
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp && held.EndedAt is null);
    }

    /// <summary>
    /// BFF-ERR-001 AC5, BFF-LOG-001 AC2 and chapter 09: an identity token whose
    /// <c>sid</c> is an identifier naming a session that is not found is a refusal, as
    /// one naming a session that has ended is: the browser is returned to the stored
    /// return address with the code of a session that is not there, and that same code
    /// is recorded once at Information as what the refusal carried inside.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_001_AC5_ATokenWhoseSessionIsNotFoundReturnsTheBrowserExpiredAndIsLoggedAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        _ = await HolderAsync(deployment);

        var arriving = new Browser(deployment);

        _ = await arriving.SendAsync("GET", Start);

        deployment.Provider.Answers["/oidc/token"] = (
            HttpStatusCode.OK,
            await ExchangedAsync(
                deployment,
                fails: null,
                new SessionId(Guid.CreateVersion7(deployment.Clock.GetUtcNow()))));

        Answer refused = await arriving.SendAsync("GET", Returned(deployment, "code=a-code"));

        Assert.Equal(StatusCodes.Status302Found, refused.Status);
        Assert.Equal(Page + "?error=" + ErrorCodes.SessionExpired, refused.Location);
        Assert.Empty(refused.Body);
        Assert.Equal([Inside], deployment.SignOnLog.Entries);
        Logged(deployment, ErrorCodes.SessionExpired.ToString());
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
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
        Assert.DoesNotContain(Beside, deployment.SignOnLog.Entries);
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
        Assert.DoesNotContain(Beside, deployment.SignOnLog.Entries);
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
    /// BFF-ERR-001 AC5, BFF-SESS-006 and chapter 09: an authorization response's
    /// <c>error</c> reaches the return through the browser, so one that does not read
    /// as RFC 6749 Appendix A.7 defines an error, or is longer than 64 characters, is a
    /// refusal whatever else the return carries: the browser is returned to the stored
    /// return address with the code of a session that is not there, the code of a
    /// request that cannot be read is recorded once at Information as what it carried
    /// inside, no code is traded, and no entry the library writes carries the value.
    /// </summary>
    /// <param name="unread">The one respect the value does not read in.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("line break")]
    [InlineData("control character")]
    [InlineData("quotation mark")]
    [InlineData("backslash")]
    [InlineData("delete")]
    [InlineData("outside ASCII")]
    [InlineData("empty")]
    [InlineData("65 characters")]
    public async Task BFF_ERR_001_AC5_AnAuthorizationResponsesErrorThatDoesNotReadReturnsTheBrowserExpiredAndIsInNoEntryAsync(
        string unread)
    {
        string error = unread switch
        {
            "line break" => Forged + "\r\nentry",
            "control character" => Forged + "\u0001entry",
            "quotation mark" => Forged + "\"entry",
            "backslash" => Forged + "\\entry",
            "delete" => Forged + "\u007Fentry",
            "outside ASCII" => Forged + "éentry",
            "empty" => string.Empty,
            _ => Forged + new string('e', 65 - Forged.Length),
        };

        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        var arriving = new Browser(deployment);

        _ = await arriving.SendAsync("GET", Start);

        Answer refused = await arriving.SendAsync(
            "GET",
            Returned(deployment, "error=" + Uri.EscapeDataString(error) + "&code=a-code"));

        Assert.Equal(StatusCodes.Status302Found, refused.Status);
        Assert.Equal(Page + "?error=" + ErrorCodes.SessionExpired, refused.Location);
        Assert.Empty(refused.Body);
        Assert.Equal([Inside], deployment.SignOnLog.Entries);
        Logged(deployment, ErrorCodes.RequestMalformed.ToString());
        Assert.DoesNotContain(
            deployment.SignOnLog.Carried.SelectMany(entry => entry.Values),
            value => value is not null && value.Contains(Forged, StringComparison.Ordinal));
        Assert.DoesNotContain(
            deployment.Logs.Lines,
            line => line.StartsWith(Library, StringComparison.Ordinal)
                && line.Contains(Forged, StringComparison.Ordinal));
        Assert.DoesNotContain(
            deployment.Provider.Asked,
            asked => string.Equals(asked.AbsolutePath, "/oidc/token", StringComparison.Ordinal));
        Assert.DoesNotContain(deployment.Sessions.All, held => held.Type is SessionType.PerApp);
    }

    /// <summary>
    /// BFF-ERR-001 AC5 and chapter 09: an authorization response's <c>error</c> of
    /// exactly 64 characters, each one RFC 6749 Appendix A.7 allows and the outermost of
    /// its ranges among them, reads: it is taken as the provider wrote it, a refusal
    /// recorded at Information with that value as what it carried inside.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ERR_001_AC5_AnAuthorizationResponsesErrorOf64CharactersThatReadsIsLoggedAsync()
    {
        const string outermost = " !#[]~";
        string error = outermost + new string('e', 64 - outermost.Length);

        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        var arriving = new Browser(deployment);

        _ = await arriving.SendAsync("GET", Start);

        Answer refused = await arriving.SendAsync(
            "GET",
            Returned(deployment, "error=" + Uri.EscapeDataString(error)));

        Assert.Equal(64, error.Length);
        Assert.Equal(StatusCodes.Status302Found, refused.Status);
        Assert.Equal(Page + "?error=" + ErrorCodes.SessionExpired, refused.Location);
        Assert.Empty(refused.Body);
        Assert.Equal([Inside], deployment.SignOnLog.Entries);
        Logged(deployment, error);
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
    /// BFF-LOG-001 AC2 and BFF-ERR-001 AC5: a refused authorization response is logged
    /// once, at Information, by the code it returns and the one it carried inside, and
    /// by no other entry.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_LOG_001_AC2_ARefusedAuthorizationResponseIsLoggedOnceAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        var arriving = new Browser(deployment);

        _ = await arriving.SendAsync("GET", Start);

        Answer refused = await arriving.SendAsync("GET", Returned(deployment, "error=access_denied"));

        Assert.Equal(Page + "?error=" + ErrorCodes.SessionExpired, refused.Location);
        Assert.Equal([Inside], deployment.SignOnLog.Entries);
        Logged(deployment, "access_denied");
    }

    /// <summary>
    /// BFF-LOG-001 AC2 and BFF-ERR-001 AC5: a refused exchange is logged once, at
    /// Information, by the code it returns and the one it carried inside, and by no
    /// other entry.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_LOG_001_AC2_ARefusedExchangeIsLoggedOnceAsync()
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

        Assert.Equal(Page + "?error=" + ErrorCodes.SessionExpired, refused.Location);
        Assert.Equal([Inside], deployment.SignOnLog.Entries);
        Logged(deployment, "invalid_grant");
    }

    /// <summary>
    /// BFF-ERR-001 AC5 and chapter 09: a fault the push answered adds, beside the
    /// fault's own entry, one entry at Error carrying the status the push read and the
    /// <c>error</c> it read, where it read one, and nothing else of the answer, never
    /// its description.
    /// </summary>
    /// <param name="status">The status the push is answered.</param>
    /// <param name="body">The body it is answered with.</param>
    /// <param name="error">The error the body names, where it names one.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":"invalid_client","error_description":"a-description-never-logged","error_uri":"https://identity.example.test/a-description-never-logged"}""", "invalid_client")]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"invalid_request","error_description":"a-description-never-logged"}""", "invalid_request")]
    [InlineData(HttpStatusCode.InternalServerError, """{"error":"server_error","error_description":"a-description-never-logged"}""", "server_error")]
    [InlineData(HttpStatusCode.ServiceUnavailable, """{"error_description":"a-description-never-logged"}""", null)]
    [InlineData(HttpStatusCode.BadGateway, "<html>a-description-never-logged</html>", null)]
    [InlineData(HttpStatusCode.Created, """{"expires_in":60}""", null)]
    public async Task BFF_ERR_001_AC5_AFaultThePushAnsweredLogsTheStatusAndTheErrorReadAsync(
        HttpStatusCode status,
        string body,
        string? error)
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        deployment.Provider.Answers["/oidc/par"] = (status, body);

        Answer faulted = await new Browser(deployment).SendAsync("GET", Start);

        Faulted(faulted);
        LoggedBeside(deployment, (int)status, error);
    }

    /// <summary>
    /// BFF-ERR-001 AC5 and chapter 09: a fault the exchange answered adds, beside the
    /// fault's own entry, one entry at Error carrying the status the exchange read and
    /// the <c>error</c> it read, where it read one, and nothing else of the answer,
    /// never its description.
    /// </summary>
    /// <param name="status">The status the exchange is answered.</param>
    /// <param name="body">The body it is answered with.</param>
    /// <param name="error">The error the body names, where it names one.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":"invalid_client","error_description":"a-description-never-logged","error_uri":"https://identity.example.test/a-description-never-logged"}""", "invalid_client")]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":"invalid_grant","error_description":"a-description-never-logged"}""", "invalid_grant")]
    [InlineData(HttpStatusCode.BadRequest, """{"error":"unauthorized_client","error_description":"a-description-never-logged"}""", "unauthorized_client")]
    [InlineData(HttpStatusCode.InternalServerError, """{"error":"server_error","error_description":"a-description-never-logged"}""", "server_error")]
    [InlineData(HttpStatusCode.ServiceUnavailable, """{"error_description":"a-description-never-logged"}""", null)]
    [InlineData(HttpStatusCode.BadGateway, "<html>a-description-never-logged</html>", null)]
    [InlineData(HttpStatusCode.OK, """{"access_token":"a-description-never-logged"}""", null)]
    public async Task BFF_ERR_001_AC5_AFaultTheExchangeAnsweredLogsTheStatusAndTheErrorReadAsync(
        HttpStatusCode status,
        string body,
        string? error)
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
        LoggedBeside(deployment, (int)status, error);
    }

    /// <summary>
    /// BFF-ERR-001 AC5 and chapter 09: a fault the authorization response answered
    /// adds, beside the fault's own entry, one entry at Error carrying the
    /// <c>error</c> it read and nothing else it carried, never its description; no
    /// status was read, so none is carried.
    /// </summary>
    /// <param name="error">The error the provider returned the browser with.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("server_error")]
    [InlineData("temporarily_unavailable")]
    public async Task BFF_ERR_001_AC5_AFaultTheAuthorizationResponseAnsweredLogsTheErrorReadAsync(string error)
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        var arriving = new Browser(deployment);

        _ = await arriving.SendAsync("GET", Start);

        Answer faulted = await arriving.SendAsync(
            "GET",
            Returned(
                deployment,
                "error=" + error
                + "&error_description=" + Described
                + "&error_uri=" + Uri.EscapeDataString("https://identity.example.test/" + Described)));

        Faulted(faulted);
        LoggedBeside(deployment, status: null, error);
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

    // BFF-ERR-001 AC5: the entry a fault adds, beside its own, where the push, the
    // exchange or the authorization response answered.
    private static (LogLevel Level, int EventId) Beside => (LogLevel.Error, 27);

    // BFF-ERR-001 AC5: the entry that names a client in no registry, whose fault
    // carries no code.
    private static (LogLevel Level, int EventId) Unregistered => (LogLevel.Error, 12);

    // BFF-ERR-001 AC5: the entry that names an identity token carrying no session
    // identifier, whose fault carries no code.
    private static (LogLevel Level, int EventId) Unnamed => (LogLevel.Error, 28);

    // BFF-LOG-001 AC2: one entry at Information names the code the browser was
    // returned with and, beside it, what the refusal carried inside.
    private static void Logged(Deployment deployment, string inside)
    {
        int at = deployment.SignOnLog.Entries.ToList().IndexOf(Inside);

        Assert.True(at >= 0, "No entry recorded what the refusal carried inside.");
        Assert.Equal(1, deployment.SignOnLog.Entries.Count(entry => entry == Inside));
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), deployment.SignOnLog.Carried[at]["Returned"]);
        Assert.Equal(inside, deployment.SignOnLog.Carried[at]["Code"]);
        Assert.DoesNotContain(Beside, deployment.SignOnLog.Entries);
        Assert.DoesNotContain(deployment.SignOnLog.Entries, entry => entry.EventId is 10 or 11);
    }

    // BFF-ERR-001 AC5: one entry at Error carries the status and the error a fault
    // read where the authentication application answered, and nothing else of the
    // answer: a description reaches no log, this one or the deployment's.
    private static void LoggedBeside(Deployment deployment, int? status, string? error)
    {
        int at = deployment.SignOnLog.Entries.ToList().IndexOf(Beside);

        Assert.True(at >= 0, "No entry recorded the status and the error the fault read.");
        Assert.Equal(1, deployment.SignOnLog.Entries.Count(entry => entry == Beside));

        IReadOnlyDictionary<string, string?> carried = deployment.SignOnLog.Carried[at];

        Assert.Equal(
            ["CorrelationId", "Error", "Status", "{OriginalFormat}"],
            carried.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(status?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, carried["Status"]);
        Assert.Equal(error ?? string.Empty, carried["Error"]);
        Assert.DoesNotContain(Inside, deployment.SignOnLog.Entries);
        Assert.DoesNotContain(
            deployment.SignOnLog.Carried.SelectMany(entry => entry.Values),
            value => value is not null && value.Contains(Described, StringComparison.Ordinal));
        Assert.DoesNotContain(
            deployment.Logs.Lines,
            line => line.Contains(Described, StringComparison.Ordinal));
    }

    // A unit of work that could not begin, as a store answers one: a code and the
    // details that name what broke.
    private static Error Unbegun =>
        Error.From(ErrorCodes.SystemFault, "unit", JsonSerializer.SerializeToElement("begin"));

    // BFF-ERR-002 AC2: the one entry a fault is kept in the log as, found by the
    // correlation identifier its answer carries.
    private static string Kept(Deployment deployment, Answer faulted)
    {
        string correlation = faulted.Text("correlationId");

        return Assert.Single(
            deployment.Logs.Lines,
            line => line.Contains(correlation, StringComparison.Ordinal)
                && line.Contains(ErrorCodes.SystemFault.ToString(), StringComparison.Ordinal));
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

    // The return as the authentication application sends the browser back: the state
    // the push carried, and what a test has it carry beside.
    private static string Returned(Deployment deployment, string answered) =>
        "/auth/signon/return?state="
        + Uri.EscapeDataString(Parameter("?" + deployment.Provider.Carried[^1], "state"))
        + "&" + answered;

    // What the token endpoint answers an exchange with: an identity token made as the
    // authentication application makes one, signed under its current key, issued by
    // it to this client, unexpired and naming the record, or failing in the one
    // respect a test names.
    private static async Task<string> ExchangedAsync(
        Deployment deployment,
        string? fails,
        SessionId? record = null) =>
        await IssuedAsync(deployment, fails, (record ?? Spine(deployment)).Value.ToString());

    // The same answer with the token carrying in `sid` what a test names, or no such
    // claim where it names nothing.
    private static async Task<string> IssuedAsync(Deployment deployment, string? fails, string? sid)
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;

        // The signing keys are made at the first read of the key set.
        _ = await new Browser(deployment).SendAsync("GET", "/oidc/jwks");

        SigningKey current = Assert.Single(
            await deployment.Keys.HeldAsync(cancellation),
            key => key.IsCurrent);
        byte[] material = await deployment.Keys.PrivateKeyAsync(current.KeyId, cancellation)
            ?? throw new InvalidOperationException("The current signing key holds no private key.");

        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        if (fails is not "signature")
        {
            signer.ImportPkcs8PrivateKey(material, out _);
        }

        DateTimeOffset now = deployment.Clock.GetUtcNow();
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["sub"] = Guid.CreateVersion7(now).ToString(),
            ["iat"] = now.AddMinutes(-10).ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(fails is "expiry" ? -1 : 5).ToUnixTimeSeconds(),
        };

        if (sid is not null)
        {
            claims["sid"] = sid;
        }

        string identity = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = fails is "issuer" ? "https://another.example.test" : "https://identity.example.test",
                Audience = fails is "audience" ? "another-application" : Client,
                Claims = claims,
                SigningCredentials = new SigningCredentials(
                    new ECDsaSecurityKey(signer)
                    {
                        KeyId = current.KeyId,
                        CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
                    },
                    current.Algorithm),
            });

        return JsonSerializer.Serialize(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["id_token"] = identity });
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
