using System;
using System.Threading.Tasks;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Oidc;

/// <summary>
/// The way back to the application after a sign-in: the authorization a browser with no
/// session is kept for, the answer that completes the sign-in and says where it waits,
/// and the continuation that issues the code once (AUTH-SESS-012 AC10, D-224, D-226,
/// D-228).
/// </summary>
[Trait("kind", "unit")]
public sealed class ContinuationTests : IAsyncDisposable
{
    private const string Continuation = "/oidc/authorize/continue";
    private const string ContinueTo = "https://identity.example.test" + Continuation;

    private readonly Deployment _deployment = new();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _deployment.DisposeAsync();

    /// <summary>
    /// AUTH-SESS-012 AC10: a request that is not silent, met with no session, is
    /// answered after the sign-in completes: the completing answer carries
    /// <c>continueTo</c>, following it issues the code to the client's registered
    /// destination with its <c>state</c>, the code is exchanged, and a second follow
    /// issues nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC10_TheSignInThatCompletesIsSentBackWithTheCodeOnceAsync()
    {
        Browser browser = await SignedOutAsync();
        Answer forwarded = await browser.SendAsync(
            "GET",
            await RelyingParty.AuthorizeAsync(_deployment, RelyingParty.Application, silent: false));
        Answer signedIn = await SignedInAsync(browser);
        Answer continued = await browser.SendAsync("GET", Continuation);
        Answer exchanged = await new Machine(_deployment).PostAsync(
            "/oidc/token",
            RelyingParty.Code(RelyingParty.Returned(continued, "code"), RelyingParty.Application));
        Answer again = await browser.SendAsync("GET", Continuation);

        Assert.Equal(StatusCodes.Status302Found, forwarded.Status);
        Assert.StartsWith("https://identity.example.test/signin", RelyingParty.Where(forwarded), StringComparison.Ordinal);
        Assert.Equal("complete", signedIn.Text("status"));
        Assert.Equal(ContinueTo, signedIn.Text("continueTo"));
        Assert.Equal(StatusCodes.Status302Found, continued.Status);
        Assert.StartsWith(RelyingParty.Destination + "?", RelyingParty.Where(continued), StringComparison.Ordinal);
        Assert.Equal("the-state", RelyingParty.Returned(continued, "state"));
        Assert.Equal(StatusCodes.Status200OK, exchanged.Status);
        Assert.Equal(StatusCodes.Status302Found, again.Status);
        Assert.Equal("/", RelyingParty.Where(again));
    }

    /// <summary>
    /// AUTH-SESS-012 AC10: one past <c>oidc.continuation.lifetime</c>, counted from the
    /// request and not from the sign-in, issues nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC10_AFollowPastTheContinuationLifetimeIssuesNothingAsync()
    {
        Browser browser = await SignedOutAsync();

        _ = await browser.SendAsync(
            "GET",
            await RelyingParty.AuthorizeAsync(_deployment, RelyingParty.Application, silent: false));
        _deployment.Clock.Advance(TimeSpan.FromMinutes(20));

        Answer signedIn = await SignedInAsync(browser);

        _deployment.Clock.Advance(TimeSpan.FromMinutes(11));

        Answer read = await browser.SendAsync("GET", "/auth/session");
        Answer continued = await browser.SendAsync("GET", Continuation);

        Assert.Equal(ContinueTo, signedIn.Text("continueTo"));
        Assert.False(read.Json().TryGetProperty("continueTo", out _));
        Assert.Equal(StatusCodes.Status302Found, continued.Status);
        Assert.Equal("/", RelyingParty.Where(continued));
    }

