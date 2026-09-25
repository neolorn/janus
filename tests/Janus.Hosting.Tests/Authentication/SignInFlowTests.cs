using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Authentication;

/// <summary>
/// Signing in as a browser does it: what the answer carries, what the browser is
/// left holding, and where a link opened elsewhere gets to (chapter 09 section 3).
/// </summary>
[Trait("kind", "unit")]
public sealed class SignInFlowTests : IAsyncDisposable
{
    private const string Session = "__Host-identity-session";
    private const string Browsers = "__Host-identity-browser";
    private const string Language = "en";

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment that can send, whose messages carry the code and the token as the
    /// shipped templates do, and whose policy admits the link a test asks for.
    /// </summary>
    public SignInFlowTests()
    {
        Flow.Prepare(_deployment);

        _deployment.Configuration.Set(
            Settings.PolicyDefault,
            Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>(
                    [.. Policies.SystemDefault.LoginFactors, Factor.EmailLink]),
            });

        foreach (SendKind kind in new[] { SendKind.Email, SendKind.Sms })
        {
            _deployment.Templates.Set(
                MessageKind.SignInLink,
                kind,
                Language,
                new MessageTemplate(kind is SendKind.Email ? "link" : null, "{code} {token}"));
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// AUTH-FACT-016 AC1: a password from a browser the account has not been seen on
    /// is held for a code, and the browser is left holding no session; the code sent
    /// to the primary address completes it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_016_AC1_AHeldSignInLeavesTheBrowserWithNoSessionAsync()
    {
        await RegisteredAsync();

        var browser = new Browser(_deployment);
        string challenge = await BegunAsync(browser);

        Answer held = await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "password"),
            ("value", Flow.Password));

        Assert.Equal(StatusCodes.Status200OK, held.Status);
        Assert.Equal("deviceVerificationRequired", held.Text("status"));
        Assert.False(browser.Cookies.ContainsKey(Session));

        Answer completed = await browser.SendAsync(
            "POST",
            "/auth/device/verify",
            ("challengeId", challenge),
            ("code", Emailed()));

