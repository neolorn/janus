using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests.Sending;
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
    private const string Recorded = "recovered@example.test";

    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private readonly Deployment _deployment = new();
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment that can send, whose recovery message and suspension notice carry
    /// the token as the shipped templates do.
    /// </summary>
    public RecoveryFlowTests()
    {
        Flow.Prepare(_deployment);
        _deployment.RecoveryApprovals.Work = _deployment.Work;
        _deployment.RecoveryAudit.Work = _deployment.Work;

        foreach (MessageKind message in new[] { MessageKind.RecoveryLink, MessageKind.SecurityNotice, MessageKind.CredentialSuspended })
        {
            foreach (SendKind kind in new[] { SendKind.Email, SendKind.Sms })
            {
                _deployment.Templates.Set(
                    message,
                    kind,
                    Language,
                    new MessageTemplate(kind is SendKind.Email ? "recovery" : null, "{link}"));
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _randomness.Dispose();
        await _deployment.DisposeAsync();
    }

    /// <summary>
    /// AUTH-ABUSE-004: a recovery link is asked for by a person, so it is counted under
    /// the purpose a sign-in link is and by no <c>notification</c> restriction, which
    /// counts notices alone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_ARecoveryLinkIsCountedByNoNotificationRestrictionAsync()
    {
        _ = await RegisteredAsync();

        var notified = new RestrictionKey("notification.destination", RestrictionKeyKind.Destination, Flow.Address);
        var mailed = new RestrictionKey("email.destination", RestrictionKeyKind.Destination, Flow.Address);
        int notices = _deployment.SendLedger.Sends(notified).Count;
        int mails = _deployment.SendLedger.Sends(mailed).Count;

        Browser browser = await ArrivedAsync();

        Assert.Equal(
            StatusCodes.Status202Accepted,
            (await browser.SendAsync("POST", "/recovery/begin", ("identifier", Flow.Address))).Status);

        Assert.Equal(notices, _deployment.SendLedger.Sends(notified).Count);
        Assert.Equal(mails + 1, _deployment.SendLedger.Sends(mailed).Count);
    }

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
            ("token", Landing.Token(_deployment.Mail.Taken[^1].Body)));

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

    /// <summary>
    /// CONV-CODE-006 AC2 and AUTH-RECOV-002: an approval whose body carries no reason is
    /// refused with the code chapter 10 gives before the service is reached, so a
    /// caller the service would refuse for want of the permission is answered for the
    /// body, and no link is sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_CODE_006_AC2_AnApprovalMissingItsReasonIsRefusedBeforeTheServiceAsync()
    {
        Browser caller = await RegisteredAsync();
        int sent = _deployment.Mail.Taken.Count;

        Answer approved = await caller.SendAsync(
            "POST",
            "/admin/recovery/approve",
            ("subject", Guid.NewGuid().ToString()),
            ("channelUsed", Flow.Address));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, approved.Status);
        Assert.Equal(ErrorCodes.RecoveryReasonRequired.ToString(), approved.Text("code"));
        Assert.Equal(sent, _deployment.Mail.Taken.Count);
    }

    /// <summary>
    /// CONV-CODE-006 AC3 and API-CONV-002 AC3: a reason or a channel past 1024
    /// characters after trimming, or a blank channel, is malformed before the service
    /// is reached, so a caller without the permission is answered for the body.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_CODE_006_AC3_AnApprovalsFreeTextOutsideTheBoundIsRefusedBeforeTheServiceAsync()
    {
        Browser caller = await RegisteredAsync();
        int sent = _deployment.Mail.Taken.Count;
        string overlong = " " + new string('r', 1025) + " ";

        Answer longer = await caller.SendAsync(
            "POST",
            "/admin/recovery/approve",
            ("subject", Guid.NewGuid().ToString()),
            ("reason", overlong),
            ("channelUsed", Flow.Address));
        Answer unreached = await caller.SendAsync(
            "POST",
            "/admin/recovery/approve",
            ("subject", Guid.NewGuid().ToString()),
            ("reason", "Confirmed by video call."),
            ("channelUsed", "   "));

        Assert.Equal(StatusCodes.Status400BadRequest, longer.Status);
        Assert.Equal("reason", longer.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Equal(StatusCodes.Status400BadRequest, unreached.Status);
        Assert.Equal("channelUsed", unreached.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Equal(sent, _deployment.Mail.Taken.Count);
    }

    /// <summary>
    /// AUTH-RECOV-002 AC7 and chapter 09 section 5: an approval whose link's send a
    /// sending restriction refuses is answered 429 <c>auth.restriction.exceeded</c> with
    /// <c>retryAt</c>, and leaves no approval, no record of it and no send.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_AC7_AnApprovalWhoseLinkARestrictionRefusesIsAnsweredAndLeavesNothingAsync()
    {
        Browser approver = await ApproverAsync();
        SubjectId subject = Recovered();

        _deployment.Configuration.Set<IReadOnlyList<Restriction>>(
            Settings.Restrictions,
            [
                new Restriction(
                    "email.destination",
                    RestrictionKeyKind.Destination,
                    HostKeyName: null,
                    RestrictionPurpose.SignIn,
                    [new Bucket(1, TimeSpan.FromHours(1), BucketWindow.Sliding)])
                {
                    Channel = RestrictionChannel.Email,
                },
            ]);

        Answer approved = await ApprovedAsync(approver, subject);
        int sent = _deployment.Mail.Taken.Count;
        Answer refused = await ApprovedAsync(approver, subject);

        Assert.Equal(StatusCodes.Status200OK, approved.Status);
        Assert.Equal(StatusCodes.Status429TooManyRequests, refused.Status);
        Assert.Equal(ErrorCodes.RestrictionExceeded.ToString(), refused.Text("code"));
        Assert.Equal(
            _deployment.Clock.GetUtcNow() + TimeSpan.FromHours(1),
            refused.Json().GetProperty("details").GetProperty("retryAt").GetDateTimeOffset());
        Assert.Single(_deployment.RecoveryApprovals.All);
        Assert.Single(_deployment.RecoveryAudit.Written);
        Assert.Equal(sent, _deployment.Mail.Taken.Count);
        Assert.False(_deployment.Work.Open);
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC4 and chapter 09 section 5: an approval by an approver restricted
    /// after the gate step is refused 403 <c>authz.restricted</c> at the second ask
    /// inside its unit of work, nothing approved and nothing sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC4_AnApprovalRefusedAtTheSecondAskApprovesAndSendsNothingAsync()
    {
        Browser approver = await ApproverAsync();
        SubjectId subject = Recovered();
        int sent = _deployment.Mail.Taken.Count;

        await RestrictedSinceTheGateStep.RefusesAsync(_deployment, () => ApprovedAsync(approver, subject));

        Assert.Empty(_deployment.RecoveryApprovals.All);
        Assert.Empty(_deployment.RecoveryAudit.Written);
        Assert.Equal(sent, _deployment.Mail.Taken.Count);
    }

    private static Task<Answer> ApprovedAsync(Browser approver, SubjectId subject) =>
        approver.SendAsync(
            "POST",
            "/admin/recovery/approve",
            ("subject", subject.ToString()),
            ("reason", "Confirmed on the recorded address."),
            ("channelUsed", Recorded));

    // A signed-in member of the administrative organization who may approve a recovery.
    private async Task<Browser> ApproverAsync()
    {
        Browser browser = await RegisteredAsync();

        _deployment.Administers(Administration);
        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Administration, Permissions.RecoveryApprove);

        return browser;
    }

    // Another account, holding the address its recovery is confirmed on.
    private SubjectId Recovered()
    {
        var subject = SubjectId.New(_randomness);

        _deployment.Accounts.Stands(subject, AccountState.Active);
        _ = _deployment.Identifiers.Verified(subject, IdentifierKind.Email, Recorded);

        return subject;
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
    private string Token() => Landing.Token(_deployment.Mail.Taken[^1].Body);
}
