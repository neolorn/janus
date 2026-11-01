using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using Xunit;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// A browser application that holds no secret, as the provider meets it: a direct
/// request or an unauthenticated push, the code with PKCE <c>S256</c> alone, an access
/// token for its audience and no refresh token, and the cross-origin answers its
/// browser needs (AUTH-OIDC-001, AUTH-OIDC-004, AUTH-OIDC-006, D-252).
/// </summary>
[Trait("kind", "unit")]
public sealed class PublicApplicationTests
{
    private const string Webmail = "webmail";
    private const string Pages = "account-pages";
    private const string Callback = "https://webmail.example.test/login/callback";
    private const string Loopback = "http://127.0.0.1:8080/callback";
    private const string Registered = "https://webmail.example.test";
    private const string MailScope = "urn:ietf:params:oauth:scope:mail";

    /// <summary>
    /// AUTH-OIDC-006 AC5 (D-252): a public application's direct request with PKCE
    /// <c>S256</c> is answered with a code, which it exchanges with no secret for an ID
    /// token naming itself, an access token naming its audience alone and carrying
    /// <c>openid</c> and the registered scopes it asked for, and no refresh token.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC5_ADirectRequestFromAPublicApplicationIsAnsweredAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        Answer answered = await browser.SendAsync("GET", Direct(Webmail, Callback, "openid email " + MailScope));

        Assert.Equal(StatusCodes.Status302Found, answered.Status);
        Assert.StartsWith(Callback + "?", RelyingParty.Where(answered), StringComparison.Ordinal);

        Answer exchanged = await new Machine(deployment).PostAsync(
            "/oidc/token",
            Exchange(RelyingParty.Returned(answered, "code"), Webmail, Callback));

        JsonElement access = MailServerAdapter.Claims(exchanged.Text("access_token"));
        JsonElement identity = MailServerAdapter.Claims(exchanged.Text("id_token"));

