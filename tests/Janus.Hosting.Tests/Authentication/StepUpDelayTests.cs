using System;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Authentication;

/// <summary>
/// A factor refused at a step-up is a failed authentication, and it answers to the
/// delay a failed sign-in answers to, per source and per account, through the one
/// throttle (AUTH-ABUSE-001, BFF-ABUSE-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class StepUpDelayTests : IAsyncDisposable
{
    private const string Wrong = "notthepasswordatall";
    private const string Trace = "step-up";

    private static readonly IPAddress Holder = IPAddress.Parse("192.0.2.10");

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment that can send.
    /// </summary>
    public StepUpDelayTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// AUTH-ABUSE-001 AC1: refused step-up factors from one session earn a delay that
    /// escalates with each failure, and each attempt inside it is answered
    /// auth.throttled with the instant it lifts and the seconds to wait.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_001_AC1_RefusedStepUpFactorsAreDelayedProgressivelyAsync()
    {
        (Browser owner, string challenge) = await OpenedAsync();

        for (int failure = 0; failure < 3; failure++)
        {
            AssertRefused(await SteppedAsync(owner, Holder, challenge, Wrong));
        }

        AssertThrottled(await SteppedAsync(owner, Holder, challenge, Wrong), TimeSpan.FromSeconds(1));

        _deployment.Clock.Advance(TimeSpan.FromSeconds(1));

        AssertRefused(await SteppedAsync(owner, Holder, challenge, Wrong));
        AssertThrottled(await SteppedAsync(owner, Holder, challenge, Wrong), TimeSpan.FromSeconds(2));

        _deployment.Clock.Advance(TimeSpan.FromSeconds(2));

        AssertRefused(await SteppedAsync(owner, Holder, challenge, Wrong));
        AssertThrottled(await SteppedAsync(owner, Holder, challenge, Wrong), TimeSpan.FromSeconds(4));

        Assert.Equal(5, _deployment.SessionAudit.StepUpsFailed.Count);
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC1: inside the delay the right factor is refused as a wrong one
    /// is, byte for byte, without being looked at; once the delay lifts the same
    /// challenge completes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_001_AC1_TheRightFactorInsideTheDelayIsRefusedUncheckedAsync()
    {
        (Browser owner, string challenge) = await OpenedAsync();

        for (int failure = 0; failure < 3; failure++)
        {
            _ = await SteppedAsync(owner, Holder, challenge, Wrong);
        }

        int recorded = _deployment.SessionAudit.Records.Count;

        Answer right = await SteppedAsync(owner, Holder, challenge, Flow.Password);
        Answer wrong = await SteppedAsync(owner, Holder, challenge, Wrong);

        AssertThrottled(right, TimeSpan.FromSeconds(1));
        Assert.Equal(wrong.Body, right.Body);
        Assert.Equal(wrong.Header("Retry-After"), right.Header("Retry-After"));
        Assert.Equal(recorded, _deployment.SessionAudit.Records.Count);
        Assert.Equal(3, _deployment.SessionAudit.StepUpsFailed.Count);

        _deployment.Clock.Advance(TimeSpan.FromSeconds(1));

        Answer stepped = await SteppedAsync(owner, Holder, challenge, Flow.Password);

        Assert.Equal(StatusCodes.Status200OK, stepped.Status);
        Assert.Equal("complete", stepped.Text("status"));
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC2: the failures a step-up earned decay, so a failure after a
    /// quiet spell earns the delay a third failure earns and not the one a fourth does.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_001_AC2_TheStepUpDelayDecaysAsync()
    {
        (Browser owner, string challenge) = await OpenedAsync();

        for (int failure = 0; failure < 3; failure++)
        {
            _ = await SteppedAsync(owner, Holder, challenge, Wrong);
        }

        // One half-life: three failures stand as two, and the challenge the first
        // three were presented against has lapsed.
        _deployment.Clock.Advance(TimeSpan.FromMinutes(10));

        string reopened = await BegunAsync(owner);

        AssertRefused(await SteppedAsync(owner, Holder, reopened, Wrong));
        AssertThrottled(await SteppedAsync(owner, Holder, reopened, Wrong), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC3 and AC4: step-up failures spread over many addresses are
    /// caught by the account's own count, and however many there are the delay they
    /// earn the account stops at its cap.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_001_AC4_TheAccountDelayAStepUpEarnsStopsAtItsCapAsync()
    {
        (Browser owner, string challenge) = await OpenedAsync();

        for (int failure = 0; failure < 12; failure++)
        {
            _deployment.Clock.Advance(TimeSpan.FromSeconds(30));

            AssertRefused(await SteppedAsync(owner, Source(failure), challenge, Wrong));
        }

        AssertThrottled(await SteppedAsync(owner, Source(12), challenge, Wrong), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC3: a sign-in failure and a step-up failure against one account
    /// are counted once, together, so neither flow is a second budget of guesses
    /// against the account's factors.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_001_AC3_SignInAndStepUpFailuresShareTheAccountsCountAsync()
    {
        (Browser owner, string challenge) = await OpenedAsync();
        var stranger = new Browser(_deployment);

        _ = await stranger.SendAsync("GET", "/auth/session");

        string signIn = await BegunAsync(stranger);

        for (int failure = 0; failure < 2; failure++)
        {
            Answer refused = await stranger.SendAsync(
                Source(failure),
                Trace,
                "POST",
                "/auth/factor",
                ("challengeId", signIn),
                ("factor", "password"),
                ("value", Wrong));

            AssertRefused(refused);
        }

        AssertRefused(await SteppedAsync(owner, Holder, challenge, Wrong));
        AssertThrottled(await SteppedAsync(owner, Source(2), challenge, Flow.Password), TimeSpan.FromSeconds(1));
    }

    private static IPAddress Source(int index) =>
        IPAddress.Parse("198.51.100." + (index + 1).ToString(CultureInfo.InvariantCulture));

    private static async Task<string> BegunAsync(Browser browser)
    {
        Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));

        Assert.Equal(StatusCodes.Status200OK, began.Status);

        return began.Text("challengeId");
    }

    private static Task<Answer> SteppedAsync(Browser browser, IPAddress source, string challenge, string value) =>
        browser.SendAsync(
            source,
            Trace,
            "POST",
            "/auth/step-up",
            ("challengeId", challenge),
            ("factor", "password"),
            ("value", value));

    private static void AssertRefused(Answer answer)
    {
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, answer.Status);
        Assert.Equal(ErrorCodes.FactorRejected.ToString(), answer.Text("code"));
    }

    // BFF-ABUSE-001: the answer names the instant the delay lifts, and the header
    // gives the whole seconds to it, measured on the deployment's clock.
    private void AssertThrottled(Answer answer, TimeSpan wait)
    {
        Assert.Equal(StatusCodes.Status429TooManyRequests, answer.Status);
        Assert.Equal(ErrorCodes.Throttled.ToString(), answer.Text("code"));
        Assert.Equal(
            _deployment.Clock.GetUtcNow() + wait,
            answer.Json().GetProperty("details").GetProperty("retryAt").GetDateTimeOffset());
        Assert.Equal(
            ((long)wait.TotalSeconds).ToString(CultureInfo.InvariantCulture),
            answer.Header("Retry-After"));
    }

    // A signed-in browser, and a challenge it opened for its own account before any
    // failure could hold the opening.
    private async Task<(Browser Owner, string Challenge)> OpenedAsync()
    {
        Browser owner = await Flow.SignedInAsync(_deployment);

        _deployment.Clock.Advance(TimeSpan.FromMinutes(5));

        return (owner, await BegunAsync(owner));
    }
}
