using System;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Recovery;

/// <summary>
/// Getting back in as a browser does it: what the recovery link reaches, what it does
/// not, and what a reported loss answers with (chapter 09 section 5).
/// </summary>
[Trait("kind", "unit")]
public sealed class RecoveryFlowTests : IAsyncDisposable
{
    private const string Language = "en";
    private const string Elsewhere = "nobody@example.test";
    private const string NewPassword = "lemoncurdandbutter";

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment that can send, whose recovery message carries the token as the
    /// shipped template does.
    /// </summary>
    public RecoveryFlowTests()
    {
        Flow.Prepare(_deployment);

        foreach (MessageKind message in new[] { MessageKind.RecoveryLink, MessageKind.SecurityNotice })
        {
            foreach (SendKind kind in new[] { SendKind.Email, SendKind.Sms })
            {
                _deployment.Templates.Set(
                    message,
                    kind,
                    Language,
                    new MessageTemplate(kind is SendKind.Email ? "recovery" : null, "{token}"));
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// AUTH-RECOV-005 AC2: the link the address received sets the password, and the
    /// new password signs the account in.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_005_AC2_TheLinkSetsAPasswordThatSignsInAsync()
    {
        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        _ = await RegisteredAsync();

        Browser browser = await ArrivedAsync();

        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await browser.SendAsync("POST", "/recovery/begin", ("identifier", Flow.Address))).Status);

        Answer completed = await browser.SendAsync(
            "POST",
            "/recovery/complete",
            ("token", Token()),
            ("password", NewPassword));

        Assert.Equal(StatusCodes.Status204NoContent, completed.Status);

        Browser signing = await ArrivedAsync();

        Answer begun = await signing.SendAsync(
            "POST",
            "/auth/begin",
            ("identifier", Flow.Address));

        Answer reached = await signing.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", begun.Text("challengeId")),
            ("factor", "password"),
            ("value", NewPassword));

        Assert.Equal(StatusCodes.Status200OK, reached.Status);
        Assert.Equal("complete", reached.Text("status"));
    }

    /// <summary>
    /// AUTH-RECOV-002 and D-147: the two links are separate tokens with one endpoint
    /// each, so the self-service link opens no enrolment session.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_TheSelfServiceLinkOpensNoEnrolmentSessionAsync()
    {
        _ = await RegisteredAsync();

        Browser browser = await ArrivedAsync();

        _ = await browser.SendAsync("POST", "/recovery/begin", ("identifier", Flow.Address));

        string token = Token();

        Answer refused = await browser.SendAsync("POST", "/enrol/begin", ("token", token));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal(ErrorCodes.EnrolmentTokenInvalid.ToString(), refused.Text("code"));

        // The token is still the one the message carried, so the endpoint that does
        // consume it is unaffected by the refusal.
        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await browser.SendAsync(
                "POST",
                "/recovery/complete",
                ("token", token),
                ("password", NewPassword))).Status);
    }

    /// <summary>
    /// AUTH-ABUSE-003: an identifier no account holds is accepted exactly as one that
    /// is held, so the answer says nothing about which it was.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_003_AnIdentifierNoAccountHoldsIsAcceptedTheSameWayAsync()
    {
        _ = await RegisteredAsync();

        Browser browser = await ArrivedAsync();

        Answer held = await browser.SendAsync(
            "POST",
            "/recovery/begin",
            ("identifier", Flow.Address));

        Answer unheld = await browser.SendAsync(
            "POST",
            "/recovery/begin",
            ("identifier", Elsewhere));

        Assert.Equal(held.Status, unheld.Status);
        Assert.Equal(StatusCodes.Status202Accepted, unheld.Status);
        Assert.Equal(held.Body, unheld.Body);
    }

    /// <summary>
    /// AUTH-RECOV-007: the report is accepted, the answer carries the end of the
    /// window, and the link the notice carried cancels it from a browser holding
    /// nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC1_AReportedLossAnswersWithTheWindowAsync()
    {
        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        Browser browser = await RegisteredAsync();
        AuthenticatorId credential = Enrolled();

        Answer reported = await browser.SendAsync(
            "POST",
            "/recovery/report-loss",
            ("credentialId", credential.ToString()));

        Assert.Equal(StatusCodes.Status202Accepted, reported.Status);
        Assert.Equal(
            _deployment.Clock.GetUtcNow() + TimeSpan.FromDays(7),
            reported.Json().GetProperty("invalidatesAt").GetDateTimeOffset());

        Browser elsewhere = await ArrivedAsync();

        Answer cancelled = await elsewhere.SendAsync(
            "POST",
            "/recovery/report-loss/" + credential + "/cancel",
            ("token", _deployment.Mail.Taken[^1].Body.Trim()));

        Assert.Equal(StatusCodes.Status204NoContent, cancelled.Status);
    }

    /// <summary>
    /// AUTH-RECOV-007: nobody at all reports nothing, and the report a browser
    /// holding no session sends is refused before it reaches the account.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_ASessionlessReportIsRefusedAsync()
    {
        _ = await RegisteredAsync();
        AuthenticatorId credential = Enrolled();

        Browser browser = await ArrivedAsync();

        Answer refused = await browser.SendAsync(
            "POST",
            "/recovery/report-loss",
            ("credentialId", credential.ToString()));

        Assert.Equal(StatusCodes.Status401Unauthorized, refused.Status);
    }

    // The account the tests recover, signed in, with the clock past the minute the
    // registration's own messages hold the address for (AUTH-ABUSE-004).
    private async Task<Browser> RegisteredAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Clock.Advance(TimeSpan.FromMinutes(5));

        return browser;
    }

    // A browser that has been to the deployment once, which is what leaves it holding
    // the first contact every state change presents back (BFF-CSRF-005a).
    private async Task<Browser> ArrivedAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/auth/session");

        return browser;
    }

    // A confirmed generator on the account the flow registered, which is what a loss
    // report is about.
    private AuthenticatorId Enrolled()
    {
        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        var id = AuthenticatorId.New(_deployment.Clock);

        var credential = Authenticator.EnrollingTotp(
            id,
            subject,
            Label(),
            new byte[20],
            _deployment.Clock.GetUtcNow());

        credential.Confirm(_deployment.Clock.GetUtcNow());
        _deployment.Authenticators.Hold(credential);

        return id;
    }

    private static CredentialLabel Label() =>
        CredentialLabel.TryParse("Phone", out CredentialLabel label)
            ? label
            : throw new InvalidOperationException("The label is not one.");

    // The token the recovery message carried, which never touches any answer.
    private string Token() => _deployment.Mail.Taken[^1].Body.Trim();
}
