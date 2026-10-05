using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Alerting;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Recovery;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
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
    /// OPS-OBS-002 AC2: a blocklist fall back met while a password is changed is still
    /// waiting for the alert channels once the corpus answers again, and is carried
    /// once rather than forgotten with the outage.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_OBS_002_AC2_AFallbackOutlivesTheOutageUntilItIsCarriedAsync()
    {
        _deployment.Configuration.Set(Settings.AlertingEmailDestinations, ["ops@example.test"]);
        _deployment.Configuration.Set(Settings.AlertingSmsDestinations, ["+441632960098"]);
        _deployment.Configuration.Set(Settings.AlertingOwnerEmail, "owner@example.test");
        _deployment.Configuration.Set(Settings.AlertingOwnerSms, "+441632960099");

        Browser browser = await SignedInAsync();

        _deployment.Corpus.Unreachable.Add(BlocklistSource.RangeApi);

        Answer changed = await browser.SendAsync("POST", "/account/password", ("password", Replacement));

        _deployment.Corpus.Unreachable.Clear();

        Assert.Equal(StatusCodes.Status204NoContent, changed.Status);
        Assert.Equal(
            [Alerts.Key(AlertCondition.Degradation, "password.blocklist.fallback", named: null)],
            _deployment.Raised.Waiting.Select(alert => Alerts.Deduplication(alert.Raised.IdempotencyKey)));
        Assert.Equal(1, await _deployment.CarryAlertsAsync());
        Assert.Empty(_deployment.Raised.Waiting);
    }

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
    /// AUTH-FACT-008 AC4: the set a generation returns is read from the account as
    /// viewed at the instant it was returned, and as not exported.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AC4_AGeneratedSetIsReadFromTheAccountAsViewedAsync()
    {
        Browser browser = await SignedInAsync();

        Answer generated = await browser.SendAsync("POST", "/account/recoverycodes");
        _deployment.Clock.Advance(TimeSpan.FromMinutes(1));
        Answer account = await browser.SendAsync("GET", "/account");

        Assert.Equal(StatusCodes.Status200OK, account.Status);
        Assert.Equal(
            generated.Json().GetProperty("generatedAt").GetDateTimeOffset(),
            account.Json().GetProperty("recoveryCodes").GetProperty("viewedAt").GetDateTimeOffset());
        Assert.Equal(
            JsonValueKind.Null,
            account.Json().GetProperty("recoveryCodes").GetProperty("exportedAt").ValueKind);
    }

    /// <summary>
    /// AUTH-FACT-008 AC4, AUTH-RECOV-006 AC2 and LIB-API-005: the frontend's report of a
    /// copy, download or print is answered with nothing, and the account reads when it
    /// was made.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AC4_AReportedExportIsReadFromTheAccountAsync()
    {
        Browser browser = await SignedInAsync();

        Assert.Equal(
            StatusCodes.Status200OK,
            (await browser.SendAsync("POST", "/account/recoverycodes")).Status);

        _deployment.Clock.Advance(TimeSpan.FromMinutes(1));

        Answer reported = await browser.SendAsync("POST", "/account/recoverycodes/exported");

        Assert.Equal(StatusCodes.Status204NoContent, reported.Status);

        Answer account = await browser.SendAsync("GET", "/account");

        Assert.Equal(StatusCodes.Status200OK, account.Status);
        Assert.Equal(
            _deployment.Clock.GetUtcNow(),
            account.Json().GetProperty("recoveryCodes").GetProperty("exportedAt").GetDateTimeOffset());
    }

    /// <summary>
    /// IDN-ACCT-007 AC2 and chapter 09 section 6: recording an export is a change, so a
    /// restricted account's report is refused 403 with the restriction's code, with no
    /// step-up asked, and the account reads the set as not exported.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_007_AC2_ARestrictedAccountsReportOfAnExportIsRefusedAsync()
    {
        Browser browser = await SignedInAsync();
        _ = await browser.SendAsync("POST", "/account/recoverycodes");
        _deployment.Restriction.Restrict(_deployment.Directory.Created[^1].Subject);

        Answer refused = await browser.SendAsync("POST", "/account/recoverycodes/exported");
        Answer account = await browser.SendAsync("GET", "/account");

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.Restricted.ToString(), refused.Text("code"));
        Assert.Equal(
            JsonValueKind.Null,
            account.Json().GetProperty("recoveryCodes").GetProperty("exportedAt").ValueKind);
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC3: a restriction of the account committed after the gate step
    /// and before the first write refuses the report of an export, and the account
    /// reads the set as not exported.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ARestrictionCommittedSinceTheGateStepRefusesTheExportAsync()
    {
        Browser browser = await SignedInAsync();
        _ = await browser.SendAsync("POST", "/account/recoverycodes");

        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync("POST", "/account/recoverycodes/exported"));
        Answer account = await browser.SendAsync("GET", "/account");

        Assert.Equal(
            JsonValueKind.Null,
            account.Json().GetProperty("recoveryCodes").GetProperty("exportedAt").ValueKind);
    }

    /// <summary>
    /// AUTH-FACT-008 and chapter 09 section 4: an account holding no set is answered
    /// with the service's refusal.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AnExportReportedWithNoSetIsAConflictAsync()
    {
        Browser browser = await SignedInAsync();

        Answer refused = await browser.SendAsync("POST", "/account/recoverycodes/exported");

        Assert.Equal(StatusCodes.Status409Conflict, refused.Status);
        Assert.Equal(ErrorCodes.FactorNotEnrolled.ToString(), refused.Text("code"));
    }

    /// <summary>
    /// API-CONV-003: a browser holding no session reports no export, since there is no
    /// account for the report to be about.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AnExportReportedWithNoSessionIsAnsweredNobodyAsync()
    {
        Browser browser = await ArrivedAsync();

        Answer refused = await browser.SendAsync("POST", "/account/recoverycodes/exported");

        Assert.Equal(StatusCodes.Status401Unauthorized, refused.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), refused.Text("code"));
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
    /// BFF-STEP-001 AC1: a gate the account can meet with what it holds is answered
    /// with the three values of the gate, the outcome <c>present</c> and the
    /// combinations that meet it, and no instant a report completes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_STEP_001_AC1_AGateTheAccountCanMeetNamesWhatMeetsItAsync()
    {
        Browser browser = await SignedInAsync();

        Held(AuthenticatorState.Active, invalidatesAt: null);

        Assert.Equal(
            Written("""{"required":{"level":"aal2","phishingResistant":false,"maxAge":900},"outcome":"present","options":[["password","totp"]],"pendingUntil":null}"""),
            await RefusedAsync(browser));
    }

    /// <summary>
    /// BFF-STEP-001 AC1: a gate above anything the account has ever reached is
    /// answered with the outcome <c>enrol</c> and no combination.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_STEP_001_AC1_AGateTheAccountNeverReachedAsksForEnrolmentAsync()
    {
        _deployment.Configuration.Set(
            Settings.PolicyDefault,
            Policies.SystemDefault with
            {
                Gates = new Dictionary<StepUpAction, Gate>(Policies.SystemDefault.Gates)
                {
                    [StepUpAction.FactorRemove] = new Gate(GateLevel.Aal2, false, TimeSpan.FromMinutes(20)),
                },
            });

        Browser browser = await SignedInAsync();

        Assert.Equal(
            Written("""{"required":{"level":"aal2","phishingResistant":false,"maxAge":1200},"outcome":"enrol","options":[],"pendingUntil":null}"""),
            await RefusedAsync(browser));
    }

    /// <summary>
    /// BFF-STEP-001 AC1: a gate the account reached with a factor it can no longer
    /// present, and no report running, is answered with the outcome
    /// <c>report-loss</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_STEP_001_AC1_AGateReachedWithAFactorThatIsGoneAsksForAReportAsync()
    {
        Browser browser = await SignedInAsync();

        Held(AuthenticatorState.Suspended, invalidatesAt: null);

        Assert.Equal(
            Written("""{"required":{"level":"aal2","phishingResistant":false,"maxAge":900},"outcome":"report-loss","options":[],"pendingUntil":null}"""),
            await RefusedAsync(browser));
    }

    /// <summary>
    /// BFF-STEP-001 AC1: a gate met only by a factor a running report is taking away is
    /// answered with the outcome <c>pending</c> and the instant the report completes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_STEP_001_AC1_AGateWaitingOnAReportNamesWhenItCompletesAsync()
    {
        Browser browser = await SignedInAsync();
        DateTimeOffset completes = _deployment.Clock.GetUtcNow().AddDays(7);

        Held(AuthenticatorState.Suspended, completes);

        Assert.Equal(
            Written(
                """{"required":{"level":"aal2","phishingResistant":false,"maxAge":900},"outcome":"pending","options":[],"pendingUntil":"""
                    + JsonSerializer.Serialize(completes)
                    + "}"),
            await RefusedAsync(browser));
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

        // The enrolment ended with the link it was opened from.
        Assert.Empty(_deployment.Links.Held);
    }

    /// <summary>
    /// AUTH-RECOV-002 and chapter 09 section 3: the enrolment session is usable only
    /// against the endpoints the chapter names for it, and the recovery codes are not
    /// one of them, so a browser holding that session and no other is answered there as
    /// one holding no session, while the enrolment stands and once it has ended.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_TheEnrolmentSessionDoesNotReachTheRecoveryCodesAsync()
    {
        _ = await SignedInAsync();

        await LinkedAsync(_deployment.Directory.Created[^1].Subject);

        Browser browser = await ArrivedAsync();

        _ = await browser.SendAsync("POST", "/enrol/begin", ("token", Link));

        Answer standing = await browser.SendAsync("POST", "/account/recoverycodes");

        _ = await browser.SendAsync("POST", "/account/password", ("password", Replacement));

        Answer ended = await browser.SendAsync("POST", "/account/recoverycodes");

        Assert.Equal(StatusCodes.Status401Unauthorized, standing.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), standing.Text("code"));
        Assert.Equal(StatusCodes.Status401Unauthorized, ended.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), ended.Text("code"));
        Assert.DoesNotContain(_deployment.Authenticators.All, held => held.Factor is Factor.RecoveryCodes);
    }

    /// <summary>
    /// BFF-ORDER-001 stages 5 and 8, chapter 09 section 3, D-189: an enrolment session
    /// is resolved only on the routes the chapter lists for it. On any other route that
    /// requires a session, the removal of a credential and the upgrade of a key among
    /// them, the browser that carries it and no session is answered as one holding
    /// none, with no details, and keeps what it carries: the enrolment still stands on
    /// its own routes afterwards.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AnEnrolmentSessionIsNoSessionOnARouteItDoesNotReachAsync()
    {
        _ = await SignedInAsync();
        await LinkedAsync(_deployment.Directory.Created[^1].Subject);
        Browser browser = await ArrivedAsync();
        _ = await browser.SendAsync("POST", "/enrol/begin", ("token", Link));
        string carried = browser.Cookies[BrowserCookies.PreAuthentication];

        Answer[] elsewhere =
        [
            await browser.SendAsync("DELETE", "/account/credentials/" + Guid.NewGuid()),
            await browser.SendAsync("POST", "/account/credentials/" + Guid.NewGuid() + "/upgrade"),
            await browser.SendAsync("PATCH", "/account/credentials/" + Guid.NewGuid(), ("label", Label)),
            await browser.SendAsync("POST", "/account/recoverycodes"),
            await browser.SendAsync("GET", "/account"),
            await browser.SendAsync("GET", "/account/credentials"),
            await browser.SendAsync("POST", "/recovery/report-loss", ("credentialId", Guid.NewGuid().ToString())),
        ];
        Answer reached = await browser.SendAsync("POST", "/account/password", ("password", Replacement));

        Assert.All(
            elsewhere,
            answer =>
            {
                Assert.Equal(StatusCodes.Status401Unauthorized, answer.Status);
                Assert.Equal(ErrorCodes.SessionExpired.ToString(), answer.Text("code"));
                Assert.Empty(answer.Json().GetProperty("details").EnumerateObject());
            });
        Assert.Equal(carried, browser.Cookies[BrowserCookies.PreAuthentication]);
        Assert.Equal(StatusCodes.Status204NoContent, reached.Status);
    }

    /// <summary>
    /// AUTH-RECOV-002 and chapter 09 section 3: an enrolment session that has ended is
    /// answered as a session that has ended, with no details, at the routes it reached
    /// and at those it never did.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_AnEndedEnrolmentSessionIsExpiredWithNoDetailsAsync()
    {
        _ = await SignedInAsync();
        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        await LinkedAsync(subject, mailboxLost: true);
        Browser browser = await ArrivedAsync();
        _ = await browser.SendAsync("POST", "/enrol/begin", ("token", Link));
        string identifier = "/account/identifiers/" + (await EmailAsync(subject)).Value;
        _ = await browser.SendAsync("POST", "/account/password", ("password", Replacement));

        Answer[] ended =
        [
            await browser.SendAsync("POST", "/account/password", ("password", Replacement)),
            await browser.SendAsync("POST", "/account/factors/totp/begin", ("label", Label)),
            await browser.SendAsync("POST", "/account/recoverycodes/exported"),
            await browser.SendAsync("PUT", identifier + "/replace", ("value", Replaced)),
            await browser.SendAsync("POST", identifier + "/verify", ("code", "000000")),
            await browser.SendAsync("DELETE", "/account/credentials/" + Guid.NewGuid()),
            await browser.SendAsync("POST", "/account/credentials/" + Guid.NewGuid() + "/upgrade"),
        ];

        Assert.All(
            ended,
            answer =>
            {
                Assert.Equal(StatusCodes.Status401Unauthorized, answer.Status);
                Assert.Equal(ErrorCodes.SessionExpired.ToString(), answer.Text("code"));
                Assert.Empty(answer.Json().GetProperty("details").EnumerateObject());
            });
    }

    /// <summary>
    /// AUTH-FACT-008 AC4 and chapter 09 section 6: the report of an export is one of
    /// the routes the enrolment session reaches, and the account reads when it was
    /// made.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AC4_TheEnrolmentSessionReportsAnExportAsync()
    {
        Browser holder = await SignedInAsync();
        _ = await holder.SendAsync("POST", "/account/recoverycodes");
        await LinkedAsync(_deployment.Directory.Created[^1].Subject);
        Browser browser = await ArrivedAsync();
        _ = await browser.SendAsync("POST", "/enrol/begin", ("token", Link));

        Answer reported = await browser.SendAsync("POST", "/account/recoverycodes/exported");

        Assert.Equal(StatusCodes.Status204NoContent, reported.Status);
        Assert.Equal(
            _deployment.Clock.GetUtcNow(),
            (await holder.SendAsync("GET", "/account"))
                .Json().GetProperty("recoveryCodes").GetProperty("exportedAt").GetDateTimeOffset());
    }

    /// <summary>
    /// AUTH-RECOV-006 AC5 and chapter 09 sections 3 and 6, D-189: an enrolment session
    /// whose second step showed recovery codes stays open on its routes until the
    /// report of their export, which answers 204, sets the export and ends the
    /// session, so the same browser is then answered as one holding none.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_006_AC5_TheReportOfAnExportCompletesTheEnrolmentSessionAsync()
    {
        Browser holder = await SignedInAsync();
        await LinkedAsync(_deployment.Directory.Created[^1].Subject);
        Browser browser = await ArrivedAsync();
        _ = await browser.SendAsync("POST", "/enrol/begin", ("token", Link));
        Answer begun = await browser.SendAsync("POST", "/account/factors/totp/begin", ("label", Label));
        Answer confirmed = await browser.SendAsync(
            "POST",
            "/account/factors/totp/confirm",
            ("credentialId", begun.Text("id")),
            ("code", Code(begun.Text("secret"))));
        _deployment.Clock.Advance(TimeSpan.FromMinutes(2));

        Answer standing = await browser.SendAsync("POST", "/account/factors/totp/begin", ("label", Label + " 2"));
        Answer reported = await browser.SendAsync("POST", "/account/recoverycodes/exported");
        Answer again = await browser.SendAsync("POST", "/account/recoverycodes/exported");
        Answer ended = await browser.SendAsync("POST", "/account/password", ("password", Replacement));

        Assert.Equal(StatusCodes.Status200OK, confirmed.Status);
        Assert.Equal(
            Settings.FactorRecoveryCodesCount.Default,
            confirmed.Json().GetProperty("recoveryCodes").GetArrayLength());
        Assert.Equal(StatusCodes.Status200OK, standing.Status);
        Assert.Equal(StatusCodes.Status204NoContent, reported.Status);
        Assert.Equal(StatusCodes.Status401Unauthorized, again.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), again.Text("code"));
        Assert.Equal(StatusCodes.Status401Unauthorized, ended.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), ended.Text("code"));
        Assert.Equal(
            _deployment.Clock.GetUtcNow(),
            (await holder.SendAsync("GET", "/account"))
                .Json().GetProperty("recoveryCodes").GetProperty("exportedAt").GetDateTimeOffset());
    }

    /// <summary>
    /// IDN-ACCT-007 AC2 and chapter 09 section 6, D-189: the report that completes an
    /// enrolment session is admitted for a restricted account, where its own session's
    /// report is refused 403.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_007_AC2_ARestrictedAccountsReportCompletingAnEnrolmentSessionIsAdmittedAsync()
    {
        Browser holder = await SignedInAsync();
        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        _deployment.Restriction.Restrict(subject);
        await LinkedAsync(subject);
        Browser browser = await ArrivedAsync();
        _ = await browser.SendAsync("POST", "/enrol/begin", ("token", Link));
        Answer begun = await browser.SendAsync("POST", "/account/factors/totp/begin", ("label", Label));
        Answer confirmed = await browser.SendAsync(
            "POST",
            "/account/factors/totp/confirm",
            ("credentialId", begun.Text("id")),
            ("code", Code(begun.Text("secret"))));

        Answer own = await holder.SendAsync("POST", "/account/recoverycodes/exported");
        Answer reported = await browser.SendAsync("POST", "/account/recoverycodes/exported");

        Assert.Equal(StatusCodes.Status200OK, confirmed.Status);
        Assert.Equal(StatusCodes.Status403Forbidden, own.Status);
        Assert.Equal(ErrorCodes.Restricted.ToString(), own.Text("code"));
        Assert.Equal(StatusCodes.Status204NoContent, reported.Status);
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

    /// <summary>
    /// REG-IDENT-007 AC3 (D-189) and chapter 09: the enrolment session reaches the
    /// pending verification of the replace it staged and no other, so the code of an add
    /// the account staged from a session answers 422 <c>auth.code.invalid</c> there and
    /// a press of its link answers 422 <c>auth.code.expired</c>, and the add stands
    /// unproved.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_007_AC3_TheEnrolmentSessionReachesNoVerificationItDidNotStageAsync()
    {
        Browser holder = await SignedInAsync();
        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        _ = await holder.SendAsync("POST", "/account/identifiers", ("kind", "email"), ("value", Replaced));
        string staged = "/account/identifiers/" + Assert.Single(_deployment.Pending.All).Identifier.Value + "/verify";
        await LinkedAsync(subject, mailboxLost: true);
        Browser browser = await ArrivedAsync();
        _ = await browser.SendAsync("POST", "/enrol/begin", ("token", Link));

        Answer code = await browser.SendAsync(
            "POST",
            staged,
            ("code", Flow.Code(_deployment, IdentifierKind.Email)));
        Answer press = await browser.SendAsync(
            "POST",
            staged,
            ("linkToken", Flow.Token(_deployment, IdentifierKind.Email)),
            ("press", true));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, code.Status);
        Assert.Equal(ErrorCodes.CodeInvalid.ToString(), code.Text("code"));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, press.Status);
        Assert.Equal(ErrorCodes.CodeExpired.ToString(), press.Text("code"));
        Assert.False(Assert.Single(_deployment.Pending.All).Staged.IsVerified);
    }

    // The account the tests act on, with the clock past the minute the registration's
    // own messages hold the address for (AUTH-ABUSE-004).
    /// <summary>
    /// AUTHZ-GATE-006 AC3: a restriction of the account committed after the gate step
    /// and before the first write refuses the setting of a password, the generating of
    /// recovery codes and the beginning and the confirming of a generator, and no
    /// credential is written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ARestrictionCommittedSinceTheGateStepRefusesEachCredentialChangeAsync()
    {
        Browser browser = await SignedInAsync();
        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync("POST", "/account/password", ("password", Replacement)));
        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync("POST", "/account/recoverycodes"));
        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync("POST", "/account/factors/totp/begin", ("label", Label)));

        Assert.Empty(await _deployment.Authenticators.OfAsync(subject, cancellationToken));

        Answer begun = await browser.SendAsync("POST", "/account/factors/totp/begin", ("label", Label));

        await RestrictedSinceTheGateStep.RefusesAsync(
            _deployment,
            () => browser.SendAsync(
                "POST",
                "/account/factors/totp/confirm",
                ("credentialId", begun.Text("id")),
                ("code", Code(begun.Text("secret")))));

        Assert.False(Assert.Single(await _deployment.Authenticators.OfAsync(subject, cancellationToken)).Confirmed);
    }

    private async Task<Browser> SignedInAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Clock.Advance(TimeSpan.FromMinutes(5));

        return browser;
    }

    // What removing a credential is refused with, which is a gate the policy binds and
    // is judged before the credential is looked for (BFF-STEP-001).
    private static async Task<string> RefusedAsync(Browser browser)
    {
        Answer refused = await browser.SendAsync("DELETE", "/account/credentials/" + Guid.NewGuid());

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.StepUpRequired.ToString(), refused.Text("code"));

        return Written(refused.Json().GetProperty("details").GetRawText());
    }

    // JSON as one writer writes it, so that two documents compare member by member
    // and in order, whatever each escaped.
    private static string Written(string json) => JsonNode.Parse(json)!.ToJsonString();

    // A code generator beside the password the account registered with, standing as
    // given; the session the registration opened proved the password alone.
    private void Held(AuthenticatorState state, DateTimeOffset? invalidatesAt) =>
        _deployment.Authenticators.Hold(Authenticator.Existing(
            new AuthenticatorId(Guid.NewGuid()),
            _deployment.Directory.Created[^1].Subject,
            Factor.Totp,
            CredentialLabel.TryParse(Label, out CredentialLabel label)
                ? label
                : throw new InvalidOperationException("The label is not one."),
            state,
            _deployment.Clock.GetUtcNow(),
            lastUsedAt: null,
            invalidatesAt,
            confirmed: true,
            totp: null,
            webAuthn: null));

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
