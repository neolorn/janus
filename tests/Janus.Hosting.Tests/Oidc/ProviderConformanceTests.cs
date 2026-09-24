using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
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
