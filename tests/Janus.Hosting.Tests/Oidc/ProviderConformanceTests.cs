using System;
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
/// every authorization request is pushed, and the reference it is answered with is
/// taken once and lapses (AUTH-OIDC-006).
/// </summary>
[Trait("kind", "unit")]
public sealed class ProviderConformanceTests
{
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
}
