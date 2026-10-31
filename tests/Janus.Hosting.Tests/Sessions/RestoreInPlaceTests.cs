using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Sessions;

/// <summary>
/// The restore in place of a session that lapsed for inactivity, on the application
/// where the expiry met it: the cookie the browser keeps, the two routes that alone
/// read it, and the token they are carried by (AUTH-SESS-005, BFF-STEP-001,
/// BFF-ORDER-001 stage 5, D-224 item 13, D-226).
/// </summary>
[Trait("kind", "unit")]
public sealed class RestoreInPlaceTests : IAsyncDisposable
{
    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment a browser can register against, which is how a test here gets a
    /// session to let lapse.
    /// </summary>
    public RestoreInPlaceTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _deployment.DisposeAsync();

    /// <summary>
    /// BFF-STEP-001 AC4 and AUTH-SESS-005 AC4: a session that lapsed for inactivity
    /// under a policy requiring AAL2 is answered <c>single-factor</c>, its cookie
    /// surviving the answer; the restore begins and completes on that cookie and the
    /// token of the browser's pre-authentication session, the session keeps its
    /// record, the pre-authentication session ends, and the interrupted request,
    /// retried, is answered on the restored session and its new token.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_STEP_001_AC4_AnIdleSessionIsRestoredInPlaceAndTheRequestRetriedAsync()
    {
        Browser browser = await LapsedAsync();
        string lapsed = browser.Cookies[BrowserCookies.Session];
        string token = browser.Cookies[BrowserCookies.Csrf];

        Answer expired = await browser.SendAsync("GET", "/account/");

        string first = browser.Cookies[BrowserCookies.PreAuthentication];
        string keptByTheAnswer = browser.Cookies[BrowserCookies.Session];
        string carriedBy = browser.Cookies[BrowserCookies.Csrf];

        Answer began = await browser.SendAsync("POST", "/auth/restore/begin");
        JsonElement webAuthn = began.Json().GetProperty("webauthn");
        string keptByTheBegin = browser.Cookies[BrowserCookies.Session];

        Answer restored = await browser.SendAsync(
            "POST",
            "/auth/restore",
            ("challengeId", began.Text("challengeId")),
            ("factor", "password"),
            ("value", Flow.Password));

        string rotated = browser.Cookies[BrowserCookies.Session];
        Answer retried = await browser.SendAsync("GET", "/account/");
        Answer changed = await browser.SendAsync("POST", "/auth/logout");

        Assert.Equal(StatusCodes.Status401Unauthorized, expired.Status);
        Assert.Equal("single-factor", Asked(expired));
        Assert.Equal((lapsed, lapsed), (keptByTheAnswer, keptByTheBegin));
        Assert.NotEqual(lapsed, rotated);
        Assert.NotEqual(token, carriedBy);
        Assert.Equal(StatusCodes.Status200OK, began.Status);
        Assert.Equal(["password"], began.Json().GetProperty("factors").EnumerateArray().Select(factor => factor.GetString()));
        Assert.Equal("required", webAuthn.GetProperty("userVerification").GetString());
        Assert.Empty(webAuthn.GetProperty("allowCredentials").EnumerateArray());
        Assert.Equal(StatusCodes.Status200OK, restored.Status);
        Assert.Equal("complete", restored.Text("status"));
        Assert.DoesNotContain(BrowserCookies.PreAuthentication, browser.Cookies.Keys, StringComparer.Ordinal);
        Assert.Null(await _deployment.Contacts.FindAsync(
            Janus.Authentication.OpaqueToken.Of(first).Fingerprint(),
            TestContext.Current.CancellationToken));
        Assert.Equal(StatusCodes.Status200OK, retried.Status);
        Assert.Equal(StatusCodes.Status204NoContent, changed.Status);
    }