        Assert.Equal(StatusCodes.Status200OK, completed.Status);
        Assert.Equal("complete", completed.Text("status"));
        Assert.True(browser.Cookies.ContainsKey(Session));
        Assert.True(browser.Cookies.ContainsKey(Browsers));
    }

    /// <summary>
    /// AUTH-FACT-003 AC4: the link signs in the browser that asked for it and no
    /// other; the browser that merely opened it is left holding nothing and is given
    /// the code to type where the sign-in began.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_003_AC4_OnlyTheAskingBrowserIsSignedInByTheLinkAsync()
    {
        // The new-device check is what holds a first sign-in from an unseen browser
        // (AUTH-FACT-016); what the link does is this test's subject, so the check is
        // off and the press is answered by the sign-in itself.
        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        await RegisteredAsync();

        var asking = new Browser(_deployment);
        string challenge = await BegunAsync(asking);

        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await asking.SendAsync("POST", "/auth/link", ("identifier", Flow.Address))).Status);

        string token = Flow.Token(_deployment, IdentifierKind.Email);
        var elsewhere = new Browser(_deployment);

        _ = await elsewhere.SendAsync("GET", "/auth/session");

        Answer opened = await elsewhere.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "emailLink"),
            ("linkToken", token),
            ("press", true));

        Assert.Equal(StatusCodes.Status200OK, opened.Status);
        Assert.False(opened.Json().GetProperty("sameBrowser").GetBoolean());
        Assert.Equal(6, opened.Text("code").Length);
        Assert.False(elsewhere.Cookies.ContainsKey(Session));

        Answer pressed = await asking.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "emailLink"),
            ("linkToken", token),
            ("press", true));

        Assert.Equal(StatusCodes.Status200OK, pressed.Status);
        Assert.Equal("complete", pressed.Text("status"));
        Assert.Equal("aal1", pressed.Text("assuranceLevel"));
        Assert.True(asking.Cookies.ContainsKey(Session));
    }

    /// <summary>
    /// API-LAND-001 AC1 and AC3: what a link the library sends resolves to is data
    /// for the frontend to render, never a page of the library's own, and the landing
    /// route reaches it with one call.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_LAND_001_AC1_ALinkResolvesToDataAndNotToAPageAsync()
    {
        await RegisteredAsync();

        var asking = new Browser(_deployment);
        string challenge = await BegunAsync(asking);

        _ = await asking.SendAsync("POST", "/auth/link", ("identifier", Flow.Address));

        var elsewhere = new Browser(_deployment);

        _ = await elsewhere.SendAsync("GET", "/auth/session");

        Answer landed = await elsewhere.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "emailLink"),
            ("linkToken", Flow.Token(_deployment, IdentifierKind.Email)),
            ("press", true));

        Assert.Equal(StatusCodes.Status200OK, landed.Status);
        Assert.StartsWith("{", landed.Body, StringComparison.Ordinal);
        Assert.True(landed.Json().TryGetProperty("code", out _));
    }

    /// <summary>
    /// API-LAND-001 AC2: a token that resolves to nothing yields a code for the
    /// frontend to render, and no page and no session.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_LAND_001_AC2_AnUnknownTokenYieldsACodeAndNoSessionAsync()
    {
        await RegisteredAsync();

        var browser = new Browser(_deployment);
        string challenge = await BegunAsync(browser);

        Answer landed = await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "emailLink"),
            ("linkToken", "nothing-answers-to-this"),
            ("press", true));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, landed.Status);
        Assert.Equal(ErrorCodes.CodeExpired.ToString(), landed.Text("code"));
        Assert.False(browser.Cookies.ContainsKey(Session));
    }

    /// <summary>
    /// A browser that holds no session is told so by the session endpoint, which is
    /// the one place a frontend asks what it is holding (chapter 09 section 3).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task GetSession_NoSession_RefusesAsync()
    {
        var browser = new Browser(_deployment);

        Answer read = await browser.SendAsync("GET", "/auth/session");

        Assert.Equal(StatusCodes.Status401Unauthorized, read.Status);
    }

    /// <summary>
    /// CONV-CODE-006 AC2: ending a sign-in link with a body that carries no link token
    /// is refused naming the member before the service is reached, which would answer
    /// any token it resolves to nothing with no content.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_CODE_006_AC2_AnAbandonMissingItsTokenIsRefusedBeforeTheServiceAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        Answer abandoned = await browser.SendAsync("POST", "/auth/link/abandon", "{}");

        Assert.Equal(StatusCodes.Status400BadRequest, abandoned.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), abandoned.Text("code"));
        Assert.Equal("linkToken", abandoned.Json().GetProperty("details").GetProperty("member").GetString());
    }

    // The code the last message carried, which is the one the step under test sent.
    private string Emailed() => _deployment.Mail.Taken[^1].Body.Split(' ')[0];

    // The account a sign-in is against, and the interval its registration messages
    // opened left behind (AUTH-ABUSE-004).
    /// <summary>
    /// REG-IDENT-003 AC1: the phone the account verified at registration opens a
    /// sign-in exactly as the primary email does, and the same entries are offered.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_003_AC1_ANonPrimaryVerifiedIdentifierOpensTheSameSignInAsync()
    {
        await RegisteredAsync();

        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        Answer byEmail = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        Answer byPhone = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Number));

        Assert.Equal(StatusCodes.Status200OK, byPhone.Status);
        Assert.Equal(Offered(byEmail), Offered(byPhone));
        Assert.NotEmpty(byPhone.Text("challengeId"));
    }

    /// <summary>
    /// REG-IDENT-003 AC2: an identifier no account holds opens a sign-in that answers
    /// with the same status, the same fields and the same length as one that is held,
    /// so nothing about which it was crosses the boundary.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_003_AC2_AnUnknownIdentifierAnswersAsAHeldOneDoesAsync()
    {
        await RegisteredAsync();

        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        Answer held = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        Answer unheld = await browser.SendAsync(
            "POST",
            "/auth/begin",
            ("identifier", "nobody@example.test"));

        Assert.Equal(held.Status, unheld.Status);
        Assert.Equal(Fields(held), Fields(unheld));
        Assert.Equal(Offered(held), Offered(unheld));
        Assert.Equal(held.Body.Length, unheld.Body.Length);
    }

    /// <summary>
    /// IDN-ACCT-006 AC2: the address the account registered in lower case, typed in
    /// capitals, signs in to that account with its password.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_006_AC2_AnAddressTypedInCapitalsSignsInToTheAccountAsync()
    {
        // The new-device check would hold the first sign-in from this browser for a
        // code (AUTH-FACT-016); which account the address finds is this test's subject.
        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        await RegisteredAsync();

        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        Answer began = await browser.SendAsync(
            "POST",
            "/auth/begin",
            ("identifier", Flow.Address.ToUpperInvariant()));
        Answer signedIn = await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", began.Text("challengeId")),
            ("factor", "password"),
            ("value", Flow.Password));
        Answer session = await browser.SendAsync("GET", "/auth/session");

        Assert.Equal(StatusCodes.Status200OK, signedIn.Status);
        Assert.Equal("complete", signedIn.Text("status"));
        Assert.Equal(StatusCodes.Status200OK, session.Status);
        Assert.Equal(
            _deployment.Directory.Created[^1].Subject.Value,
            session.Json().GetProperty("subject").GetGuid());
    }

    // The entries the answer offered, which are the policy's and never the account's.
    private static IReadOnlyList<string> Offered(Answer answered) =>
    [
        .. answered.Json().GetProperty("available").EnumerateArray()
            .Select(entry => entry.GetString() ?? string.Empty),
    ];

    // The names the answer carries, in the order it carries them.
    private static IReadOnlyList<string> Fields(Answer answered) =>
        [.. answered.Json().EnumerateObject().Select(field => field.Name)];

    private async Task RegisteredAsync()
    {
        _ = await Flow.SignedInAsync(_deployment);

        _deployment.Clock.Advance(TimeSpan.FromMinutes(5));
    }

    // A browser that has a first contact and a sign-in open against the address the
    // registration flow registered.
    private static async Task<string> BegunAsync(Browser browser)
    {
        _ = await browser.SendAsync("GET", "/auth/session");

        Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));

        Assert.Equal(StatusCodes.Status200OK, began.Status);

        return began.Text("challengeId");
    }
}