        Assert.Equal(StatusCodes.Status200OK, exchanged.Status);
        Assert.Equal(RelyingParty.Protocol, access.GetProperty("aud").GetString());
        Assert.Equal(Webmail, identity.GetProperty("aud").GetString());
        Assert.Equal(
            ["email", "openid", MailScope],
            access.GetProperty("scope").GetString()!.Split(' ').Order(StringComparer.Ordinal));
        Assert.False(exchanged.Json().TryGetProperty("refresh_token", out _));
    }

    /// <summary>
    /// AUTH-OIDC-006 AC5 (D-252): a direct request from a client of any other kind is
    /// still refused with <c>invalid_request</c>, and no code is issued.
    /// </summary>
    /// <param name="clientId">The client asking.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(RelyingParty.Application)]
    [InlineData(RelyingParty.Protocol)]
    public async Task AUTH_OIDC_006_AC5_ADirectRequestFromAnyOtherKindIsRefusedAsync(string clientId)
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        Answer answered = await browser.SendAsync("GET", Direct(clientId, RelyingParty.Destination, "openid email"));

        Assert.Equal(StatusCodes.Status400BadRequest, answered.Status);
        Assert.Null(answered.Location);
        Assert.Contains(OpenIddictConstants.Errors.InvalidRequest, answered.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(
            deployment.Tokens.All,
            token => token.Type is OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
    }

    /// <summary>
    /// AUTH-OIDC-006 AC5 (D-252): a public application's request with the plain method, a
    /// challenge naming no method, no challenge, or a request for <c>offline_access</c>,
    /// is refused with <c>invalid_request</c> and no code is issued.
    /// </summary>
    /// <param name="form">The form the request takes.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("plain")]
    [InlineData("unnamed")]
    [InlineData("unchallenged")]
    [InlineData("offline")]
    public async Task AUTH_OIDC_006_AC5_APublicRequestWithoutS256OrAskingToHoldOneIsRefusedAsync(string form)
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        (string Name, string? Value)[] fields = Request(Webmail, Callback, "openid email");

        fields = form switch
        {
            "plain" => RelyingParty.With(
                RelyingParty.With(fields, "code_challenge", RelyingParty.Verifier),
                "code_challenge_method",
                "plain"),
            "unnamed" => RelyingParty.With(fields, "code_challenge_method", null),
            "unchallenged" => RelyingParty.With(
                RelyingParty.With(fields, "code_challenge", null),
                "code_challenge_method",
                null),
            _ => RelyingParty.With(fields, "scope", "openid email offline_access"),
        };

        Answer answered = await browser.SendAsync("GET", Query(fields));

        Assert.Equal(OpenIddictConstants.Errors.InvalidRequest, Refused(answered));
        Assert.DoesNotContain(
            deployment.Tokens.All,
            token => token.Type is OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
    }

    /// <summary>
    /// AUTH-OIDC-001 AC5 (D-252): a public application asking for a scope it is not
    /// registered with is refused with <c>invalid_scope</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_001_AC5_APublicApplicationsUnregisteredScopeIsRefusedAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        Answer answered = await browser.SendAsync("GET", Direct(Webmail, Callback, "openid phone"));

        Assert.Equal(OpenIddictConstants.Errors.InvalidScope, Refused(answered));
    }

    /// <summary>
    /// AUTH-OIDC-006 AC5 (D-252): a public application's token request that
    /// authenticates with a secret is refused with <c>invalid_client</c>, and its code is
    /// not exchanged.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC5_ATokenRequestFromAPublicApplicationWithASecretIsRefusedAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        Answer answered = await browser.SendAsync("GET", Direct(Webmail, Callback, "openid email"));

        Answer exchanged = await new Machine(deployment).PostAsync(
            "/oidc/token",
            [.. Exchange(RelyingParty.Returned(answered, "code"), Webmail, Callback), ("client_secret", RelyingParty.Secret)]);

        Assert.Equal(StatusCodes.Status401Unauthorized, exchanged.Status);
        Assert.Equal(OpenIddictConstants.Errors.InvalidClient, exchanged.Text("error"));
        Assert.False(exchanged.Json().TryGetProperty("access_token", out _));
    }

    /// <summary>
    /// AUTH-OIDC-006 (D-252): a public application may push its request with no client
    /// authentication, naming any of its registered return addresses; naming none where
    /// it holds several is refused, since none is then the registered one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_APublicApplicationPushesWithoutAuthenticatingAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        Answer first = await RelyingParty.PushAsync(deployment, Request(Webmail, Callback, "openid email"));
        Answer further = await RelyingParty.PushAsync(deployment, Request(Webmail, Loopback, "openid email"));
        Answer unnamed = await RelyingParty.PushAsync(
            deployment,
            RelyingParty.With(Request(Webmail, Callback, "openid email"), "redirect_uri", null));

        Assert.Equal(
            (StatusCodes.Status201Created, StatusCodes.Status201Created, StatusCodes.Status400BadRequest),
            (first.Status, further.Status, unnamed.Status));
        Assert.Equal(OpenIddictConstants.Errors.InvalidRequest, unnamed.Text("error"));
        Assert.False(unnamed.Json().TryGetProperty("request_uri", out _));
    }

    /// <summary>
    /// AUTH-OIDC-006 (D-252): a public application registered with no audience is its
    /// own tokens' audience.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_APublicApplicationWithNoAudienceIsItsOwnAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        Answer answered = await browser.SendAsync("GET", Direct(Pages, "https://pages.example.test/callback", "openid email"));

        Answer exchanged = await new Machine(deployment).PostAsync(
            "/oidc/token",
            Exchange(RelyingParty.Returned(answered, "code"), Pages, "https://pages.example.test/callback"));

        Assert.Equal(StatusCodes.Status200OK, exchanged.Status);
        Assert.Equal(Pages, MailServerAdapter.Claims(exchanged.Text("access_token")).GetProperty("aud").GetString());
    }

    /// <summary>
    /// AUTH-OIDC-004 AC2 (D-252): a public application's access token lives
    /// <c>oidc.publicapplication.accesstoken.lifetime</c>, not the general lifetime.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_004_AC2_APublicApplicationsTokenLivesItsOwnLifetimeAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        Answer answered = await browser.SendAsync("GET", Direct(Webmail, Callback, "openid email"));

        Answer exchanged = await new Machine(deployment).PostAsync(
            "/oidc/token",
            Exchange(RelyingParty.Returned(answered, "code"), Webmail, Callback));

        JsonElement access = MailServerAdapter.Claims(exchanged.Text("access_token"));

        Assert.Equal(
            (long)TimeSpan.FromMinutes(15).TotalSeconds,
            access.GetProperty("exp").GetInt64() - access.GetProperty("iat").GetInt64());
    }

    /// <summary>
    /// AUTH-OIDC-006 AC6 (D-252): from the origin of a public application's registered
    /// return address, the token endpoint's preflight and its token request, discovery and
    /// the key set each answer for that origin exactly, allowing no credentials.
    /// </summary>
    /// <param name="origin">The origin the browser names.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(Registered)]
    [InlineData("http://127.0.0.1:8080")]
    public async Task AUTH_OIDC_006_AC6_ARegisteredOriginIsAnsweredForItselfAsync(string origin)
    {
        await using var deployment = new Deployment();

        Browser browser = await PreparedAsync(deployment);
        Answer authorized = await browser.SendAsync("GET", Direct(Webmail, Callback, "openid email"));
        var machine = new Machine(deployment) { Origin = origin };

        Answer preflight = await machine.PreflightAsync("/oidc/token", "POST", "content-type");
        Answer exchanged = await machine.PostAsync(
            "/oidc/token",
            Exchange(RelyingParty.Returned(authorized, "code"), Webmail, Callback));
        Answer discovery = await machine.GetAsync("/.well-known/openid-configuration", bearer: string.Empty);
        Answer keys = await machine.GetAsync("/oidc/jwks", bearer: string.Empty);

        Assert.Equal(StatusCodes.Status204NoContent, preflight.Status);
        Assert.Equal(("POST", "Content-Type"), (preflight.Header("Access-Control-Allow-Methods"), preflight.Header("Access-Control-Allow-Headers")));
        Assert.Equal(
            (StatusCodes.Status200OK, StatusCodes.Status200OK, StatusCodes.Status200OK),
            (exchanged.Status, discovery.Status, keys.Status));

        foreach (Answer answered in new[] { preflight, exchanged, discovery, keys })
        {
            Assert.Equal(origin, answered.Header("Access-Control-Allow-Origin"));
            Assert.Null(answered.Header("Access-Control-Allow-Credentials"));
        }
    }

    /// <summary>
    /// AUTH-OIDC-006 AC6 (D-252): from any other origin, and from a client of another kind's
    /// origin, the same requests carry no cross-origin header, and the preflight is not
    /// answered for them.
    /// </summary>
    /// <param name="origin">The origin the browser names.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("https://elsewhere.example.test")]
    [InlineData("https://app.example.test")]
    [InlineData("https://webmail.example.test:8443")]
    [InlineData("http://webmail.example.test")]
    [InlineData("null")]
    public async Task AUTH_OIDC_006_AC6_AnyOtherOriginGetsNoCrossOriginHeaderAsync(string origin)
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        var machine = new Machine(deployment) { Origin = origin };

        Answer[] answers =
        [
            await machine.PreflightAsync("/oidc/token", "POST", "content-type"),
            await machine.PostAsync("/oidc/token", Exchange("a-code-never-issued", Webmail, Callback)),
            await machine.GetAsync("/.well-known/openid-configuration", bearer: string.Empty),
            await machine.GetAsync("/oidc/jwks", bearer: string.Empty),
        ];

        Assert.NotEqual(StatusCodes.Status204NoContent, answers[0].Status);
        Assert.All(answers, answered => Assert.Empty(CrossOrigin(answered)));
    }

    /// <summary>
    /// AUTH-OIDC-006 AC6 (D-252): no route but the three answers a public application's
    /// origin across origins: not the push, the user info, nor a browser route.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC6_NoOtherRouteAnswersCrossOriginAsync()
    {
        await using var deployment = new Deployment();

        await RegisteredAsync(deployment);

        var machine = new Machine(deployment) { Origin = Registered };

        Answer[] answers =
        [
            await machine.PreflightAsync("/oidc/par", "POST", "content-type"),
            await RelyingParty.PushAsync(deployment, Request(Webmail, Callback, "openid email")),
            await machine.PostAsync("/oidc/par", Request(Webmail, Callback, "openid email")),
            await machine.PreflightAsync("/oidc/userinfo", "GET", "authorization"),
            await machine.GetAsync("/oidc/userinfo", bearer: "not-a-token"),
            await machine.PreflightAsync("/auth/session", "GET"),
            await machine.CallAsync("/auth/session"),
        ];

        Assert.All(answers, answered => Assert.Empty(CrossOrigin(answered)));
    }

    // The webmail, registered with two return addresses and the mail server as its
    // audience; the account pages, with one and none; and the two clients every flow
    // test registers.
    private static async Task RegisteredAsync(Deployment deployment)
    {
        await RelyingParty.RegisteredAsync(deployment);

        await deployment.Clients.AddAsync(
            new OidcClient(Webmail, Webmail, OidcClientKind.PublicApplication, Callback, ["openid", "email", "profile", MailScope])
            {
                OtherRedirects = [Loopback],
                Audience = RelyingParty.Protocol,
            },
            secret: null,
            deployment.Clock.GetUtcNow(),
            TestContext.Current.CancellationToken);
        await deployment.Clients.AddAsync(
            new OidcClient(Pages, Pages, OidcClientKind.PublicApplication, "https://pages.example.test/callback", ["openid", "email"]),
            secret: null,
            deployment.Clock.GetUtcNow(),
            TestContext.Current.CancellationToken);
    }

    private static async Task<Browser> PreparedAsync(Deployment deployment)
    {
        await RegisteredAsync(deployment);

        Flow.Prepare(deployment);

        return await Flow.SignedInAsync(deployment);
    }

    // An authorization request as a public application makes it: no secret, and silent,
    // so a live session is answered at once.
    private static (string Name, string? Value)[] Request(string clientId, string redirect, string scope) =>
        RelyingParty.With(
            RelyingParty.Request(clientId, silent: true, redirect, scope),
            "client_secret",
            null);

    private static string Direct(string clientId, string redirect, string scope) =>
        Query(Request(clientId, redirect, scope));

    private static string Query((string Name, string? Value)[] fields) =>
        "/oidc/authorize?" + string.Join(
            "&",
            fields
                .Where(field => field.Value is not null)
                .Select(field => field.Name + "=" + Uri.EscapeDataString(field.Value!)));

    private static (string Name, string? Value)[] Exchange(string code, string clientId, string redirect) =>
    [
        ("grant_type", "authorization_code"),
        ("code", code),
        ("client_id", clientId),
        ("redirect_uri", redirect),
        ("code_verifier", RelyingParty.Verifier),
    ];

    // The protocol's refusal, answered to the browser before any return address was
    // judged, in the library's one shape (LIB-API-003 AC4).
    private static string Refused(Answer answered)
    {
        Assert.Equal((StatusCodes.Status400BadRequest, null), (answered.Status, answered.Location));
        Assert.Equal("api.request.malformed", answered.Text("code"));

        return answered.Json().GetProperty("details").GetProperty("error").GetString() ?? string.Empty;
    }

    private static string[] CrossOrigin(Answer answered) =>
        [.. answered.Headers.Keys.Where(name => name.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase))];
}
