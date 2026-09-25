using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// What a throttle looks like at the boundary: the interval it carries, and the same
/// answer whether or not an account stands behind the identifier (BFF-ABUSE-001,
/// BFF-ABUSE-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class ThrottlingTests : IAsyncDisposable
{
    private const string Language = "en";
    private const string Wrong = "notthepasswordatall";
    private const string Unheld = "nobody@example.test";
    private const string Trace = "throttled";

    private static readonly IPAddress Attacker = IPAddress.Parse("203.0.113.5");
    private static readonly IPAddress Elsewhere = IPAddress.Parse("203.0.113.6");

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment that can send every message the sign-in, recovery and
    /// non-existence flows write, whose policy admits the link and the email code.
    /// </summary>
    public ThrottlingTests()
    {
        Flow.Prepare(_deployment);

        _deployment.Configuration.Set(
            Settings.PolicyDefault,
            Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>(
                    [.. Policies.SystemDefault.LoginFactors, Factor.EmailLink, Factor.EmailCode]),
            });

        foreach (MessageKind message in new[]
        {
            MessageKind.SignInLink,
            MessageKind.NoAccount,
            MessageKind.RecoveryLink,
            MessageKind.SecurityNotice,
        })
        {
            foreach (SendKind kind in new[] { SendKind.Email, SendKind.Sms })
            {
                _deployment.Templates.Set(
                    message,
                    kind,
                    Language,
                    new MessageTemplate(kind is SendKind.Email ? "message" : null, "{code} {token}"));
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// BFF-ABUSE-001 AC2: a factor presented from an address that has earned a delay
    /// is answered 429 auth.throttled, with the instant the delay lifts in the body
    /// and the seconds to it in the header, the two agreeing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ABUSE_001_AC2_AThrottledSignInCarriesItsIntervalAsync()
    {
        await RegisteredAsync();

        (Browser browser, string challenge) = await FailedAsync(Flow.Address, Attacker);

        Answer throttled = await browser.SendAsync(
            Attacker,
            Trace,
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "password"),
            ("value", Flow.Password));

        AssertThrottled(throttled);
    }

    /// <summary>
    /// BFF-ABUSE-001 AC2: a sign-in link asked for from an address that has earned a
    /// delay is answered with the interval.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ABUSE_001_AC2_AThrottledSignInLinkCarriesItsIntervalAsync()
    {
        await RegisteredAsync();

        (Browser browser, _) = await FailedAsync(Unheld, Attacker);

        AssertThrottled(
            await browser.SendAsync(Attacker, Trace, "POST", "/auth/link", ("identifier", Unheld)));
    }

    /// <summary>
    /// BFF-ABUSE-001 AC2: an email code asked for from an address that has earned a
    /// delay is answered with the interval.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ABUSE_001_AC2_AThrottledEmailCodeCarriesItsIntervalAsync()
    {
        await RegisteredAsync();

        (Browser browser, _) = await FailedAsync(Unheld, Attacker);

        AssertThrottled(
            await browser.SendAsync(Attacker, Trace, "POST", "/auth/email-otp", ("identifier", Unheld)));
    }

    /// <summary>
    /// BFF-ABUSE-001 AC2: a recovery begun from an address that has earned a delay is
    /// answered with the interval.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ABUSE_001_AC2_AThrottledRecoveryCarriesItsIntervalAsync()
    {
        await RegisteredAsync();

        (Browser browser, _) = await FailedAsync(Unheld, Attacker);

        AssertThrottled(
            await browser.SendAsync(Attacker, Trace, "POST", "/recovery/begin", ("identifier", Unheld)));
    }

    /// <summary>
    /// BFF-ABUSE-001 AC1: the delay that failures against an address an account holds
    /// earned, and the one the same failures against an address nobody holds earned,
    /// are answered byte for byte alike, body and header both.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ABUSE_001_AC1_TheThrottledAnswerIsTheSameForAHeldAndAnUnheldAddressAsync()
    {
        await RegisteredAsync();

        (Browser held, _) = await FailedAsync(Flow.Address, Attacker);
        (Browser unheld, _) = await FailedAsync(Unheld, Elsewhere);

        Answer forHeld = await held.SendAsync(
            Attacker,
            Trace,
            "POST",
            "/auth/begin",
            ("identifier", Flow.Address));
        Answer forUnheld = await unheld.SendAsync(
            Elsewhere,
            Trace,
            "POST",
            "/auth/begin",
            ("identifier", Unheld));

        AssertThrottled(forHeld);
        Assert.Equal(forHeld.Status, forUnheld.Status);
        Assert.Equal(forHeld.Body, forUnheld.Body);
        Assert.Equal(forHeld.Header("Retry-After"), forUnheld.Header("Retry-After"));
    }

    /// <summary>
    /// BFF-ABUSE-002 AC1: a sign-in link asked for an address an account holds and
    /// one asked for an address nobody holds are answered alike: status, body and
    /// every header.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ABUSE_002_AC1_TheLinkRequestIsAnsweredAlikeForAHeldAndAnUnheldAddressAsync()
    {
        await RegisteredAsync();

        (Answer held, Answer unheld, int sent) = await AskedAsync("/auth/link");

        Assert.Equal(StatusCodes.Status202Accepted, held.Status);
        AssertAlike(held, unheld);
        Assert.Equal(2, sent);
    }

    /// <summary>
    /// BFF-ABUSE-002 AC1: an email code asked for an address an account holds and one
    /// asked for an address nobody holds are answered alike: status, body and every
    /// header.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ABUSE_002_AC1_TheEmailCodeRequestIsAnsweredAlikeForAHeldAndAnUnheldAddressAsync()
    {
        await RegisteredAsync();

        (Answer held, Answer unheld, int sent) = await AskedAsync("/auth/email-otp");

        Assert.Equal(StatusCodes.Status202Accepted, held.Status);
        AssertAlike(held, unheld);
        Assert.Equal(2, sent);
    }

    private static void AssertAlike(Answer held, Answer unheld)
    {
        Assert.Equal(held.Status, unheld.Status);
        Assert.Equal(held.Body, unheld.Body);
        Assert.Equal(held.SetCookie, unheld.SetCookie);
        Assert.Equal(
            held.Headers.OrderBy(header => header.Key, StringComparer.OrdinalIgnoreCase),
            unheld.Headers.OrderBy(header => header.Key, StringComparer.OrdinalIgnoreCase));
    }

    private static async Task<string> BegunAsync(Browser browser, string identifier, IPAddress source)
    {
        Answer began = await browser.SendAsync(source, Trace, "POST", "/auth/begin", ("identifier", identifier));

        Assert.Equal(StatusCodes.Status200OK, began.Status);

        return began.Text("challengeId");
    }

    // Each ask comes from a browser that has already made first contact, from an
    // address of its own, under the one trace, so nothing the browser or the address
    // carries tells the two answers apart but the address asked about. What comes
    // back beside the answers is how many messages the two asks sent between them.
    private async Task<(Answer Held, Answer Unheld, int Sent)> AskedAsync(string path)
    {
        var forHeld = new Browser(_deployment);
        var forUnheld = new Browser(_deployment);

        _ = await forHeld.SendAsync("GET", "/auth/session", source: Attacker);
        _ = await forUnheld.SendAsync("GET", "/auth/session", source: Elsewhere);

        int before = _deployment.Mail.Taken.Count;

        Answer held = await forHeld.SendAsync(Attacker, Trace, "POST", path, ("identifier", Flow.Address));
        Answer unheld = await forUnheld.SendAsync(Elsewhere, Trace, "POST", path, ("identifier", Unheld));

        return (held, unheld, _deployment.Mail.Taken.Count - before);
    }

    // Three refused passwords from one address against a sign-in for the identifier
    // given, which earns that address, and the account where there is one, the first
    // delay (AUTH-ABUSE-001); the challenge they were presented against comes back
    // with the browser.
    private async Task<(Browser Browser, string Challenge)> FailedAsync(string identifier, IPAddress source)
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/auth/session", source: source);

        string challenge = await BegunAsync(browser, identifier, source);

        for (int failure = 0; failure < 3; failure++)
        {
            Answer refused = await browser.SendAsync(
                source,
                Trace,
                "POST",
                "/auth/factor",
                ("challengeId", challenge),
                ("factor", "password"),
                ("value", Wrong));

            Assert.Equal(ErrorCodes.FactorRejected.ToString(), refused.Text("code"));
        }

        return (browser, challenge);
    }

    // API-CONV-003: the body names the instant the first delay lifts and the header
    // the whole seconds to it, on the deployment's clock.
    private void AssertThrottled(Answer answer)
    {
        Assert.Equal(StatusCodes.Status429TooManyRequests, answer.Status);
        Assert.Equal(ErrorCodes.Throttled.ToString(), answer.Text("code"));
        Assert.Equal(
            _deployment.Clock.GetUtcNow() + TimeSpan.FromSeconds(1),
            answer.Json().GetProperty("details").GetProperty("retryAt").GetDateTimeOffset());
        Assert.Equal("1", answer.Header("Retry-After"));
    }

    private async Task RegisteredAsync()
    {
        _ = await Flow.SignedInAsync(_deployment);

        _deployment.Clock.Advance(TimeSpan.FromMinutes(5));
    }
}
