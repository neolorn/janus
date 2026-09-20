using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Janus.Hosting.Tests.Registration;

/// <summary>
/// The registration flow as a browser drives it: the pre-authentication session it
/// is bound to, what a browser without that cookie may do, and where the tokens are
/// (BFF-CSRF-005a, BFF-CSRF-005b, REG-SESS-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class RegistrationFlowTests : IAsyncDisposable
{
    private const string Begin = "{\"clientId\":\"web\"}";

    // The steps a browser that carries nothing tries in turn.
    private static readonly string[] Steps =
    [
        "/register/identifiers",
        "/register/phone/skip",
        "/register/confirm",
        "/register/terms",
    ];

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment whose messages carry the code and the link token, as the
    /// shipped templates do.
    /// </summary>
    public RegistrationFlowTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// BFF-CSRF-005a AC1: a browser that carries nothing is given a first contact by
    /// the endpoint it reached, before it has anything to bind a token to.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005a_AC1_FirstContactIssuesAPreAuthenticationSessionAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/register");

        Assert.True(browser.Cookies.ContainsKey("__Host-janus-preauth"));
        Assert.True(browser.Cookies.ContainsKey("__Host-janus-csrf"));
        Assert.Single(_deployment.Contacts.All);
    }

    /// <summary>
    /// BFF-CSRF-005a AC2: the token of a first contact is checked exactly as a
    /// session's is, so a state change without it is refused and one with it passes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005a_AC2_TheFirstContactTokenIsValidatedLikeASessionsAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/register");

        Answer without = await browser.SendAsync(
            "POST",
            "/register",
            Begin,
            header: true,
            token: false);

        Answer with = await browser.SendAsync("POST", "/register", ("clientId", "web"));

        Assert.Equal(StatusCodes.Status403Forbidden, without.Status);
        Assert.Equal(StatusCodes.Status201Created, with.Status);
    }

    /// <summary>
    /// BFF-CSRF-005a AC4: a first contact is not a sign-in, so nothing that needs an
    /// account answers to a browser that carries only one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005a_AC4_AFirstContactCarriesNoIdentityAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/register");

        Answer account = await browser.SendAsync("GET", "/account");

        Assert.Equal(StatusCodes.Status401Unauthorized, account.Status);
    }

    /// <summary>
    /// BFF-CSRF-005b AC1, REG-SESS-001 AC2: a browser that does not carry the first
    /// contact the registration was created under reaches none of it, and the stream
    /// answers it as an absent resource.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005b_AC1_AnotherBrowserReachesNoneOfTheRegistrationAsync()
    {
        Browser started = await Flow.BegunAsync(_deployment);
        var elsewhere = new Browser(_deployment);

        Answer state = await elsewhere.SendAsync("GET", "/register");
        Answer age = await elsewhere.SendAsync("PUT", "/register/age", ("dateOfBirth", "1990-01-01"));
        Answer stream = await elsewhere.SendAsync("GET", "/register/events");

        Assert.Equal(StatusCodes.Status401Unauthorized, state.Status);
        Assert.Equal(StatusCodes.Status401Unauthorized, age.Status);
        Assert.Equal(StatusCodes.Status404NotFound, stream.Status);

        Assert.Equal(
            StatusCodes.Status200OK,
            (await started.SendAsync("GET", "/register")).Status);
    }

    /// <summary>
    /// REG-SESS-001 AC2: the refusal holds for every step of the flow, not only the
    /// one that reads the state.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_SESS_001_AC2_EveryStepIsRefusedWithoutTheFirstContactAsync()
    {
        _ = await Flow.BegunAsync(_deployment);

        var elsewhere = new Browser(_deployment);

        _ = await elsewhere.SendAsync("GET", "/register/nothing");

        foreach (string step in Steps)
        {
            Answer refused = await elsewhere.SendAsync("POST", step, ("value", Flow.Address));

            Assert.Equal(StatusCodes.Status401Unauthorized, refused.Status);
        }
    }

    /// <summary>
    /// BFF-CSRF-005a AC3: the session the registration ends with takes the place of
    /// the first contact, which is cleared in the same answer.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005a_AC3_AuthenticationRotatesTheFirstContactAsync()
    {
        Browser browser = await Flow.SecuredAsync(_deployment);

        Answer completed = await browser.SendAsync(
            "POST",
            "/register/terms",
            ("termsVersion", "terms-3"),
            ("noticeVersion", "notice-2"));

        Assert.Equal(StatusCodes.Status201Created, completed.Status);
        Assert.True(browser.Cookies.ContainsKey("__Host-janus-session"));
        Assert.False(browser.Cookies.ContainsKey("__Host-janus-preauth"));
        Assert.Empty(_deployment.Contacts.All);
        Assert.Single(_deployment.Sessions.All);
    }

    /// <summary>
    /// A browser that already holds a session is answered with its account, and no
    /// registration is staged for it (REG-SESS-002).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BeginAsync_ABrowserAlreadySignedIn_IsAnsweredWithTheAccountAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        int staged = _deployment.Registrations.All.Count;

        Answer landing = await browser.SendAsync("POST", "/register", ("clientId", "web"));
        Answer account = await browser.SendAsync("GET", "/account");

        Assert.Equal(StatusCodes.Status200OK, landing.Status);
        Assert.Equal(account.Body, landing.Body);
        Assert.Equal(staged, _deployment.Registrations.All.Count);
    }

    /// <summary>
    /// BFF-CSRF-005b AC2: no route of the flow names a token, and the stream answers
    /// to the cookie alone: one put in the query opens nothing, and the browser that
    /// carries the cookie is read without presenting anything else.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005b_AC2_NoTokenTravelsInAUrlAsync()
    {
        Browser browser = await Flow.AwaitingAsync(_deployment);

        Assert.All(
            Routes(),
            pattern => Assert.DoesNotContain("token", pattern, StringComparison.OrdinalIgnoreCase));

        var elsewhere = new Browser(_deployment);

        Answer claimed = await elsewhere.SendAsync(
            "GET",
            "/register/events?token=" + Carried(browser));

        Answer carried = await browser.SendAsync("GET", "/register", token: false);

        Assert.Equal(StatusCodes.Status404NotFound, claimed.Status);
        Assert.Equal(StatusCodes.Status200OK, carried.Status);
    }

    /// <summary>
    /// BFF-CSRF-005b AC3: what the stream sends is the state document the polling
    /// endpoint answers with, so a frontend that loses the stream misses nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005b_AC3_TheStreamAndThePollCarryTheSameStateAsync()
    {
        Browser browser = await Flow.AwaitingAsync(_deployment);

        using var abort = new CancellationTokenSource();

        (Task running, ResponseBody written) = browser.Open("/register/events", abort.Token);

        await SettledAsync(written);
        await Flow.VerifiedAsync(_deployment, browser, IdentifierKind.Email);

        string streamed = await SentAsync(written);

        await abort.CancelAsync();

        try
        {
            await running;
        }
        catch (OperationCanceledException)
        {
        }

        Answer polled = await browser.SendAsync("GET", "/register");

        Assert.Equal(polled.Body, streamed);
    }

    /// <summary>
    /// BFF-CSRF-005b AC4: a link that is merely opened changes nothing, a press from
    /// the browser that asked for it verifies, and a press from anywhere else is
    /// answered with the code to type.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005b_AC4_OnlyAPressFromTheOriginatingBrowserVerifiesAsync()
    {
        Browser browser = await Flow.AwaitingAsync(_deployment);

        string token = Flow.Token(_deployment, IdentifierKind.Email);
        string identifier = Flow.Waiting(await browser.SendAsync("GET", "/register"), IdentifierKind.Email);

        Answer opened = await browser.SendAsync(
            "POST",
            "/register/verify/" + identifier,
            ("linkToken", token),
            ("press", false));

        Assert.Equal(StatusCodes.Status200OK, opened.Status);
        Assert.Equal(identifier, Flow.Waiting(await browser.SendAsync("GET", "/register"), IdentifierKind.Email));

        var elsewhere = new Browser(_deployment);

        _ = await elsewhere.SendAsync("GET", "/register");

        Answer pressed = await elsewhere.SendAsync(
            "POST",
            "/register/verify/" + identifier,
            ("linkToken", token),
            ("press", true));

        Assert.Equal(StatusCodes.Status200OK, pressed.Status);
        Assert.False(pressed.Json().GetProperty("sameBrowser").GetBoolean());
        Assert.Equal(6, pressed.Text("code").Length);
        Assert.Equal(identifier, Flow.Waiting(await browser.SendAsync("GET", "/register"), IdentifierKind.Email));

        Answer press = await browser.SendAsync(
            "POST",
            "/register/verify/" + identifier,
            ("linkToken", token),
            ("press", true));

        Assert.Equal(StatusCodes.Status204NoContent, press.Status);
        Assert.True(Proved(await browser.SendAsync("GET", "/register")));
    }

    // Whether the one staged identifier stands verified.
    private static bool Proved(Answer state) =>
        state.Json().GetProperty("identifiers")[0].GetProperty("verified").GetBoolean();

    // Every route the registration group mounts, as the endpoint table holds them.
    private List<string> Routes()
    {
        var patterns = new List<string>();

        foreach (Endpoint endpoint in _deployment.Endpoints)
        {
            if (endpoint is RouteEndpoint route
                && route.RoutePattern.RawText is { Length: > 0 } pattern
                && pattern.StartsWith("/register", StringComparison.Ordinal))
            {
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private static string Carried(Browser browser) => browser.Cookies["__Host-janus-preauth"];

    // The stream's first reading, after which it is parked on its interval and the
    // fakes are the test's alone again.
    private static async Task SettledAsync(ResponseBody written)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);

        Assert.Equal(0, written.Length);
    }

    // What the stream sent, once it has sent something.
    private static async Task<string> SentAsync(ResponseBody written)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (DateTime.UtcNow < giveUp && written.Length is 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }

        string sent = written.Taken();
        int at = sent.IndexOf("data: ", StringComparison.Ordinal);

        Assert.True(at >= 0, "The stream sent no state: [" + sent + "]");

        return sent[(at + 6)..].TrimEnd('\n');
    }
}
