using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// The addresses the library composes while it is mounted under a prefix of the host's
/// choosing (LIB-HOST-003 AC2, API-CONV-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class PrefixedAddressTests
{
    private const string Prefix = "/identity";

    private const string Origin = "https://identity.example.test";

    private const string Client = "this-application";

    private const string Secret = "a-secret-the-deployment-set";

    /// <summary>
    /// LIB-HOST-003 AC2: the provider's return is forwarded to the continuation under
    /// the prefix the request arrived under, and not to the same path at the root.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_HOST_003_AC2_TheProviderReturnIsForwardedUnderThePrefixAsync()
    {
        await using var deployment = new Deployment(prefix: Prefix);

        Answer forwarded = await new Browser(deployment).SendAsync(
            "GET",
            Prefix + "/callbacks/providers/google/return?code=issued&state=sent");

        Assert.Equal(StatusCodes.Status303SeeOther, forwarded.Status);
        Assert.Equal(Prefix + "/auth/providers/google/return?code=issued&state=sent", forwarded.Location);
    }

    /// <summary>
    /// LIB-HOST-003 AC2: a cross-site post that navigated the page without a session is
    /// answered with a read of the address it was posted to, the prefix included.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_HOST_003_AC2_ACrossSiteReturnIsReadAgainUnderThePrefixAsync()
    {
        await using var deployment = new Deployment(prefix: Prefix);
        var context = new DefaultHttpContext();

        context.Request.Method = "POST";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("identity.example.test");
        context.Request.Path = new PathString(Prefix + "/account/profile");
        context.Request.QueryString = new QueryString("?paid=1");
        context.Request.Headers["Sec-Fetch-Site"] = "cross-site";
        context.Request.Headers["Sec-Fetch-Mode"] = "navigate";
        context.Request.Headers["Sec-Fetch-Dest"] = "document";
        context.Response.Body = new ResponseBody();

        await deployment.SendAsync(context);

        Assert.Equal(StatusCodes.Status303SeeOther, context.Response.StatusCode);
        Assert.Equal(Prefix + "/account/profile?paid=1", context.Response.Headers.Location.ToString());
    }

    /// <summary>
    /// LIB-HOST-003 AC2: the sign-on reaches the provider at the address the host
    /// declared it mounted at, prefix and all: the request is pushed there, the browser
    /// is sent to its authorization endpoint there, and the code is exchanged there.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_HOST_003_AC2_TheSignOnReachesTheProviderUnderItsPrefixAsync()
    {
        await using var deployment = new Deployment(
            prefix: Prefix,
            signIn: new AuthenticationAddresses(Origin + "/signin", Origin + Prefix));

        await deployment.Clients.RecordAsync(
            new OidcClient(
                Client,
                Client,
                OidcClientKind.BrowserApplication,
                Origin + Prefix + SignOn.ReturnPath,
                ["openid"]),
            OpaqueToken.Of(Secret).Fingerprint(),
            DateTimeOffset.MinValue,
            TestContext.Current.CancellationToken);

        Browser holder = await HolderAsync(deployment);
        var arriving = new Browser(deployment);

        Answer forwarded = await arriving.SendAsync("GET", Prefix + SignOn.StartPath);
        Answer issued = await holder.SendAsync("GET", Local(forwarded));
        Answer established = await arriving.SendAsync("GET", Local(issued));

        Assert.StartsWith(Origin + Prefix + "/oidc/authorize?", forwarded.Location, StringComparison.Ordinal);
        Assert.StartsWith(Origin + Prefix + SignOn.ReturnPath + "?", issued.Location, StringComparison.Ordinal);
        Assert.Equal(StatusCodes.Status302Found, established.Status);
        Assert.Equal("/", established.Location);
        Assert.Equal(
            [Prefix + "/oidc/par", Prefix + "/oidc/token"],
            deployment.Provider.Asked.Select(asked => asked.AbsolutePath));
    }

    // The deployment is one origin here, so what a browser would follow across two
    // applications is followed as a path of the one it is talking to.
    private static string Local(Answer answered) =>
        (answered.Location ?? throw new InvalidOperationException("The answer forwarded nowhere."))[Origin.Length..];

    // A browser at the authentication application holding the record every other
    // application's session stands on (AUTH-SESS-004).
    private static async Task<Browser> HolderAsync(Deployment deployment)
    {
        using var randomness = RandomNumberGenerator.Create();
        var secret = OpaqueToken.Draw(randomness);
        var holder = new Browser(deployment);

        await deployment.Sessions.AddAsync(
            Session.Begin(
                SessionId.New(TimeProvider.System),
                SubjectId.New(randomness),
                new Assurance(AssuranceLevel.Aal1, PhishingResistant: false),
                new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
                deployment.Clock.GetUtcNow(),
                TimeSpan.FromDays(1),
                TimeSpan.FromDays(30),
                satisfiesEveryGate: false),
            secret.Fingerprint(),
            OpaqueToken.Draw(randomness).Fingerprint(),
            TestContext.Current.CancellationToken);

        holder.Hold(BrowserCookies.Session, secret.Value);

        return holder;
    }
}
