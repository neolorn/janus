using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Oidc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// The addresses the library composes while it is mounted under a prefix of the host's
/// choosing (LIB-HOST-003, API-CONV-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class PrefixedAddressTests
{
    private const string Prefix = "/identity";

    private const string Origin = "https://identity.example.test";

    private const string Client = "this-application";

    private const string Secret = "a-secret-the-deployment-set";

    private const string Trace = "mounted";

    // The three documents that belong to the site and not to the mount.
    private static readonly string[] SiteRoot =
    [
        "/.well-known/change-password",
        "/.well-known/passkey-endpoints",
        "/.well-known/webauthn",
    ];

    // A parameter of a route, as its pattern writes it.
    private static readonly Regex Parameter = new(@"\{[^}]+\}", RegexOptions.None, TimeSpan.FromSeconds(5));

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

        await deployment.Clients.AddAsync(
            new OidcClient(
                Client,
                Client,
                OidcClientKind.BrowserApplication,
                Origin + Prefix + SignOn.ReturnPath,
                ["openid"]),
            Encoding.UTF8.GetBytes(Secret),
            deployment.Clock.GetUtcNow(),
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

    /// <summary>
    /// LIB-HOST-003 AC1: a host mounts the library under a prefix of its choosing by
    /// mounting it in a branch and by nothing else. The calls that mount the profiles
    /// and the routes take no path, and under any prefix a request is answered as it
    /// is with none.
    /// </summary>
    /// <param name="prefix">The prefix the host chose.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("/identity")]
    [InlineData("/a/deeper/mount")]
    public async Task LIB_HOST_003_AC1_APrefixOfTheHostsChoosingAnswersAsNoneDoesAsync(string prefix)
    {
        await using var rooted = new Deployment();
        await using var prefixed = new Deployment(prefix: prefix);
        MethodInfo[] mounting =
        [
            typeof(PipelineProfiles).GetMethod(nameof(PipelineProfiles.UseMachineProfile))!,
            typeof(PipelineProfiles).GetMethod(nameof(PipelineProfiles.UseBrowserProfile))!,
            typeof(IdentityEndpoints).GetMethod(nameof(IdentityEndpoints.MapIdentityEndpoints))!,
        ];

        Answer atRoot = await new Browser(rooted).SendAsync("GET", "/auth/session", trace: Trace);
        Answer underPrefix = await new Browser(prefixed).SendAsync("GET", prefix + "/auth/session", trace: Trace);

        Assert.All(mounting, mount => Assert.Single(mount.GetParameters()));
        Assert.Equal(StatusCodes.Status401Unauthorized, atRoot.Status);
        Assert.Equal(atRoot.Status, underPrefix.Status);
        Assert.Equal(atRoot.Body, underPrefix.Body);
    }

    /// <summary>
    /// LIB-HOST-003 AC3: under a prefix every route the library maps is answered under
    /// it and not at the root, every address the discovery document publishes carries
    /// it, and the three site-root documents answer at the root and not under it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_HOST_003_AC3_EveryPathAndPublishedAddressCarriesThePrefixAsync()
    {
        await using var deployment = new Deployment(
            prefix: Prefix,
            signIn: new AuthenticationAddresses(Origin + "/signin", Origin + Prefix));
        (string Method, string Path)[] routes = Routes(deployment);
        (string Method, string Path)[] mounted = [.. routes.Where(route => !SiteRoot.Contains(route.Path))];
        List<string> unanswered = [];
        List<string> stray = [];

        foreach ((string method, string path) in mounted)
        {
            if (!await IsRoutedAsync(deployment, method, Prefix + path))
            {
                unanswered.Add(method + " " + path);
            }

            if (await IsRoutedAsync(deployment, method, path))
            {
                stray.Add(method + " " + path);
            }
        }

        var machine = new Machine(deployment);
        Answer discovery = await machine.GetAsync(Prefix + "/.well-known/openid-configuration", bearer: string.Empty);
        Answer undiscovered = await machine.GetAsync("/.well-known/openid-configuration", bearer: string.Empty);
        string[] published =
        [
            .. discovery.Json().EnumerateObject()
                .Where(member => member.Value.ValueKind is JsonValueKind.String)
                .Select(member => member.Value.GetString()!)
                .Where(value => value.StartsWith("https://", StringComparison.Ordinal)),
        ];

        Assert.NotEmpty(mounted);
        Assert.Empty(unanswered);
        Assert.Empty(stray);
        Assert.Equal(StatusCodes.Status404NotFound, undiscovered.Status);
        Assert.Contains(Origin + Prefix, published);
        Assert.All(published, address => Assert.StartsWith(Origin + Prefix, address, StringComparison.Ordinal));
        Assert.Equal(SiteRoot.Order(StringComparer.Ordinal), routes.Select(route => route.Path).Where(SiteRoot.Contains));

        foreach (string document in SiteRoot)
        {
            Assert.True(await IsRoutedAsync(deployment, "GET", document));
            Assert.False(await IsRoutedAsync(deployment, "GET", Prefix + document));
        }
    }

    // The deployment is one origin here, so what a browser would follow across two
    // applications is followed as a path of the one it is talking to.
    private static string Local(Answer answered) =>
        (answered.Location ?? throw new InvalidOperationException("The answer forwarded nowhere."))[Origin.Length..];

    // Every route the library maps, by method and by a path that fills each of its
    // parameters, in the order of the paths.
    private static (string Method, string Path)[] Routes(Deployment deployment) =>
    [
        .. deployment.Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint => endpoint.Metadata
                .GetMetadata<HttpMethodMetadata>()!.HttpMethods
                .Select(method => (method, Parameter.Replace(endpoint.RoutePattern.RawText!, "filled"))))
            .OrderBy(route => route.Item2, StringComparer.Ordinal),
    ];

    // Whether the application routes a request to an endpoint, which is whether the
    // library answers the path or leaves it to the host.
    private static async Task<bool> IsRoutedAsync(Deployment deployment, string method, string path)
    {
        var context = new DefaultHttpContext();

        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString(new Uri(Origin).Host);
        context.Request.Path = new PathString(path);
        context.Response.Body = new ResponseBody();

        await deployment.SendAsync(context);

        return context.GetEndpoint() is not null;
    }

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
                breakGlassReason: null),
            secret.Fingerprint(),
            OpaqueToken.Draw(randomness).Fingerprint(),
            TestContext.Current.CancellationToken);

        holder.Hold(BrowserCookies.Session, secret.Value);

        return holder;
    }
}