    /// <summary>
    /// AUTH-SESS-012 (D-224): a browser has one authorization waiting, a later request
    /// replacing the earlier, and a silent request, which is told it has no session, is
    /// never kept.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC10_ALaterRequestReplacesTheOneWaitingAsync()
    {
        Browser browser = await SignedOutAsync();
        (string Name, string? Value)[] later = RelyingParty.With(
            RelyingParty.Request(RelyingParty.Application, silent: false, RelyingParty.Destination, "openid email"),
            "state",
            "the-later-state");

        _ = await browser.SendAsync(
            "GET",
            await RelyingParty.AuthorizeAsync(_deployment, RelyingParty.Application, silent: false));
        _ = await browser.SendAsync(
            "GET",
            RelyingParty.Authorization(
                RelyingParty.Application,
                (await RelyingParty.PushAsync(_deployment, later)).Text("request_uri")));
        _ = await browser.SendAsync(
            "GET",
            await RelyingParty.AuthorizeAsync(_deployment, RelyingParty.Application, silent: true));

        int waiting = _deployment.Waiting.Count;

        _ = await SignedInAsync(browser);

        Answer continued = await browser.SendAsync("GET", Continuation);

        Assert.Equal(1, waiting);
        Assert.Equal("the-later-state", RelyingParty.Returned(continued, "state"));
    }

    /// <summary>
    /// AUTH-SESS-012 (D-224): the session a sign-in a navigation completed is read with
    /// says where its authorization waits, until the continuation spends it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC10_TheSessionSaysWhereItsAuthorizationWaitsAsync()
    {
        Browser waiting = await SignedOutAsync();

        _ = await waiting.SendAsync(
            "GET",
            await RelyingParty.AuthorizeAsync(_deployment, RelyingParty.Application, silent: false));
        _ = await SignedInAsync(waiting);

        Answer before = await waiting.SendAsync("GET", "/auth/session");

        _ = await waiting.SendAsync("GET", Continuation);

        Answer after = await waiting.SendAsync("GET", "/auth/session");

        Assert.Equal(ContinueTo, before.Text("continueTo"));
        Assert.False(after.Json().TryGetProperty("continueTo", out _));
    }

    /// <summary>
    /// AUTH-SESS-012 AC10: a sign-in that completes once the request's lifetime has run
    /// out finds nothing waiting, so its answer carries no <c>continueTo</c> and the
    /// continuation issues nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC10_ASignInPastTheContinuationLifetimeCarriesNothingAsync()
    {
        Browser browser = await SignedOutAsync();

        _ = await browser.SendAsync(
            "GET",
            await RelyingParty.AuthorizeAsync(_deployment, RelyingParty.Application, silent: false));
        _deployment.Clock.Advance(TimeSpan.FromMinutes(31));

        Answer signedIn = await SignedInAsync(browser);
        Answer continued = await browser.SendAsync("GET", Continuation);

        Assert.Equal("complete", signedIn.Text("status"));
        Assert.False(signedIn.Json().TryGetProperty("continueTo", out _));
        Assert.Equal("/", RelyingParty.Where(continued));
    }

    /// <summary>
    /// AUTH-SESS-012, API-REDIR-001 (D-228): with nothing waiting the continuation sends
    /// the browser to the origin of the registered address of
    /// <c>redirect.defaultclient</c>, and a browser with no session finds nothing
    /// waiting.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_012_AC10_WithNothingWaitingTheBrowserGoesToTheDefaultClientAsync()
    {
        await RelyingParty.RegisteredAsync(_deployment);
        _deployment.Configuration.Set(Settings.RedirectDefaultClient, RelyingParty.Application);

        Answer continued = await new Browser(_deployment).SendAsync("GET", Continuation);

        Assert.Equal(StatusCodes.Status302Found, continued.Status);
        Assert.Equal("https://app.example.test", RelyingParty.Where(continued));
    }

    // A person who registered on this browser and signed out, so that the browser holds
    // no session and is one the person has used before.
    private async Task<Browser> SignedOutAsync()
    {
        Browser browser = await RelyingParty.PreparedAsync(_deployment);

        Assert.Equal(StatusCodes.Status204NoContent, (await browser.SendAsync("POST", "/auth/logout")).Status);

        return browser;
    }

    // AUTH-FACT-001: the person signs in with the password registration set.
    private static async Task<Answer> SignedInAsync(Browser browser)
    {
        Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));

        return await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", began.Text("challengeId")),
            ("factor", "password"),
            ("value", Flow.Password));
    }
}
