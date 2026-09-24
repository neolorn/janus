using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Xunit;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// The provider held to the OAuth 2.0 Security Best Current Practice and to OAuth 2.1:
/// each form either of them retires is refused, every authorization request is pushed,
/// and a code reaches the registered destination and no other (AUTH-OIDC-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class ProviderConformanceTests
{
    /// <summary>
    /// AUTH-OIDC-006 AC1: the implicit and hybrid forms, which hand a token to the
    /// browser, are refused where the request is pushed.
    /// </summary>
    /// <param name="responseType">What the request asks to be answered with.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("token")]
    [InlineData("id_token")]
    [InlineData("id_token token")]
    [InlineData("code id_token")]
    [InlineData("code token")]
    public async Task AUTH_OIDC_006_AC1_TheImplicitFormsAreRefusedAsync(string responseType)
    {
        await using var deployment = new Deployment();

        await RelyingParty.RegisteredAsync(deployment);

        Answer pushed = await RelyingParty.PushAsync(
            deployment,
            RelyingParty.With(Request(), "response_type", responseType));

        Refused(pushed, OpenIddictConstants.Errors.UnsupportedResponseType);
    }

    /// <summary>
    /// AUTH-OIDC-006 AC1: the password grant, and every grant beside the code and the
    /// refresh, is refused at the token endpoint.
    /// </summary>
    /// <param name="grant">The grant asked for.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("password")]
    [InlineData("client_credentials")]
    [InlineData("urn:ietf:params:oauth:grant-type:device_code")]
    [InlineData("urn:ietf:params:oauth:grant-type:token-exchange")]
    public async Task AUTH_OIDC_006_AC1_EveryOtherGrantIsRefusedAsync(string grant)
    {
        await using var deployment = new Deployment();

        await RelyingParty.RegisteredAsync(deployment);

        Answer answered = await new Machine(deployment).PostAsync(
            "/oidc/token",
            ("grant_type", grant),
            ("client_id", RelyingParty.Protocol),
            ("client_secret", RelyingParty.Secret),
            ("username", Flow.Address),
            ("password", Flow.Password),
            ("scope", "openid"));

        Refused(answered, OpenIddictConstants.Errors.UnsupportedGrantType);
    }

    /// <summary>
    /// AUTH-OIDC-006 AC1: a proof key by the plain method, and a request carrying no
    /// proof key at all, are refused where the request is pushed.
    /// </summary>
    /// <param name="method">The method named, or nothing.</param>
    /// <param name="challenge">Whether a challenge is carried.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("plain", true)]
    [InlineData(null, true)]
    [InlineData(null, false)]
    public async Task AUTH_OIDC_006_AC1_OnlyTheS256ProofKeyIsTakenAsync(string? method, bool challenge)
    {
        await using var deployment = new Deployment();

        await RelyingParty.RegisteredAsync(deployment);

        (string Name, string? Value)[] fields = RelyingParty.With(
            RelyingParty.With(
                Request(),
                "code_challenge",
                challenge
                    ? string.Equals(method, "plain", StringComparison.Ordinal)
                        ? RelyingParty.Verifier
                        : RelyingParty.Challenge
                    : null),
            "code_challenge_method",
            method);

        Refused(await RelyingParty.PushAsync(deployment, fields), OpenIddictConstants.Errors.InvalidRequest);
    }

    /// <summary>
    /// AUTH-OIDC-006 AC1, API-REDIR-001 AC1: a destination that is not the registered
    /// one character for character never receives the code, however near it is.
    /// </summary>
    /// <param name="asked">The destination the request names.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(RelyingParty.Destination + "/")]
    [InlineData(RelyingParty.Destination + "?next=https://attacker.test")]
    [InlineData(RelyingParty.Destination + "/../../collect")]
    [InlineData(RelyingParty.Destination + "extra")]
    [InlineData("https://APP.example.test/signin/callback")]
    [InlineData("http://app.example.test/signin/callback")]
    [InlineData("https://app.example.test.attacker.test/signin/callback")]
    [InlineData("https://app.example.test:8443/signin/callback")]
    public async Task AUTH_OIDC_006_AC1_OnlyTheExactRegisteredDestinationReceivesTheCodeAsync(string asked)
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        Answer answered = await browser.SendAsync(
            "GET",
            await RelyingParty.AuthorizeAsync(deployment, RelyingParty.Application, silent: true, asked));

        Assert.Equal(StatusCodes.Status302Found, answered.Status);
        Assert.Equal(
            RelyingParty.Destination,
            RelyingParty.Where(answered)[..RelyingParty.Where(answered).IndexOf('?', StringComparison.Ordinal)]);
        Assert.NotEmpty(RelyingParty.Returned(answered, "code"));
    }

    /// <summary>
    /// AUTH-OIDC-006 AC1: a code is exchanged only by naming the destination it was
    /// issued for, exactly.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC1_ACodeIsNotExchangedForAnotherDestinationAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Protocol);

        Answer answered = await new Machine(deployment).PostAsync(
            "/oidc/token",
            RelyingParty.With(
                RelyingParty.Code(code, RelyingParty.Protocol),
                "redirect_uri",
                RelyingParty.Destination + "/"));

        Refused(answered, OpenIddictConstants.Errors.InvalidGrant);
    }

    /// <summary>
    /// AUTH-OIDC-006 AC1: no public client exists to receive a refresh token, since a
    /// client that does not authenticate is refused where it pushes and where it
    /// exchanges, and a browser application's own layer, which does authenticate, is
    /// handed none and cannot refresh.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC1_NoPublicClientReceivesARefreshTokenAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        var machine = new Machine(deployment);

        Answer pushed = await RelyingParty.PushAsync(
            deployment,
            RelyingParty.With(Request(), "client_secret", null));
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application);
        Answer unauthenticated = await machine.PostAsync(
            "/oidc/token",
            RelyingParty.With(RelyingParty.Code(code, RelyingParty.Application), "client_secret", null));
        Answer exchanged = await machine.PostAsync(
            "/oidc/token",
            RelyingParty.Code(code, RelyingParty.Application));

        Refused(pushed, OpenIddictConstants.Errors.InvalidClient);
        Refused(unauthenticated, OpenIddictConstants.Errors.InvalidClient);
        Assert.Equal(StatusCodes.Status200OK, exchanged.Status);
        Assert.False(exchanged.Json().TryGetProperty("refresh_token", out _));
    }

    /// <summary>
    /// AUTH-OIDC-006 AC1: the discovery document names the code flow, its proof key by
    /// S256, and the code and refresh grants, and nothing either specification
    /// retires.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC1_TheDocumentNamesOnlyWhatIsAdmittedAsync()
    {
        await using var deployment = new Deployment();

        JsonElement document = (await new Machine(deployment)
            .GetAsync("/.well-known/openid-configuration", bearer: string.Empty))
            .Json();

        Assert.Equal(["code"], Listed(document, "response_types_supported"));
        Assert.Equal(["S256"], Listed(document, "code_challenge_methods_supported"));
        Assert.Equal(
            ["authorization_code", "refresh_token"],
            Listed(document, "grant_types_supported").Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AUTH-OIDC-006 AC2: an authorization request that carries its parameters rather
    /// than a pushed reference is refused, and no code is issued for it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC2_ADirectAuthorizationRequestIsRefusedAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        string direct = "/oidc/authorize?" + string.Join(
            "&",
            Request()
                .Where(field => field.Value is not null && field.Name is not "client_secret")
                .Select(field => field.Name + "=" + Uri.EscapeDataString(field.Value!)));

        Answer answered = await browser.SendAsync("GET", direct);

        Assert.Equal(StatusCodes.Status400BadRequest, answered.Status);
        Assert.Null(answered.Location);
        Assert.Contains(OpenIddictConstants.Errors.InvalidRequest, answered.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(
            deployment.Tokens.All,
            token => token.Type is OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
    }

    /// <summary>
    /// AUTH-OIDC-006 AC2: a reference a code was issued against is not taken again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC2_AReferenceIsTakenOnceAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        string authorization = await RelyingParty.AuthorizeAsync(
            deployment,
            RelyingParty.Application,
            silent: true);

        Answer first = await browser.SendAsync("GET", authorization);
        Answer again = await browser.SendAsync("GET", authorization);

        Assert.NotEmpty(RelyingParty.Returned(first, "code"));
        Assert.Equal(StatusCodes.Status400BadRequest, again.Status);
        Assert.Null(again.Location);
    }

    /// <summary>
    /// AUTH-OIDC-006 AC2: a reference is spent by the first answer it is given,
    /// whatever that answer is, so one refused, or one that forwarded a browser to
    /// sign in, is not taken again.
    /// </summary>
    /// <param name="silent">Whether the request asked not to be shown anything.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AUTH_OIDC_006_AC2_AReferenceAnsweredWithoutACodeIsSpentAsync(bool silent)
    {
        await using var deployment = new Deployment(
            signIn: new AuthenticationAddresses(
                "https://identity.example.test/signin",
                "https://identity.example.test"));

        await RelyingParty.RegisteredAsync(deployment);

        var browser = new Browser(deployment);
        string authorization = await RelyingParty.AuthorizeAsync(deployment, RelyingParty.Application, silent);

        Answer first = await browser.SendAsync("GET", authorization);
        Answer again = await browser.SendAsync("GET", authorization);

        Assert.Equal(StatusCodes.Status302Found, first.Status);
        Assert.Equal(StatusCodes.Status400BadRequest, again.Status);
        Assert.Null(again.Location);
    }

    /// <summary>
    /// AUTH-OIDC-006 AC2: a reference lapses sixty seconds after it is issued, and the
    /// push says so.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC2_AReferenceLapsesAfterSixtySecondsAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        Answer kept = await RelyingParty.PushAsync(deployment, Request());
        Answer lapsed = await RelyingParty.PushAsync(deployment, Request());

        deployment.Clock.Advance(TimeSpan.FromSeconds(59));

        Answer inTime = await browser.SendAsync(
            "GET",
            RelyingParty.Authorization(RelyingParty.Application, kept.Text("request_uri")));

        deployment.Clock.Advance(TimeSpan.FromSeconds(2));

        Answer late = await browser.SendAsync(
            "GET",
            RelyingParty.Authorization(RelyingParty.Application, lapsed.Text("request_uri")));

        Assert.Equal(60, kept.Json().GetProperty("expires_in").GetInt32());
        Assert.NotEmpty(RelyingParty.Returned(inTime, "code"));
        Assert.Equal(StatusCodes.Status400BadRequest, late.Status);
        Assert.Null(late.Location);
    }

    /// <summary>
    /// AUTH-OIDC-006 AC3: every access token the token endpoint issues is typed
    /// `at+jwt` and carries the seven claims, its audience the client it was issued to.
    /// </summary>
    /// <param name="clientId">The client the token is issued to.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(RelyingParty.Protocol)]
    [InlineData(RelyingParty.Application)]
    public async Task AUTH_OIDC_006_AC3_EveryAccessTokenIsTypedAndCarriesTheSevenClaimsAsync(string clientId)
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, clientId);
        Answer exchanged = await new Machine(deployment).PostAsync(
            "/oidc/token",
            RelyingParty.Code(code, clientId));

        Typed(exchanged.Text("access_token"), clientId, deployment);
    }

    /// <summary>
    /// AUTH-OIDC-006 AC3: an access token issued on a refresh is typed and carries the
    /// seven claims as the first was.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC3_ARefreshedAccessTokenIsTypedAndCarriesTheSevenClaimsAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        var machine = new Machine(deployment);
        string code = await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Protocol);
        Answer exchanged = await machine.PostAsync("/oidc/token", RelyingParty.Code(code, RelyingParty.Protocol));
        Answer refreshed = await machine.PostAsync(
            "/oidc/token",
            ("grant_type", "refresh_token"),
            ("refresh_token", exchanged.Text("refresh_token")),
            ("client_id", RelyingParty.Protocol),
            ("client_secret", RelyingParty.Secret));

        Assert.Equal(StatusCodes.Status200OK, refreshed.Status);
        Typed(refreshed.Text("access_token"), RelyingParty.Protocol, deployment);
    }

    /// <summary>
    /// AUTH-OIDC-006 AC3: the mail server's adapter takes the token issued to the mail
    /// server's client and refuses a token issued to any other client, and an identity
    /// token issued to the mail server's own client, however validly signed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC3_TheAdapterRefusesATokenForAnotherAudienceAsync()
    {
        await using var deployment = new Deployment();

        Browser browser = await RelyingParty.PreparedAsync(deployment);
        var machine = new Machine(deployment);
        Answer forMailServer = await machine.PostAsync(
            "/oidc/token",
            RelyingParty.Code(
                await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Protocol),
                RelyingParty.Protocol));
        Answer forApplication = await machine.PostAsync(
            "/oidc/token",
            RelyingParty.Code(
                await RelyingParty.CodeAsync(deployment, browser, RelyingParty.Application),
                RelyingParty.Application));

        TokenValidationResult taken = await MailServerAdapter.VerifyAsync(
            deployment,
            forMailServer.Text("access_token"),
            RelyingParty.Protocol);
        TokenValidationResult elsewhere = await MailServerAdapter.VerifyAsync(
            deployment,
            forApplication.Text("access_token"),
            RelyingParty.Protocol);
        TokenValidationResult identity = await MailServerAdapter.VerifyAsync(
            deployment,
            forMailServer.Text("id_token"),
            RelyingParty.Protocol);

        Assert.True(taken.IsValid);
        Assert.IsType<SecurityTokenInvalidAudienceException>(elsewhere.Exception);
        Assert.IsType<SecurityTokenInvalidTypeException>(identity.Exception);
    }

    /// <summary>
    /// AUTH-OIDC-006 AC4: the discovery document names where a request is pushed and
    /// that every request must be.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC4_TheDocumentRequiresPushedRequestsAsync()
    {
        await using var deployment = new Deployment();

        JsonElement document = (await new Machine(deployment)
            .GetAsync("/.well-known/openid-configuration", bearer: string.Empty))
            .Json();

        Assert.EndsWith(
            "/oidc/par",
            document.GetProperty("pushed_authorization_request_endpoint").GetString(),
            StringComparison.Ordinal);
        Assert.True(document.GetProperty("require_pushed_authorization_requests").GetBoolean());
    }

    private static (string Name, string? Value)[] Request() =>
        RelyingParty.Request(
            RelyingParty.Application,
            silent: true,
            RelyingParty.Destination,
            "openid email");

    // AUTH-OIDC-006 AC3: RFC 9068 section 2, the header type and the seven claims,
    // the audience and client the one the token was issued to, the subject the person
    // signed in.
    private static void Typed(string token, string clientId, Deployment deployment)
    {
        JsonElement claims = MailServerAdapter.Claims(token);

        Assert.Equal("at+jwt", MailServerAdapter.Header(token).GetProperty("typ").GetString());
        Assert.Subset(
            MailServerAdapter.Named(token).ToHashSet(StringComparer.Ordinal),
            MailServerAdapter.Required.ToHashSet(StringComparer.Ordinal));
        Assert.Equal(clientId, claims.GetProperty("aud").GetString());
        Assert.Equal(clientId, claims.GetProperty("client_id").GetString());
        Assert.Equal(
            deployment.Sessions.All.First().Subject.ToString(),
            claims.GetProperty("sub").GetString());
        Assert.NotEmpty(claims.GetProperty("jti").GetString()!);
        Assert.True(claims.GetProperty("exp").GetInt64() > claims.GetProperty("iat").GetInt64());
    }

    private static void Refused(Answer answered, string error)
    {
        Assert.InRange(answered.Status, StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized);
        Assert.Null(answered.Location);
        Assert.Equal(error, answered.Text("error"));
        Assert.False(answered.Json().TryGetProperty("request_uri", out _));
    }

    private static List<string> Listed(JsonElement document, string name) =>
        [.. document.GetProperty(name).EnumerateArray().Select(held => held.GetString() ?? string.Empty)];
}
