using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Oidc;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// The provider as a relying party meets it: the document it advertises, the keys it
/// publishes, the code a live browser is sent back with, and the tokens the exchange
/// hands over the back channel (AUTH-OIDC-001 to AUTH-OIDC-004, AUTH-SESS-012,
/// API-REDIR-001, BFF-MACH-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class OidcFlowTests
{
    private const string Second = "another-browser-app";
    private const string Prefix = "/identity/v1";

    /// <summary>
    /// AUTH-OIDC-001 AC1: the discovery document names the endpoints the deployment
    /// answers and the flows it admits, and nothing it does not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AC1_TheDocumentNamesTheEndpointsAndTheFlowsAsync()
    {
        await using var deployment = new Deployment();

        Answer answered = await new Machine(deployment)
            .GetAsync("/.well-known/openid-configuration", bearer: string.Empty);

        JsonElement document = answered.Json();

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.EndsWith(
            "/oidc/authorize",
            document.GetProperty("authorization_endpoint").GetString(),
            StringComparison.Ordinal);
        Assert.EndsWith(
            "/oidc/token",
            document.GetProperty("token_endpoint").GetString(),
            StringComparison.Ordinal);
        Assert.EndsWith(
            "/oidc/userinfo",
            document.GetProperty("userinfo_endpoint").GetString(),
            StringComparison.Ordinal);
        Assert.EndsWith(
            "/oidc/jwks",
            document.GetProperty("jwks_uri").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(["code"], Listed(document, "response_types_supported"));
        Assert.Contains("S256", Listed(document, "code_challenge_methods_supported"));
    }

    /// <summary>
    /// INT-MAIL-004 AC2: the discovery document advertises no introspection endpoint;
    /// a relying party validates a token against the key set and userinfo, which it
    /// does advertise.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_004_AC2_TheDocumentAdvertisesNoIntrospectionAsync()
    {
        await using var deployment = new Deployment();

        JsonElement document = (await new Machine(deployment)
                .GetAsync("/.well-known/openid-configuration", bearer: string.Empty))
            .Json();

        Assert.DoesNotContain(
            document.EnumerateObject(),
            member => member.Name.Contains("introspection", StringComparison.Ordinal));
        Assert.True(document.TryGetProperty("jwks_uri", out _));
        Assert.True(document.TryGetProperty("userinfo_endpoint", out _));
    }

    /// <summary>
    /// AUTH-KEY-001 AC4: what the key set serves carries the configured algorithm and
    /// the public half only, named so that a token's header resolves to one of them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC4_TheKeySetCarriesTheConfiguredAlgorithmAsync()
    {
        await using var deployment = new Deployment();

        Answer answered = await new Machine(deployment).GetAsync("/oidc/jwks", bearer: string.Empty);

        JsonElement key = answered.Json().GetProperty("keys")[0];

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.Equal("EC", key.GetProperty("kty").GetString());
        Assert.Equal("ES256", key.GetProperty("alg").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
        Assert.False(key.TryGetProperty("d", out _));
        Assert.Equal(1, deployment.Keys.Count);
        Assert.NotEmpty(key.GetProperty("kid").GetString()!);
    }

    /// <summary>
    /// AUTH-KEY-001 AC2, D-162: what signs a token is the key the deployment's own
    /// store holds, so the header of an issued token names a key the set publishes and
    /// no key of the server's own exists to sign with.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_KEY_001_AC2_TheKeyThatSignsIsTheKeyTheSetPublishesAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);
        Answer exchanged = await machine.PostAsync(
            "/oidc/token",
            RelyingParty.Code(code, RelyingParty.Application));
        JsonElement published = (await machine.GetAsync("/oidc/jwks", bearer: string.Empty))
            .Json()
            .GetProperty("keys")[0];

        Assert.Equal(StatusCodes.Status200OK, exchanged.Status);
        Assert.Equal(1, deployment.Keys.Count);
        Assert.Equal(
            published.GetProperty("kid").GetString(),
            Header(exchanged.Text("access_token"), "kid"));
    }

    /// <summary>
    /// AUTH-SESS-012 AC1 and AC2, AUTH-OIDC-002 AC1: a browser holding a live session
    /// is sent back to the client with a code and nothing else, without being asked
    /// anything.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC1_ALiveSessionIsSentBackWithACodeAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        Answer answered = await browser.SendAsync(
            "GET",
            await RelyingParty.AuthorizeAsync(deployment, RelyingParty.Application, silent: true));

        Assert.Equal(StatusCodes.Status302Found, answered.Status);
        Assert.StartsWith(
            RelyingParty.Destination + "?",
            RelyingParty.Where(answered),
            StringComparison.Ordinal);
        Assert.NotEmpty(RelyingParty.Returned(answered, "code"));
        Assert.Equal("the-state", RelyingParty.Returned(answered, "state"));
    }

    /// <summary>
    /// AUTH-SESS-012 AC3: a silent request from a browser holding no session is
    /// answered at the client's destination with what is missing, and no screen.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC3_ASilentRequestWithoutASessionSaysSoAsync()
    {
        await using var deployment = new Deployment();

        await RelyingParty.RegisteredAsync(deployment);

        Answer answered = await new Browser(deployment)
            .SendAsync(
                "GET",
                await RelyingParty.AuthorizeAsync(deployment, RelyingParty.Application, silent: true));

        Assert.Equal(StatusCodes.Status302Found, answered.Status);
        Assert.Equal("login_required", RelyingParty.Returned(answered, "error"));
    }

    /// <summary>
    /// AUTH-SESS-012 AC3: a request that is not silent is forwarded to the sign-in
    /// screen the deployment declared, and is never told `login_required`, which is
    /// what `prompt=none` asked to be told and it did not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC3_AnInteractiveRequestReachesTheSignInScreenAsync()
    {
        await using var deployment = new Deployment(
            signIn: new AuthenticationAddresses(
                "https://identity.example.test/signin",
                "https://identity.example.test"));

        await RelyingParty.RegisteredAsync(deployment);

        Answer answered = await new Browser(deployment)
            .SendAsync(
                "GET",
                await RelyingParty.AuthorizeAsync(deployment, RelyingParty.Application, silent: false));

        Assert.Equal(StatusCodes.Status302Found, answered.Status);
        Assert.StartsWith(
            "https://identity.example.test/signin",
            RelyingParty.Where(answered),
            StringComparison.Ordinal);

        Assert.DoesNotContain("login_required", RelyingParty.Where(answered), StringComparison.Ordinal);
    }

    /// <summary>
    /// AUTH-SESS-012 AC4: a code is exchanged once, and presenting it again is refused
    /// as a grant the provider no longer holds.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC4_ACodeIsExchangedOnceAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);

        Answer first = await machine.PostAsync("/oidc/token", RelyingParty.Code(code, RelyingParty.Application));
        Answer again = await machine.PostAsync("/oidc/token", RelyingParty.Code(code, RelyingParty.Application));

        Assert.Equal(StatusCodes.Status200OK, first.Status);
        Assert.Equal(StatusCodes.Status400BadRequest, again.Status);
        Assert.Equal("invalid_grant", again.Text("error"));
        Assert.False(again.Json().TryGetProperty("access_token", out _));
    }

    /// <summary>
    /// AUTH-SESS-012 AC4: a code lives for `oidc.code.lifetime` on the deployment's
    /// clock, so one exchanged inside it is taken and one exchanged after it is refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC4_ACodeLapsesAtTheCodeLifetimeAsync()
    {
        await using var deployment = new Deployment();

        deployment.Configuration.Set(Settings.OidcCodeLifetime, TimeSpan.FromSeconds(30));

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string kept = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);
        string lapsed = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);

        deployment.Clock.Advance(TimeSpan.FromSeconds(29));

        Answer inTime = await machine.PostAsync("/oidc/token", RelyingParty.Code(kept, RelyingParty.Application));

        deployment.Clock.Advance(TimeSpan.FromSeconds(2));

        Answer late = await machine.PostAsync("/oidc/token", RelyingParty.Code(lapsed, RelyingParty.Application));

        Assert.Equal(StatusCodes.Status200OK, inTime.Status);
        Assert.Equal(StatusCodes.Status400BadRequest, late.Status);
        Assert.Equal("invalid_grant", late.Text("error"));
        Assert.False(late.Json().TryGetProperty("access_token", out _));
    }

    /// <summary>
    /// AUTH-SESS-012 AC4: a code issued to one registered client is refused when another
    /// registered client presents it, though that client authenticates with its own
    /// secret and carries the verifier.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC4_ACodeIsRefusedToAnotherClientAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);

        await deployment.Clients.AddAsync(
            new OidcClient(
                Second,
                Second,
                OidcClientKind.BrowserApplication,
                RelyingParty.Destination,
                ["openid", "email"]),
            Encoding.UTF8.GetBytes(RelyingParty.Secret),
            deployment.Clock.GetUtcNow(),
            TestContext.Current.CancellationToken);

        var machine = new Machine(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);

        Answer elsewhere = await machine.PostAsync("/oidc/token", RelyingParty.Code(code, Second));

        Assert.Equal(StatusCodes.Status400BadRequest, elsewhere.Status);
        Assert.Equal("invalid_grant", elsewhere.Text("error"));
        Assert.False(elsewhere.Json().TryGetProperty("access_token", out _));
    }

    /// <summary>
    /// AUTH-SESS-012 AC4: a code presented with a verifier that is not the one its
    /// challenge was made from is refused as a grant that does not match.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC4_ACodeIsRefusedWithAnotherVerifierAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);

        Answer refused = await new Machine(deployment).PostAsync(
            "/oidc/token",
            RelyingParty.With(
                RelyingParty.Code(code, RelyingParty.Application),
                "code_verifier",
                "another-verifier-of-at-least-forty-three-characters-long"));

        Assert.Equal(StatusCodes.Status400BadRequest, refused.Status);
        Assert.Equal("invalid_grant", refused.Text("error"));
        Assert.False(refused.Json().TryGetProperty("access_token", out _));
    }

    /// <summary>
    /// AUTH-SESS-012 AC4: a code presented with no verifier at all is refused, as a
    /// request missing a parameter every exchange must carry (RFC 6749 section 5.2).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC4_ACodeIsRefusedWithoutAVerifierAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);

        Answer refused = await new Machine(deployment).PostAsync(
            "/oidc/token",
            RelyingParty.With(RelyingParty.Code(code, RelyingParty.Application), "code_verifier", null));

        Assert.Equal(StatusCodes.Status400BadRequest, refused.Status);
        Assert.Equal("invalid_request", refused.Text("error"));
        Assert.False(refused.Json().TryGetProperty("access_token", out _));
    }

    /// <summary>
    /// AUTH-OIDC-006 AC1 and API-REDIR-001 AC4 (D-166, 145): a pushed request naming a
    /// destination that is not the client's registered one is an authorization request
    /// that fails, refused <c>invalid_request</c> with no description and no reference,
    /// and the attempt is recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC1_APushedRequestNamingAnUnregisteredDestinationIsRefusedAsync()
    {
        await using var deployment = new Deployment();

        _ = await RelyingParty.PreparedAsync(deployment);

        Answer pushed = await RelyingParty.PushAsync(
            deployment,
            RelyingParty.Application,
            silent: true,
            "https://attacker.test/collect",
            "openid email");

        Assert.Equal(StatusCodes.Status400BadRequest, pushed.Status);
        Assert.Equal("invalid_request", pushed.Text("error"));
        Assert.False(pushed.Json().TryGetProperty("error_description", out _));
        Assert.False(pushed.Json().TryGetProperty("request_uri", out _));
        Assert.Equal((LogLevel.Warning, 1), Assert.Single(deployment.OidcLog.Entries));
        Assert.Empty(deployment.Tokens.All);
    }

    /// <summary>
    /// API-REDIR-001 AC2: a destination that holds the registered one within it is not
    /// the registered one, and is refused where it is pushed.
    /// </summary>
    /// <param name="asked">The destination the request names.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("https://attacker.test/collect?next=" + RelyingParty.Destination)]
    [InlineData(RelyingParty.Destination + ".attacker.test")]
    [InlineData("https://attacker.test/" + RelyingParty.Destination)]
    public async Task API_REDIR_001_AC2_ADestinationContainingTheRegisteredOneIsRefusedAsync(string asked)
    {
        await using var deployment = new Deployment();

        _ = await RelyingParty.PreparedAsync(deployment);

        Answer pushed = await RelyingParty.PushAsync(
            deployment,
            RelyingParty.Application,
            silent: true,
            asked,
            "openid email");

        Assert.Equal(StatusCodes.Status400BadRequest, pushed.Status);
        Assert.Equal("invalid_request", pushed.Text("error"));
        Assert.False(pushed.Json().TryGetProperty("request_uri", out _));
    }

    /// <summary>
    /// AUTH-OIDC-001 AC2 and API-REDIR-001 AC1: a client the registry does not hold is
    /// refused where it pushes, before any destination is resolved, and a reference it
    /// names at the endpoint forwards nothing anywhere.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AC2_AnUnregisteredClientIsForwardedNowhereAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        Answer pushed = await RelyingParty.PushAsync(
            deployment,
            "nobody",
            silent: true,
            RelyingParty.Destination,
            "openid email");
        Answer answered = await browser.SendAsync(
            "GET",
            RelyingParty.Authorization("nobody", "urn:ietf:params:oauth:request_uri:guessed"));

        Assert.Equal("invalid_client", pushed.Text("error"));
        Assert.Equal(StatusCodes.Status400BadRequest, answered.Status);
        Assert.Null(answered.Location);
        Assert.Empty(deployment.Tokens.All);
    }

    /// <summary>
    /// AUTH-SESS-012 AC5 and AC6, AUTH-OIDC-002 AC1, BFF-MACH-001 AC3: the exchange
    /// is back channel, authenticates with the client's own secret, and hands a
    /// browser application an access token and an identity token naming the session.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC5_TheExchangeIsBackChannelAndNamesTheSessionAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);
        Answer exchanged = await new Machine(deployment).PostAsync(
            "/oidc/token",
            RelyingParty.Code(code, RelyingParty.Application));

        Assert.Equal(StatusCodes.Status200OK, exchanged.Status);
        Assert.NotEmpty(exchanged.Text("access_token"));
        Assert.NotEmpty(exchanged.Text("id_token"));
        Assert.False(exchanged.Json().TryGetProperty("refresh_token", out _));
        Assert.Empty(exchanged.SetCookie);
        Assert.Equal(
            Single(deployment.Sessions.All).Id.Value.ToString(),
            Claim(exchanged.Text("id_token"), "sid"));
    }

    /// <summary>
    /// AUTH-OIDC-002 AC2 and AUTH-OIDC-003 AC1: a protocol client is handed a refresh
    /// token, it rotates on use, and the one it replaced opens nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_003_AC1_ARefreshTokenRotatesAndTheOldOneIsSpentAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Protocol);
        Answer exchanged = await machine.PostAsync(
            "/oidc/token",
            RelyingParty.Code(code, RelyingParty.Protocol));

        string first = exchanged.Text("refresh_token");

        Assert.NotEmpty(first);

        Answer refreshed = await machine.PostAsync("/oidc/token", Refresh(first));

        Assert.Equal(StatusCodes.Status200OK, refreshed.Status);
        Assert.NotEqual(first, refreshed.Text("refresh_token"));

        Answer replayed = await machine.PostAsync("/oidc/token", Refresh(first));

        Assert.Equal(StatusCodes.Status400BadRequest, replayed.Status);
        Assert.NotEmpty(deployment.OidcAudit.Reuses);
    }

    /// <summary>
    /// AUTH-OIDC-001, chapter 09 section 9: what userinfo answers is what the scope
    /// named and nothing else, read against the published key set.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_UserInfoAnswersWhatTheScopeNamesAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);
        Answer exchanged = await machine.PostAsync(
            "/oidc/token",
            RelyingParty.Code(code, RelyingParty.Application));
        Answer read = await machine.GetAsync("/oidc/userinfo", exchanged.Text("access_token"));

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal(Flow.Address, read.Text("email"));
        Assert.True(read.Json().GetProperty("email_verified").GetBoolean());
        Assert.False(read.Json().TryGetProperty("phone_number", out _));
    }

    /// <summary>
    /// BFF-SESS-003 AC2: a second application reaches the same session record without
    /// asking the person anything, so moving between applications re-establishes
    /// silently and authenticates nobody again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_003_AC2_ASecondApplicationReEstablishesSilentlyAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);

        await deployment.Clients.AddAsync(
            new OidcClient(
                Second,
                Second,
                OidcClientKind.BrowserApplication,
                RelyingParty.Destination,
                ["openid", "email"]),
            Encoding.UTF8.GetBytes(RelyingParty.Secret),
            deployment.Clock.GetUtcNow(),
            TestContext.Current.CancellationToken);

        var machine = new Machine(deployment);
        string first = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);
        string second = await RelyingParty.CodeAsync(deployment, browser, Second);

        Assert.NotEqual(first, second);

        string held = Single(deployment.Sessions.All).Id.Value.ToString();

        Assert.Equal(
            held,
            Claim(
                (await machine.PostAsync("/oidc/token", RelyingParty.Code(first, RelyingParty.Application)))
                    .Text("id_token"),
                "sid"));
        Assert.Equal(
            held,
            Claim(
                (await machine.PostAsync("/oidc/token", RelyingParty.Code(second, Second)))
                    .Text("id_token"),
                "sid"));
    }

    /// <summary>
    /// BFF-MACH-001 AC3: the token endpoint authenticates a client that carries its
    /// secret and nothing a browser carries, and refuses one whose secret is not the
    /// registered one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_MACH_001_AC3_TheTokenEndpointAuthenticatesTheClientAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Protocol);

        Answer refused = await machine.PostAsync(
            "/oidc/token",
            ("grant_type", "authorization_code"),
            ("code", code),
            ("client_id", RelyingParty.Protocol),
            ("client_secret", "not-the-registered-secret"),
            ("redirect_uri", RelyingParty.Destination),
            ("code_verifier", RelyingParty.Verifier));

        Assert.Equal(StatusCodes.Status401Unauthorized, refused.Status);

        Answer taken = await machine.PostAsync(
            "/oidc/token",
            RelyingParty.Code(code, RelyingParty.Protocol));

        Assert.Equal(StatusCodes.Status200OK, taken.Status);
        Assert.NotEmpty(taken.Text("access_token"));
    }

    /// <summary>
    /// AUTH-OIDC-001 AC3: the registry is managed by hand, so the document offers no
    /// registration endpoint and the route a client would post one to is not there.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AC3_NoDynamicRegistrationEndpointExistsAsync()
    {
        await using var deployment = new Deployment();

        var machine = new Machine(deployment);
        JsonElement document = (await machine
                .GetAsync("/.well-known/openid-configuration", bearer: string.Empty))
            .Json();

        Assert.False(document.TryGetProperty("registration_endpoint", out _));

        // A path the library maps for another method answers 405, so 404 is the route
        // not being there at all rather than this request being the wrong shape.
        Answer reached = await machine.GetAsync("/oidc/register", bearer: string.Empty);

        Assert.Equal(StatusCodes.Status404NotFound, reached.Status);
        Assert.Empty(await deployment.Clients.AllAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-OIDC-001 AC4: the token a protocol client obtains is the signed-in person's
    /// and nobody else's, which is what the mail server manages app passwords under;
    /// the library holds none of those passwords.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AC4_TheProtocolClientsTokenIsTheSignedInPersonsAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Protocol);
        Answer exchanged = await new Machine(deployment).PostAsync(
            "/oidc/token",
            RelyingParty.Code(code, RelyingParty.Protocol));

        Session live = Single(deployment.Sessions.All);

        Assert.Equal(StatusCodes.Status200OK, exchanged.Status);
        Assert.Equal(live.Subject.Value.ToString(), Claim(exchanged.Text("id_token"), "sub"));
        Assert.Equal(live.Id.Value.ToString(), Claim(exchanged.Text("id_token"), "sid"));
    }

    /// <summary>
    /// AUTH-OIDC-004 AC3: a party that validates the token against the published keys
    /// rather than the record refuses it at its expiry and no later, so the latency it
    /// accepts is the configured lifetime.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_004_AC3_AnOfflineValidatorRefusesTheTokenAtItsExpiryAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);
        Answer exchanged = await machine.PostAsync(
            "/oidc/token",
            RelyingParty.Code(code, RelyingParty.Application));

        string token = exchanged.Text("access_token");

        Assert.Equal(
            StatusCodes.Status200OK,
            (await machine.GetAsync("/oidc/userinfo", token)).Status);

        deployment.Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            (await machine.GetAsync("/oidc/userinfo", token)).Status);
    }

    /// <summary>
    /// BFF-MACH-001 AC2, LIB-API-003 AC5: each of the provider's machine routes refuses
    /// a request that arrives carrying a browser's session cookie with its protocol's
    /// <c>invalid_request</c>, the code alone, in a body on the pushed request and the
    /// token request (RFC 6749 section 5.2, RFC 9126 section 2.3) and in the challenge
    /// on the userinfo request (RFC 6750 section 3), before it reads what the request
    /// presents: no reference is issued, and the code the token request carried is
    /// still exchanged by the request that carries no cookie.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_MACH_001_AC2_ACookieOnAnOidcRouteIsRefusedWithInvalidRequestAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);
        string cookie = Janus.Hosting.Bff.BrowserCookies.Session
            + "="
            + browser.Cookies[Janus.Hosting.Bff.BrowserCookies.Session];

        Answer pushed = await machine.PostCarryingAsync(
            "/oidc/par",
            cookie,
            RelyingParty.Request(RelyingParty.Application, silent: true, RelyingParty.Destination, "openid email"));
        Answer exchanged = await machine
            .PostCarryingAsync("/oidc/token", cookie, RelyingParty.Code(code, RelyingParty.Application));
        Answer claims = await machine.CallCarryingAsync("/oidc/userinfo", cookie);
        Answer taken = await machine.PostAsync("/oidc/token", RelyingParty.Code(code, RelyingParty.Application));

        Assert.All(
            new[] { pushed, exchanged },
            refused =>
            {
                Assert.Equal(StatusCodes.Status400BadRequest, refused.Status);
                Assert.Equal("invalid_request", refused.Text("error"));
                Assert.Equal("error", Assert.Single(refused.Json().EnumerateObject()).Name);
            });
        Assert.Equal(StatusCodes.Status400BadRequest, claims.Status);
        Assert.Equal("Bearer error=\"invalid_request\"", claims.Header(HeaderNames.WWWAuthenticate));
        Assert.Empty(claims.Body);
        Assert.Equal(StatusCodes.Status200OK, taken.Status);
        Assert.NotEmpty(taken.Text("access_token"));
    }

    /// <summary>
    /// API-CONV-001 AC2 and LIB-HOST-003 AC2: a deployment that mounts the library
    /// under a prefix publishes a document whose addresses carry it, because nothing
    /// in the document is a path the library wrote down.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_001_AC2_TheDocumentCarriesThePrefixTheHostMountedUnderAsync()
    {
        await using var deployment = new Deployment(prefix: Prefix);

        JsonElement document = (await new Machine(deployment)
                .GetAsync(Prefix + "/.well-known/openid-configuration", bearer: string.Empty))
            .Json();

        foreach (string named in new[]
        {
            "authorization_endpoint",
            "token_endpoint",
            "userinfo_endpoint",
            "jwks_uri",
        })
        {
            Assert.Contains(
                Prefix + "/oidc/",
                document.GetProperty(named).GetString(),
                StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AUTH-OIDC-002 AC1 and AC2, chapter 09 section 9: a browser application's own
    /// layer that asks to be given something to hold is refused where it asks, and is
    /// never quietly handed a code that would carry it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_002_AC1_ABrowserApplicationAskingToHoldOneIsRefusedAsync()
    {
        await using var deployment = new Deployment();

        await RelyingParty.RegisteredAsync(deployment);

        Answer answered = await RelyingParty.PushAsync(
            deployment,
            RelyingParty.Application,
            silent: true,
            RelyingParty.Destination,
            "openid email offline_access");

        Assert.Equal(StatusCodes.Status400BadRequest, answered.Status);
        Assert.Null(answered.Location);
        Assert.False(answered.Json().TryGetProperty("request_uri", out _));
        Assert.Empty(deployment.Tokens.All);
    }

    /// <summary>
    /// AUTH-SESS-012 AC6: the exchange hands a browser application's own layer an
    /// access token and nothing it could hold the session with afterwards.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC6_TheExchangeHandsABrowserApplicationNoRefreshTokenAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);

        Answer exchanged = await machine.PostAsync(
            "/oidc/token",
            RelyingParty.Code(code, RelyingParty.Application));

        Assert.Equal(StatusCodes.Status200OK, exchanged.Status);
        Assert.NotEmpty(exchanged.Text("access_token"));
        Assert.False(exchanged.Json().TryGetProperty("refresh_token", out _));
    }

    /// <summary>
    /// API-REDIR-001 AC4: the destination that was replaced is recorded, and the
    /// request that named the registered one records nothing, so what the log holds
    /// is the attempts and not the traffic.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_REDIR_001_AC4_OnlyTheRefusedDestinationIsRecordedAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);

        _ = await browser.SendAsync(
            "GET",
            await RelyingParty.AuthorizeAsync(deployment, RelyingParty.Application, silent: true));

        Assert.Empty(deployment.OidcLog.Entries);

        _ = await RelyingParty.PushAsync(
            deployment,
            RelyingParty.Application,
            silent: true,
            "https://attacker.test/collect",
            "openid email");

        Assert.Equal(LogLevel.Warning, Assert.Single(deployment.OidcLog.Entries).Level);
    }

    /// <summary>
    /// LIB-API-003 AC1 (D-166, 394): every error the provider answers carries the
    /// protocol's code alone, and no description or documentation address, in its body,
    /// its <c>WWW-Authenticate</c> header or the address the client is sent back to.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_003_AC1_NoProviderErrorCarriesADescriptionAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);

        _ = await machine.PostAsync("/oidc/token", RelyingParty.Code(code, RelyingParty.Application));

        Answer spent = await machine.PostAsync("/oidc/token", RelyingParty.Code(code, RelyingParty.Application));
        Answer anonymous = await RelyingParty.PushAsync(
            deployment,
            RelyingParty.With(
                RelyingParty.Request(RelyingParty.Application, silent: true, RelyingParty.Destination, "openid"),
                "client_id",
                null));
        Answer unread = await machine.GetAsync("/oidc/userinfo", "not-a-token-the-provider-issued");
        Answer returned = await new Browser(deployment)
            .SendAsync(
                "GET",
                await RelyingParty.AuthorizeAsync(deployment, RelyingParty.Application, silent: true));

        string[] carried =
        [
            spent.Body,
            anonymous.Body,
            unread.Body,
            unread.Header("WWW-Authenticate") ?? string.Empty,
            RelyingParty.Where(returned),
        ];

        Assert.Equal(
            ["invalid_grant", "invalid_request", "invalid_token", "login_required"],
            [
                spent.Text("error"),
                anonymous.Text("error"),
                unread.Header("WWW-Authenticate")!.Contains("invalid_token", StringComparison.Ordinal)
                    ? "invalid_token"
                    : string.Empty,
                RelyingParty.Returned(returned, "error"),
            ]);
        Assert.All(carried, text => Assert.DoesNotContain("error_description", text, StringComparison.Ordinal));
        Assert.All(carried, text => Assert.DoesNotContain("error_uri", text, StringComparison.Ordinal));
    }

    private static (string Name, string? Value)[] Refresh(string token) =>
    [
        ("grant_type", "refresh_token"),
        ("refresh_token", token),
        ("client_id", RelyingParty.Protocol),
        ("client_secret", RelyingParty.Secret),
    ];

    private static Session Single(IReadOnlyCollection<Session> sessions)
    {
        foreach (Session held in sessions)
        {
            return held;
        }

        throw new InvalidOperationException("The deployment holds no session.");
    }

    private static string Header(string token, string name) =>
        JsonDocument
            .Parse(Base64Url.DecodeFromChars(token.Split('.')[0]))
            .RootElement
            .GetProperty(name)
            .GetString() ?? string.Empty;

    private static string Claim(string token, string name)
    {
        string payload = token.Split('.')[1];

        return JsonDocument
            .Parse(Base64Url.DecodeFromChars(payload))
            .RootElement
            .GetProperty(name)
            .GetString() ?? string.Empty;
    }

    private static List<string> Listed(JsonElement document, string name)
    {
        var read = new List<string>();

        foreach (JsonElement held in document.GetProperty(name).EnumerateArray())
        {
            read.Add(held.GetString() ?? string.Empty);
        }

        return read;
    }
}
