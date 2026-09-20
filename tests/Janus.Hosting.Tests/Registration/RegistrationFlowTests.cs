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
    private const string Client = "web";
    private const string Address = "person@example.test";
    private const string Number = "+441632960011";
    private const string Password = "orangemarmalade";

    private const StringComparison Comparison = StringComparison.Ordinal;
    private const IdentifierKind Sms = IdentifierKind.Phone;
    private const string Language = "en";

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
    public RegistrationFlowTests()
    {
        _deployment.Configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);

        foreach (SendKind kind in new[] { SendKind.Email, SendKind.Sms })
        {
            _deployment.Templates.Set(
                MessageKind.VerificationCode,
                kind,
                Language,
                new MessageTemplate(kind is SendKind.Email ? "code" : null, "{code} {token}"));
        }
    }

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

        Answer with = await browser.SendAsync("POST", "/register", ("clientId", Client));

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
        Browser started = await BegunAsync();
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
        _ = await BegunAsync();

        var elsewhere = new Browser(_deployment);

        _ = await elsewhere.SendAsync("GET", "/register/nothing");

        foreach (string step in Steps)
        {
            Answer refused = await elsewhere.SendAsync("POST", step, ("value", Address));

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
        Browser browser = await SecuredAsync();

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
    /// BFF-CSRF-005b AC2: no route of the flow names a token, and the stream answers
    /// to the cookie alone: one put in the query opens nothing, and the browser that
    /// carries the cookie is read without presenting anything else.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_CSRF_005b_AC2_NoTokenTravelsInAUrlAsync()
    {
        Browser browser = await AwaitingAsync();

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
        Browser browser = await AwaitingAsync();

        using var abort = new CancellationTokenSource();

        (Task running, ResponseBody written) = browser.Open("/register/events", abort.Token);

        await SettledAsync(written);
        await VerifiedAsync(browser);

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
        Browser browser = await AwaitingAsync();

        string token = Token();
        string identifier = Staged(await browser.SendAsync("GET", "/register"));

        Answer opened = await browser.SendAsync(
            "POST",
            "/register/verify/" + identifier,
            ("linkToken", token),
            ("press", false));

        Assert.Equal(StatusCodes.Status200OK, opened.Status);
        Assert.False(Verified(await browser.SendAsync("GET", "/register")));

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
        Assert.False(Verified(await browser.SendAsync("GET", "/register")));

        Answer press = await browser.SendAsync(
            "POST",
            "/register/verify/" + identifier,
            ("linkToken", token),
            ("press", true));

        Assert.Equal(StatusCodes.Status204NoContent, press.Status);
        Assert.True(Verified(await browser.SendAsync("GET", "/register")));
    }

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

    // The link token the message carried, which never touches the state document.
    private string Token() => _deployment.Mail.Taken[^1].Body.Split(' ')[1];

    private string Code(IdentifierKind kind = IdentifierKind.Email) =>
        (kind is IdentifierKind.Email
            ? _deployment.Mail.Taken[^1].Body
            : _deployment.Sms.Taken[^1].Text).Split(' ')[0];

    // The identifier of a kind that is still waiting for its code.
    private static string Waiting(Answer state, IdentifierKind kind)
    {
        foreach (JsonElement staged in state.Json().GetProperty("identifiers").EnumerateArray())
        {
            if (string.Equals(staged.GetProperty("kind").GetString(), Named(kind), Comparison)
                && !staged.GetProperty("verified").GetBoolean())
            {
                return staged.GetProperty("id").GetString()!;
            }
        }

        throw new InvalidOperationException("Nothing of that kind is waiting.");
    }

    private static string Named(IdentifierKind kind) => kind is IdentifierKind.Email ? "email" : "phone";

    private static string Carried(Browser browser) => browser.Cookies["__Host-janus-preauth"];

    private static string Staged(Answer state) =>
        state.Json().GetProperty("identifiers")[0].GetProperty("id").GetString()!;


    private static bool Verified(Answer state) =>
        state.Json().GetProperty("identifiers")[0].GetProperty("verified").GetBoolean();

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

    // A browser that has begun a registration, which is a first contact plus the
    // session the endpoint carried onto it.
    private async Task<Browser> BegunAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/register");
        _ = await browser.SendAsync("POST", "/register", ("clientId", Client));

        return browser;
    }

    // A browser that has answered the age step and staged an address.
    private async Task<Browser> AwaitingAsync()
    {
        Browser browser = await BegunAsync();

        _ = await browser.SendAsync("PUT", "/register/age", ("dateOfBirth", "1990-01-01"));
        _ = await browser.SendAsync("PUT", "/register/email", ("value", Address));

        return browser;
    }

    // A browser that has reached the terms step: both identifiers verified, the
    // confirm step passed and a password that stands alone set.
    private async Task<Browser> SecuredAsync()
    {
        Browser browser = await AwaitingAsync();

        await VerifiedAsync(browser);

        _ = await browser.SendAsync("PUT", "/register/phone", ("value", Number));

        await VerifiedAsync(browser, Sms);

        Assert.Equal(
            StatusCodes.Status200OK,
            (await browser.SendAsync("POST", "/register/confirm")).Status);

        Assert.Equal(
            StatusCodes.Status200OK,
            (await browser.SendAsync("PUT", "/register/security", ("password", Password))).Status);

        return browser;
    }

    private async Task VerifiedAsync(Browser browser, IdentifierKind kind = IdentifierKind.Email)
    {
        Answer state = await browser.SendAsync("GET", "/register");
        string identifier = Waiting(state, kind);

        Answer verified = await browser.SendAsync(
            "POST",
            "/register/verify/" + identifier,
            ("code", Code(kind)));

        Assert.Equal(StatusCodes.Status204NoContent, verified.Status);
    }
}