    /// <summary>
    /// BFF-STEP-001 AC4 and BFF-ORDER-001 stage 5: no route but the two restore routes
    /// accepts the cookie the restore admits; a route that requires a session answers
    /// <c>single-factor</c>, one that requires none answers as for a browser that
    /// carried none, on the pre-authentication session's token, and the browser keeps
    /// the cookie through both.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_STEP_001_AC4_NoOtherRouteAcceptsTheCookieTheRestoreAdmitsAsync()
    {
        Browser browser = await LapsedAsync();
        string lapsed = browser.Cookies[BrowserCookies.Session];

        Answer sessions = await browser.SendAsync("GET", "/account/sessions");
        Answer begun = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        Answer read = await browser.SendAsync("GET", "/auth/session");

        Assert.Equal(StatusCodes.Status401Unauthorized, sessions.Status);
        Assert.Equal("single-factor", Asked(sessions));
        Assert.Equal(StatusCodes.Status200OK, begun.Status);
        Assert.NotEmpty(begun.Text("challengeId"));
        Assert.Equal(StatusCodes.Status401Unauthorized, read.Status);
        Assert.Equal("single-factor", Asked(read));
        Assert.Equal(lapsed, browser.Cookies[BrowserCookies.Session]);
    }

    /// <summary>
    /// BFF-ORDER-001 stage 5 and AUTH-SESS-005 AC4: once its absolute window has
    /// passed the restore no longer admits the cookie, which is cleared by the answer
    /// that says so, <c>full</c>; both restore routes then answer <c>full</c> too.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_ACookieTheRestoreNoLongerAdmitsIsClearedAsync()
    {
        Browser browser = await LapsedAsync();

        Answer lapsed = await browser.SendAsync("GET", "/account/");

        _deployment.Clock.Advance(TimeSpan.FromDays(300));

        Answer ended = await browser.SendAsync("GET", "/account/");

        bool cleared = !browser.Cookies.ContainsKey(BrowserCookies.Session);

        Answer begun = await browser.SendAsync("POST", "/auth/restore/begin");
        Answer restored = await browser.SendAsync(
            "POST",
            "/auth/restore",
            ("challengeId", "a-handle-nothing-opened"),
            ("factor", "password"),
            ("value", Flow.Password));

        Assert.Equal("single-factor", Asked(lapsed));
        Assert.Equal("full", Asked(ended));
        Assert.True(cleared);
        Assert.Equal(StatusCodes.Status401Unauthorized, begun.Status);
        Assert.Equal("full", Asked(begun));
        Assert.Equal(StatusCodes.Status401Unauthorized, restored.Status);
        Assert.Equal("full", Asked(restored));
    }

    /// <summary>
    /// AUTH-SESS-005 AC3a: a customer's idle expiry is no restore's: the cookie is
    /// cleared by the answer, <c>full</c>, and the restore's begin answers
    /// <c>full</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_005_AC3a_ACustomersIdleExpiryIsRestoredByNoRouteAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Clock.Advance(TimeSpan.FromDays(91));

        Answer expired = await browser.SendAsync("GET", "/account/");

        bool cleared = !browser.Cookies.ContainsKey(BrowserCookies.Session);

        Answer begun = await browser.SendAsync("POST", "/auth/restore/begin");

        Assert.Equal("full", Asked(expired));
        Assert.True(cleared);
        Assert.Equal(StatusCodes.Status401Unauthorized, begun.Status);
        Assert.Equal("full", Asked(begun));
    }

    // A browser signed in on a session that has lapsed for inactivity under a policy
    // requiring AAL2, inside its absolute window.
    private async Task<Browser> LapsedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Configuration.Set(
            Settings.PolicyDefault,
            Janus.Core.Policies.SystemDefault with { RequiredAssurance = AssuranceLevel.Aal2 });
        _deployment.Clock.Advance(TimeSpan.FromDays(91));

        return browser;
    }

    private static string? Asked(Answer answer) =>
        answer.Json().TryGetProperty("details", out JsonElement details)
        && details.TryGetProperty("reauthenticate", out JsonElement asked)
            ? asked.GetString()
            : null;
}
