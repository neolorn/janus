using System;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Recovery;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using OtpNet;
using Xunit;

namespace Janus.Hosting.Tests.Credentials;

/// <summary>
/// The credential endpoints as a browser reaches them: setting a password, enrolling
/// a generator, taking a set of codes, and what the enrolment session of chapter 09
/// section 3 may call (LIB-API-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class CredentialFlowTests : IAsyncDisposable
{
    private const string Language = "en";
    private const string Label = "This phone";
    private const string Replacement = "quincejellyonasaucer";
    private const string Link = "the-approver-sent-this-one";
    private const string Replaced = "elsewhere@example.test";

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment that can send the notice every enrolment carries and knows what to
    /// call itself in an authenticator app.
    /// </summary>
    public CredentialFlowTests()
    {
        Flow.Prepare(_deployment);

        _deployment.Configuration.Set(Settings.ServiceName, "Example");

        foreach (MessageKind message in new[]
            { MessageKind.SecurityNotice, MessageKind.CredentialEnrolled })
        {
            foreach (SendKind kind in new[] { SendKind.Email, SendKind.Sms })
            {
                _deployment.Templates.Set(
                    message,
                    kind,
                    Language,
                    new MessageTemplate(kind is SendKind.Email ? "notice" : null, "body"));
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// LIB-API-005 and AUTH-PASS-004: the password is changed over the endpoint, and
    /// the new one signs the account in.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_005_ThePasswordIsChangedOverTheEndpointAsync()
    {
        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        Browser browser = await SignedInAsync();

        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await browser.SendAsync("POST", "/account/password", ("password", Replacement))).Status);

        Browser signing = await ArrivedAsync();

        Answer begun = await signing.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));

        Answer reached = await signing.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", begun.Text("challengeId")),
            ("factor", "password"),
            ("value", Replacement));

        Assert.Equal(StatusCodes.Status200OK, reached.Status);
        Assert.Equal("complete", reached.Text("status"));
    }

    /// <summary>
    /// LIB-API-005, AUTH-FACT-008 and AUTH-FACT-009: the set comes back over the
    /// endpoint, once, with the instant it was made.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_API_005_TheRecoveryCodeSetComesBackOverTheEndpointAsync()
    {
        Browser browser = await SignedInAsync();

        Answer generated = await browser.SendAsync("POST", "/account/recoverycodes");

        Assert.Equal(StatusCodes.Status200OK, generated.Status);
        Assert.Equal(
            Settings.FactorRecoveryCodesCount.Default,
            generated.Json().GetProperty("codes").GetArrayLength());
        Assert.Equal(
            _deployment.Clock.GetUtcNow(),
            generated.Json().GetProperty("generatedAt").GetDateTimeOffset());
    }

    /// <summary>
    /// AUTH-FACT-007 and AUTH-RECOV-006 AC1: the enrolment answers with the address an
    /// authenticator app is pointed at, and the confirmation brings the codes a second
    /// step beside a password always brings.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_007_TheGeneratorIsEnrolledAndConfirmedOverTheEndpointsAsync()
    {
        Browser browser = await SignedInAsync();

        Answer begun = await browser.SendAsync(
            "POST",
            "/account/factors/totp/begin",
            ("label", Label));

        Assert.Equal(StatusCodes.Status200OK, begun.Status);
        Assert.StartsWith("otpauth://totp/", begun.Text("uri"), StringComparison.Ordinal);

        Answer confirmed = await browser.SendAsync(
            "POST",
            "/account/factors/totp/confirm",
            ("credentialId", begun.Text("id")),
            ("code", Code(begun.Text("secret"))));

        Assert.Equal(StatusCodes.Status200OK, confirmed.Status);
        Assert.Equal(
            Settings.FactorRecoveryCodesCount.Default,
            confirmed.Json().GetProperty("recoveryCodes").GetArrayLength());
    }

    /// <summary>
    /// AUTH-STEP-002 and AUTH-STEP-007: once the account reaches AAL2 the session that
    /// presented a password alone is sent to step up rather than served.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_002_AnAccountReachingAal2IsSentToStepUpAsync()
    {
        Browser browser = await SignedInAsync();

        Answer begun = await browser.SendAsync(
            "POST",
            "/account/factors/totp/begin",
            ("label", Label));

        Answer confirmed = await browser.SendAsync(
            "POST",
            "/account/factors/totp/confirm",
            ("credentialId", begun.Text("id")),
            ("code", Code(begun.Text("secret"))));

        Assert.Equal(StatusCodes.Status200OK, confirmed.Status);

        Answer refused = await browser.SendAsync(
            "DELETE",
            "/account/credentials/" + confirmed.Text("id"));

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), refused.Text("code"));
    }

    /// <summary>
    /// AUTH-RECOV-002 and D-148: the enrolment session reaches the password endpoint
    /// from a browser holding no session at all, and ends when the enrolment completes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_TheEnrolmentSessionReachesThePasswordEndpointAsync()
    {
        _ = await SignedInAsync();

        await LinkedAsync(_deployment.Directory.Created[^1].Subject);

        Browser browser = await ArrivedAsync();

        Assert.Equal(
            StatusCodes.Status200OK,
            (await browser.SendAsync("POST", "/enrol/begin", ("token", Link))).Status);

        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await browser.SendAsync("POST", "/account/password", ("password", Replacement))).Status);

        // The browser still carries the session on its first contact, so what answers
        // is the ended session and not an absent one.
        Answer spent = await browser.SendAsync("POST", "/account/recoverycodes");

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, spent.Status);
        Assert.Equal(ErrorCodes.EnrolmentTokenInvalid.ToString(), spent.Text("code"));
    }

    /// <summary>
    /// D-148: a browser that never opened an enrolment session reaches none of these,
    /// so the endpoints answer nobody rather than guessing whose account it is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_ABrowserHoldingNothingReachesNoCredentialAsync()
    {
        Browser browser = await ArrivedAsync();

        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            (await browser.SendAsync("POST", "/account/password", ("password", Replacement))).Status);
    }

    /// <summary>
    /// REG-IDENT-007 AC3 and AUTH-RECOV-002: the enrolment session an approver opened
    /// for a lost mailbox replaces the address over the endpoint, the new address
    /// alone confirms it, and the displaced one is never asked.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_007_AC3_TheEnrolmentSessionReplacesTheLostAddressAsync()
    {
        _deployment.Configuration.Set(Settings.IdentifiersEmailMax, 1);

        _ = await SignedInAsync();

        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        await LinkedAsync(subject, mailboxLost: true);

        Browser browser = await ArrivedAsync();

        _ = await browser.SendAsync("POST", "/enrol/begin", ("token", Link));

        IdentifierId held = await EmailAsync(subject);

        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await browser.SendAsync(
                "PUT",
                "/account/identifiers/" + held.Value + "/replace",
                ("value", Replaced))).Status);

        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await browser.SendAsync(
                "POST",
                "/account/identifiers/" + held.Value + "/verify",
                ("code", Flow.Code(_deployment, IdentifierKind.Email)))).Status);

        HeldIdentifiers standing = await _deployment.Identifiers
            .HeldAsync(subject, TestContext.Current.CancellationToken);

        Assert.Contains(
            standing.All,
            identifier => string.Equals(identifier.Canonical, Replaced, StringComparison.Ordinal));
        Assert.DoesNotContain(_deployment.Mail.Taken, sent => sent.Subject is "confirm");
    }

    // The account the tests act on, with the clock past the minute the registration's
    // own messages hold the address for (AUTH-ABUSE-004).
    private async Task<Browser> SignedInAsync()
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

    // The admin-assisted link an approver would have sent, which is the only token
    // /enrol/begin consumes (D-147).
    private async Task LinkedAsync(SubjectId subject, bool mailboxLost = false) =>
        await _deployment.Links.ReplaceAsync(
            RecoveryLink.Issue(
                OpaqueToken.Of(Link),
                subject,
                RecoveryPurpose.Enrolment,
                _deployment.Clock.GetUtcNow(),
                TimeSpan.FromHours(1),
                approver: _deployment.Directory.Created[0].Subject,
                mailboxLost),
            TestContext.Current.CancellationToken);

    // The one address the account holds, which is what a replace names.
    private async Task<IdentifierId> EmailAsync(SubjectId subject)
    {
        HeldIdentifiers held = await _deployment.Identifiers
            .HeldAsync(subject, TestContext.Current.CancellationToken);

        foreach (HeldIdentifier identifier in held.All)
        {
            if (identifier.Kind is IdentifierKind.Email)
            {
                return identifier.Id;
            }
        }

        throw new InvalidOperationException("The account holds no address.");
    }

    private string Code(string secret) =>
        new Totp(
                Base32Encoding.ToBytes(secret),
                TotpCodes.StepSeconds,
                OtpHashMode.Sha1,
                TotpCodes.Digits)
            .ComputeTotp(_deployment.Clock.GetUtcNow().UtcDateTime);
}
