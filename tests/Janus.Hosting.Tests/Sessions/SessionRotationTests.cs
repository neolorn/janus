using System;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Sessions;

/// <summary>
/// What a rotation changes at the boundary: at a step-up the identifier the session's
/// cookie carries, the record and its <see cref="SessionId"/> kept; at a sign-in the
/// first contact, which the sign-in session takes the place of (BFF-SESS-004,
/// AUTH-SESS-006, BFF-CSRF-005a, D-206).
/// </summary>
[Trait("kind", "unit")]
public sealed class SessionRotationTests : IAsyncDisposable
{
    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser and to sign it in again, the check of a
    /// browser the account was not seen on being out of the way of the sign-in.
    /// </summary>
    public SessionRotationTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// BFF-SESS-004 AC1 (D-206): a step-up gives the cookie a different identifier, and
    /// the identifier it now carries names the same record.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_004_AC1_AStepUpRotatesTheIdentifierTheCookieCarriesAndKeepsTheRecordAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        string before = browser.Cookies[BrowserCookies.Session];
        SessionId held = (await RecordAsync(before))!.Id;

        Answer stepped = await SteppedUpAsync(browser);
        string after = browser.Cookies[BrowserCookies.Session];

        Assert.Equal(StatusCodes.Status200OK, stepped.Status);
        Assert.NotEqual(before, after);
        Assert.Equal(held, (await RecordAsync(after))?.Id);
    }

    /// <summary>
    /// BFF-SESS-004 AC2 (D-206): the identifier the cookie carried before a step-up
    /// resolves nothing afterwards, so a request carrying it is one with no session.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_SESS_004_AC2_TheIdentifierBeforeAStepUpIsInvalidatedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        string before = browser.Cookies[BrowserCookies.Session];

        Assert.Equal(StatusCodes.Status200OK, (await SteppedUpAsync(browser)).Status);

        var planted = new Browser(_deployment);

        planted.Hold(BrowserCookies.Session, before);

        Answer carried = await planted.SendAsync("GET", "/auth/session");

        Assert.Null(await RecordAsync(before));
        Assert.Equal(StatusCodes.Status401Unauthorized, carried.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), carried.Text("code"));
        Assert.Equal(StatusCodes.Status200OK, (await browser.SendAsync("GET", "/auth/session")).Status);
    }

    /// <summary>
    /// AUTH-SESS-006 AC1, BFF-SESS-004 AC1: the identifier a browser carries after it
    /// signs in differs from the one its first contact carried before.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_006_AC1_TheIdentifierAfterSignInDiffersFromTheOneBeforeAsync()
    {
        (Browser browser, string first, Answer signedIn) = await SignedInAgainAsync();

        Assert.Equal(StatusCodes.Status200OK, signedIn.Status);
        Assert.Equal("complete", signedIn.Text("status"));
        Assert.NotEqual(first, browser.Cookies[BrowserCookies.Session]);
        Assert.False(browser.Cookies.ContainsKey(BrowserCookies.PreAuthentication));
    }

    /// <summary>
    /// AUTH-SESS-006 AC2, BFF-SESS-004 AC2, BFF-CSRF-005a AC3: signing in ends the first
    /// contact rather than leaving it beside the sign-in session, so the identifier it
    /// carried resolves nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_006_AC2_SigningInInvalidatesTheFirstContactAsync()
    {
        (_, string first, Answer signedIn) = await SignedInAgainAsync();

        Assert.Equal(StatusCodes.Status200OK, signedIn.Status);
        Assert.Null(await _deployment.Contacts.FindAsync(
            OpaqueToken.Of(first).Fingerprint(),
            TestContext.Current.CancellationToken));
    }

    // A step-up with the password the flow set, on a challenge the browser opens for the
    // address the flow registered (09 POST /auth/step-up).
    private static async Task<Answer> SteppedUpAsync(Browser browser)
    {
        Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));

        Assert.Equal(StatusCodes.Status200OK, began.Status);

        return await browser.SendAsync(
            "POST",
            "/auth/step-up",
            ("challengeId", began.Text("challengeId")),
            ("factor", "password"),
            ("value", Flow.Password));
    }

    // A browser the registered account signs in from with its password, having first
    // been issued a first contact, with the identifier that first contact carried.
    private async Task<(Browser Browser, string First, Answer SignedIn)> SignedInAgainAsync()
    {
        _ = await Flow.SignedInAsync(_deployment);

        _deployment.Clock.Advance(TimeSpan.FromMinutes(5));

        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        string first = browser.Cookies[BrowserCookies.PreAuthentication];
        Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));

        Assert.Equal(StatusCodes.Status200OK, began.Status);

        Answer signedIn = await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", began.Text("challengeId")),
            ("factor", "password"),
            ("value", Flow.Password));

        return (browser, first, signedIn);
    }

    private ValueTask<Session?> RecordAsync(string identifier) =>
        _deployment.Sessions.FindByFingerprintAsync(
            OpaqueToken.Of(identifier).Fingerprint(),
            TestContext.Current.CancellationToken);
}
