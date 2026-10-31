using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Oidc;
using Janus.Hosting.Tests.Oidc;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// A host built on the framework's web application that calls no authentication
/// middleware of its own, where the framework adds one ahead of the host's middleware:
/// the provider answers its routes only after its profile's stages (BFF-ORDER-001 AC8,
/// D-230).
/// </summary>
[Trait("kind", "unit")]
public sealed class FrameworkAuthenticationTests : IAsyncDisposable
{
    private const string ContinueTo = "https://identity.example.test/oidc/authorize/continue";

    private static readonly IPAddress Flooding = IPAddress.Parse("198.51.100.7");

    private static readonly IPAddress Neighbour = IPAddress.Parse("203.0.113.9");

    private readonly ConcurrentQueue<(string Path, bool Governed)> _reached = new();

    private readonly Deployment _deployment;

    private bool _faulting;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameworkAuthenticationTests"/> class.
    /// </summary>
    public FrameworkAuthenticationTests() =>
        _deployment = new Deployment(composed: true, services: Probed);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _deployment.DisposeAsync();

    /// <summary>
    /// BFF-ORDER-001 AC8: an authorization request that the flood limit refuses is
    /// answered by the stage and reaches no provider endpoint, so the reference it
    /// carries is not spent, and the same request from a source under the limit is
    /// answered by the provider.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AC8_AnAuthorizationRequestAStageRefusesReachesNoProviderEndpointAsync()
    {
        await RelyingParty.RegisteredAsync(_deployment);

        string authorization = await RelyingParty.AuthorizeAsync(_deployment, RelyingParty.Application, silent: false);

        _deployment.Configuration.Set(Settings.AbuseSourceRateLimit, 2);
        _ = await new Browser(_deployment).SendAsync("GET", "/auth/session", source: Flooding);
        _ = await new Browser(_deployment).SendAsync("GET", "/auth/session", source: Flooding);

        Answer refused = await new Browser(_deployment).SendAsync("GET", authorization, source: Flooding);

        Assert.Equal(StatusCodes.Status429TooManyRequests, refused.Status);
        Assert.Equal(ErrorCodes.Throttled.ToString(), refused.Text("code"));
        Assert.DoesNotContain(_reached, reached => reached.Path.StartsWith("/oidc/authorize", StringComparison.Ordinal));

        Answer answered = await new Browser(_deployment).SendAsync("GET", authorization, source: Neighbour);

        Assert.Equal(StatusCodes.Status302Found, answered.Status);
        Assert.StartsWith("https://identity.example.test/signin", RelyingParty.Where(answered), StringComparison.Ordinal);
        Assert.Contains(_reached, reached => reached.Path == "/oidc/authorize");
    }

    /// <summary>
    /// BFF-ORDER-001 AC8: the provider acts on a pushed request and on a token request
    /// only once the machine profile's stages have governed it, so a fault of the
    /// provider's at either is answered by the profile as the library answers a fault.
    /// </summary>
    /// <param name="path">The provider's route.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData("/oidc/par")]
    [InlineData("/oidc/token")]
    public async Task BFF_ORDER_001_AC8_AMachineRouteOfTheProviderIsAnsweredOnlyInsideItsProfileAsync(string path)
    {
        await RelyingParty.RegisteredAsync(_deployment);
        _faulting = true;

        Answer answered = await new Machine(_deployment).PostAsync(
            path,
            RelyingParty.Request(RelyingParty.Application, silent: false, RelyingParty.Destination, "openid"));

        Assert.Equal(StatusCodes.Status500InternalServerError, answered.Status);
        Assert.Equal(ErrorCodes.SystemFault.ToString(), answered.Text("code"));
        Assert.Equal([(path, true)], _reached);
    }

    /// <summary>
    /// BFF-ORDER-001 AC8, AUTH-SESS-012: an authorization request that finds no session
    /// is kept for its browser, which the stages resolved before the provider read it,
    /// so the sign-in that completes on that browser says where it waits.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AC8_AnAuthorizationRequestThatFindsNoSessionIsKeptForItsBrowserAsync()
    {
        Browser browser = await RelyingParty.PreparedAsync(_deployment);

        Assert.Equal(StatusCodes.Status204NoContent, (await browser.SendAsync("POST", "/auth/logout")).Status);

        Answer forwarded = await browser.SendAsync(
            "GET",
            await RelyingParty.AuthorizeAsync(_deployment, RelyingParty.Application, silent: false));
        Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        Answer signedIn = await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", began.Text("challengeId")),
            ("factor", "password"),
            ("value", Flow.Password));

        Assert.Equal(StatusCodes.Status302Found, forwarded.Status);
        Assert.Equal("complete", signedIn.Text("status"));
        Assert.Equal(ContinueTo, signedIn.Text("continueTo"));
    }

    /// <summary>
    /// BFF-ORDER-001 AC8: the provider is the one handler the library gives the
    /// framework's authentication, no scheme is one the framework authenticates every
    /// request with, and the library registers nothing for which the framework would add
    /// an authorization middleware ahead of the host's.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AC8_TheProviderIsTheOneHandlerTheLibraryGivesTheFrameworkAsync()
    {
        await using AsyncServiceScope scope = _deployment.Scope();
        IAuthenticationSchemeProvider schemes = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();
        IServiceProviderIsService registered = scope.ServiceProvider.GetRequiredService<IServiceProviderIsService>();

        IEnumerable<AuthenticationScheme> handling = await schemes.GetRequestHandlerSchemesAsync();

        Assert.Null(await schemes.GetDefaultAuthenticateSchemeAsync());
        Assert.Equal(
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
            handling.Select(scheme => scheme.Name));
        Assert.False(registered.IsService(typeof(IAuthorizationHandlerProvider)));
    }

    // The provider's first step after the library's own, which records each request the
    // provider acts on and whether the machine profile governed it, and faults where the
    // test says so.
    private void Probed(IServiceCollection services) =>
        _ = services.AddOpenIddict().AddServer(server => server.AddEventHandler<OpenIddictServerEvents.ProcessRequestContext>(
            handler => handler
                .UseInlineHandler(context =>
                {
                    HttpRequest request = context.Transaction.GetHttpRequest()!;

                    _reached.Enqueue((
                        request.Path.Value ?? string.Empty,
                        request.HttpContext.Features.Get<Janus.Hosting.Bff.MachineGoverned>() is not null));

                    return _faulting && request.Path.StartsWithSegments("/oidc", StringComparison.Ordinal)
                        ? throw new InvalidOperationException("The provider faulted.")
                        : ValueTask.CompletedTask;
                })
                .SetOrder(OnlyPastTheProfile.Order + 1)));
}
