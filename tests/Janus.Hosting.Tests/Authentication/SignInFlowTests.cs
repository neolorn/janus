using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using OtpNet;
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
                new MessageTemplate(kind is SendKind.Email ? "link" : null, "{code} {link}"));
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
    /// CONV-LOG-005 AC1: a link token presented under a factor that is not a link is
    /// refused naming the factor before the service is reached, so a refused press is
    /// only ever recorded under the link factor the request named.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_LOG_005_AC1_ALinkTokenUnderAnotherFactorIsRefusedAsMalformedAsync()
    {
        await RegisteredAsync();

        var browser = new Browser(_deployment);
        string challenge = await BegunAsync(browser);

        Answer landed = await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "password"),
            ("linkToken", "nothing-answers-to-this"),
            ("press", true));

        Assert.Equal(StatusCodes.Status400BadRequest, landed.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), landed.Text("code"));
        Assert.Equal("factor", landed.Json().GetProperty("details").GetProperty("member").GetString());
    }

    /// <summary>
    /// AUTH-FACT-002 AC4 and AC6: the text code is asked for at the second step by
    /// naming it with no value, which is answered 202; the code it sends, presented,
    /// completes the sign-in at AAL2.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AC4_ATextCodeAskedForAndPresentedCompletesAtAal2Async()
    {
        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);
        _deployment.Configuration.Set(
            Settings.PolicyDefault,
            Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>([.. Policies.SystemDefault.LoginFactors, Factor.PhoneCode]),
            });
        _deployment.Templates.Set(
            MessageKind.SecondStepCode,
            SendKind.Sms,
            Language,
            new MessageTemplate(null, "{code}"));

        await RegisteredAsync();

        _deployment.Authenticators.Hold(Authenticator.Existing(
            AuthenticatorId.New(_deployment.Clock),
            _deployment.Directory.Created[^1].Subject,
            Factor.PhoneCode,
            CredentialLabel.TryParse("Phone", out CredentialLabel label)
                ? label
                : throw new InvalidOperationException("The label does not read."),
            AuthenticatorState.Active,
            _deployment.Clock.GetUtcNow(),
            null,
            null,
            confirmed: true,
            null,
            null,
            isPreferred: false));

        var browser = new Browser(_deployment);
        string challenge = await BegunAsync(browser);
        int sent = _deployment.Sms.Taken.Count;

        Answer first = await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "password"),
            ("value", Flow.Password));
        Answer asked = await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "phoneCode"));
        Answer completed = await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "phoneCode"),
            ("value", _deployment.Sms.Taken[^1].Text));

        Assert.Equal("factorRequired", first.Text("status"));
        Assert.Equal(StatusCodes.Status202Accepted, asked.Status);
        Assert.Equal(sent + 1, _deployment.Sms.Taken.Count);
        Assert.Equal(StatusCodes.Status200OK, completed.Status);
        Assert.Equal("complete", completed.Text("status"));
        Assert.Equal("aal2", completed.Text("assuranceLevel"));
    }

    /// <summary>
    /// AUTH-FACT-002 AC7 and AUTH-FACT-002b AC6: a text code asked for after the first
    /// factor, where the number's signal answers <c>risk</c> by then, sends nothing and
    /// is answered with what the challenge still offers: 200 <c>factorRequired</c>
    /// naming the factors left, and 422 <c>auth.factor.rejected</c> once none is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AC7_ATextCodeAskedForAReportedNumberIsAnsweredWithWhatIsLeftAsync()
    {
        PhoneSignal reported = PhoneSignal.Clear;

        await using var changing = new Deployment(
            signals: new PhoneSignalProvider((_, _) => ValueTask.FromResult(reported)));

        Flow.Prepare(changing);

        changing.Configuration.Set(Settings.DeviceVerificationEnabled, false);
        changing.Configuration.Set(
            Settings.PolicyDefault,
            Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>([.. Policies.SystemDefault.LoginFactors, Factor.PhoneCode]),
            });

        _ = await Flow.SignedInAsync(changing);

        changing.Clock.Advance(TimeSpan.FromMinutes(5));

        SubjectId subject = changing.Directory.Created[^1].Subject;
        Authenticator generator = Held(Factor.Totp, "Generator");

        changing.Authenticators.Hold(Held(Factor.PhoneCode, "Phone"));
        changing.Authenticators.Hold(generator);

        var browser = new Browser(changing);
        string challenge = await BegunAsync(browser);

        Answer first = await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "password"),
            ("value", Flow.Password));

        reported = PhoneSignal.Risk;

        int sent = changing.Sms.Taken.Count;
        Answer left = await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "phoneCode"));

        generator.Invalidate();

        Answer none = await browser.SendAsync(
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "phoneCode"));

        Assert.Equal("factorRequired", first.Text("status"));
        Assert.Equal(StatusCodes.Status200OK, left.Status);
        Assert.Equal("factorRequired", left.Text("status"));
        Assert.Equal(
            ["totp"],
            left.Json().GetProperty("required").EnumerateArray().Select(factor => factor.GetString()));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, none.Status);
        Assert.Equal("auth.factor.rejected", none.Text("code"));
        Assert.Equal(sent, changing.Sms.Taken.Count);

        Authenticator Held(Factor factor, string named) =>
            Authenticator.Existing(
                AuthenticatorId.New(changing.Clock),
                subject,
                factor,
                CredentialLabel.TryParse(named, out CredentialLabel label)
                    ? label
                    : throw new InvalidOperationException("The label does not read."),
                AuthenticatorState.Active,
                changing.Clock.GetUtcNow(),
                null,
                null,
                confirmed: true,
                factor is Factor.Totp ? new TotpMaterial(new byte[20], null) : null,
                null,
                isPreferred: false);
    }

    /// <summary>
    /// AUTH-FACT-002 AC7, `09` `POST /auth/step-up`: a text code asked for at a step-up,
    /// where the number's signal answers <c>risk</c>, sends nothing and is answered 200
    /// <c>factorRequired</c> naming the factors of the combinations left without the
    /// entry and reporting what the factors accepted on the challenge reach, which is
    /// <c>delegated</c>, not phishing-resistant, none having been (AUTH-STEP-002 AC4e),
    /// and 403 <c>auth.stepup.required</c> with <c>report-loss</c> once none is left.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AC7_ATextCodeAskedAtAStepUpForAReportedNumberIsAnsweredWithWhatIsLeftAsync()
    {
        await using var reporting = new Deployment(
            signals: new PhoneSignalProvider((_, _) => ValueTask.FromResult(PhoneSignal.Risk)));

        Flow.Prepare(reporting);

        reporting.Configuration.Set(Settings.DeviceVerificationEnabled, false);
        reporting.Configuration.Set(
            Settings.PolicyDefault,
            Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>([.. Policies.SystemDefault.LoginFactors, Factor.PhoneCode]),
            });

        Browser owner = await Flow.SignedInAsync(reporting);

        reporting.Clock.Advance(TimeSpan.FromMinutes(5));

        SubjectId subject = reporting.Directory.Created[^1].Subject;
        Authenticator generator = Held(Factor.Totp, "Generator");

        reporting.Authenticators.Hold(Held(Factor.PhoneCode, "Phone"));
        reporting.Authenticators.Hold(generator);

        Answer began = await owner.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        string challenge = began.Text("challengeId");
        int sent = reporting.Sms.Taken.Count;

        Answer left = await owner.SendAsync(
            "POST",
            "/auth/step-up",
            ("challengeId", challenge),
            ("factor", "phoneCode"));

        generator.Invalidate();

        Answer none = await owner.SendAsync(
            "POST",
            "/auth/step-up",
            ("challengeId", challenge),
            ("factor", "phoneCode"));

        Assert.Equal(StatusCodes.Status200OK, left.Status);
        Assert.Equal("factorRequired", left.Text("status"));
        Assert.Equal("delegated", left.Text("assuranceLevel"));
        Assert.False(left.Json().GetProperty("phishingResistant").GetBoolean());
        Assert.Equal(
            ["password", "totp"],
            left.Json().GetProperty("required").EnumerateArray().Select(factor => factor.GetString()));
        Assert.Equal(StatusCodes.Status403Forbidden, none.Status);
        Assert.Equal("auth.stepup.required", none.Text("code"));
        Assert.Equal("report-loss", none.Json().GetProperty("details").GetProperty("outcome").GetString());
        Assert.Empty(none.Json().GetProperty("details").GetProperty("options").EnumerateArray());
        Assert.Equal(sent, reporting.Sms.Taken.Count);

        Authenticator Held(Factor factor, string named) =>
            Authenticator.Existing(
                AuthenticatorId.New(reporting.Clock),
                subject,
                factor,
                CredentialLabel.TryParse(named, out CredentialLabel label)
                    ? label
                    : throw new InvalidOperationException("The label does not read."),
                AuthenticatorState.Active,
                reporting.Clock.GetUtcNow(),
                null,
                null,
                confirmed: true,
                factor is Factor.Totp ? new TotpMaterial(new byte[20], null) : null,
                null,
                isPreferred: false);
    }

    /// <summary>
    /// AUTH-FACT-002 AC7, `09` `POST /auth/step-up`: a text code asked for at a step-up,
    /// where the number's signal answers <c>risk</c> and the account's other second step
    /// is under a loss report in flight, sends nothing and is answered 403
    /// <c>auth.stepup.required</c> with the outcome <c>pending</c> and the instant the
    /// report completes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AC7_ATextCodeAskedAtAStepUpWithALossReportInFlightIsAnsweredPendingAsync()
    {
        await using var reporting = new Deployment(
            signals: new PhoneSignalProvider((_, _) => ValueTask.FromResult(PhoneSignal.Risk)));

        Flow.Prepare(reporting);

        reporting.Configuration.Set(Settings.DeviceVerificationEnabled, false);
        reporting.Configuration.Set(
            Settings.PolicyDefault,
            Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>([.. Policies.SystemDefault.LoginFactors, Factor.PhoneCode]),
            });

        Browser owner = await Flow.SignedInAsync(reporting);

        reporting.Clock.Advance(TimeSpan.FromMinutes(5));

        SubjectId subject = reporting.Directory.Created[^1].Subject;
        DateTimeOffset completes = reporting.Clock.GetUtcNow() + TimeSpan.FromDays(7);
        Authenticator generator = Held(Factor.Totp, "Generator");

        generator.Suspend(completes);
        reporting.Authenticators.Hold(Held(Factor.PhoneCode, "Phone"));
        reporting.Authenticators.Hold(generator);

        Answer began = await owner.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        int sent = reporting.Sms.Taken.Count;

        Answer pending = await owner.SendAsync(
            "POST",
            "/auth/step-up",
            ("challengeId", began.Text("challengeId")),
            ("factor", "phoneCode"));

        Assert.Equal(StatusCodes.Status403Forbidden, pending.Status);
        Assert.Equal("auth.stepup.required", pending.Text("code"));
        Assert.Equal("pending", pending.Json().GetProperty("details").GetProperty("outcome").GetString());
        Assert.Equal(completes, pending.Json().GetProperty("details").GetProperty("pendingUntil").GetDateTimeOffset());
        Assert.Empty(pending.Json().GetProperty("details").GetProperty("options").EnumerateArray());
        Assert.Equal(sent, reporting.Sms.Taken.Count);

        Authenticator Held(Factor factor, string named) =>
            Authenticator.Existing(
                AuthenticatorId.New(reporting.Clock),
                subject,
                factor,
                CredentialLabel.TryParse(named, out CredentialLabel label)
                    ? label
                    : throw new InvalidOperationException("The label does not read."),
                AuthenticatorState.Active,
                reporting.Clock.GetUtcNow(),
                null,
                null,
                confirmed: true,
                factor is Factor.Totp ? new TotpMaterial(new byte[20], null) : null,
                null,
                isPreferred: false);
    }

    /// <summary>
    /// CONV-DESIGN-006 and AUTH-ABUSE-001: a generator's code presented a second time
    /// is answered 422 `auth.code.replayed` at a step-up and at a sign-in alike, a code
    /// each route declares with the rest of the failed attempts chapter 09 lists.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_006_AReplayedCodeIsAnsweredAtAStepUpAndAtASignInAsync()
    {
        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        Browser owner = await Flow.SignedInAsync(_deployment);

        _deployment.Clock.Advance(TimeSpan.FromMinutes(5));
        _deployment.Authenticators.Hold(Authenticator.Existing(
            AuthenticatorId.New(_deployment.Clock),
            _deployment.Directory.Created[^1].Subject,
            Factor.Totp,
            CredentialLabel.TryParse("Generator", out CredentialLabel label)
                ? label
                : throw new InvalidOperationException("The label does not read."),
            AuthenticatorState.Active,
            _deployment.Clock.GetUtcNow(),
            null,
            null,
            confirmed: true,
            new TotpMaterial(new byte[20], null),
            null,
            isPreferred: false));

        string generated = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(_deployment.Clock.GetUtcNow().UtcDateTime);

        var fresh = new Browser(_deployment);

        _ = await fresh.SendAsync("GET", "/register");

        Answer raised = await PresentedAsync(owner, "/auth/step-up");
        Answer again = await PresentedAsync(owner, "/auth/step-up");
        Answer elsewhere = await PresentedAsync(fresh, "/auth/factor");

        Assert.Equal(StatusCodes.Status200OK, raised.Status);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, again.Status);
        Assert.Equal("auth.code.replayed", again.Text("code"));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, elsewhere.Status);
        Assert.Equal("auth.code.replayed", elsewhere.Text("code"));

        async Task<Answer> PresentedAsync(Browser browser, string route)
        {
            Answer began = await browser.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));

            return await browser.SendAsync(
                "POST",
                route,
                ("challengeId", began.Text("challengeId")),
                ("factor", "totp"),
                ("value", generated));
        }
    }

    /// <summary>
    /// REG-DOM-001, `09` `POST /auth/step-up`: at a step-up a right email code sent to an
    /// address the domain lock now refuses answers 422
    /// <c>identity.identifier.domainnotallowed</c>, and the code is spent: presented
    /// again it answers as a code that is gone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_DOM_001_ARightEmailCodeAtAStepUpToAnAddressTheLockRefusesAnswersTheLocksCodeAsync()
    {
        var locked = new OrganizationId(Guid.NewGuid());
        _deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);
        _deployment.Configuration.Set(
            Settings.PolicyDefault,
            Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>([.. Policies.SystemDefault.LoginFactors, Factor.EmailCode]),
            });
        _deployment.Templates.Set(
            MessageKind.SignInCode,
            SendKind.Email,
            Language,
            new MessageTemplate("code", "{code}"));
        Browser owner = await Flow.SignedInAsync(_deployment);
        _deployment.Clock.Advance(TimeSpan.FromMinutes(5));
        Answer began = await owner.SendAsync("POST", "/auth/begin", ("identifier", Flow.Address));
        _ = await owner.SendAsync("POST", "/auth/email-otp", ("identifier", Flow.Address));
        string code = Emailed();
        _deployment.Memberships.Place(_deployment.Directory.Created[^1].Subject, locked);
        _deployment.Configuration.Set(
            Settings.OrganizationPolicy,
            locked.ToString(),
            PolicyOverride.None with { EmailDomains = ["elsewhere.test"] });

        Answer refused = await PresentedAsync();
        Answer again = await PresentedAsync();

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal(ErrorCodes.IdentifierDomainNotAllowed.ToString(), refused.Text("code"));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, again.Status);
        Assert.Equal(ErrorCodes.CodeExpired.ToString(), again.Text("code"));

        Task<Answer> PresentedAsync() =>
            owner.SendAsync(
                "POST",
                "/auth/step-up",
                ("challengeId", began.Text("challengeId")),
                ("factor", "emailCode"),
                ("value", code));
    }

    /// <summary>
    /// AUTH-FACT-002b AC6 and AUTH-ABUSE-003 AC1: a sign-in link asked for by text to a
    /// number the carrier reports a recent change for is not sent, and the ask is
    /// answered 202 in the bytes an ask for a number no account holds is answered in.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_ALinkByTextToAReportedNumberIsAnsweredAsAnyAskAsync()
    {
        await using var risky = new Deployment(
            signals: new PhoneSignalProvider((_, _) => ValueTask.FromResult(PhoneSignal.Risk)));

        Flow.Prepare(risky);

        risky.Configuration.Set(
            Settings.PolicyDefault,
            Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>([.. Policies.SystemDefault.LoginFactors, Factor.PhoneLink]),
            });
        risky.Templates.Set(
            MessageKind.SignInLink,
            SendKind.Sms,
            Language,
            new MessageTemplate(null, "{code} {token}"));

        _ = await Flow.SignedInAsync(risky);

        risky.Clock.Advance(TimeSpan.FromMinutes(5));

        int sent = risky.Sms.Taken.Count;
        var browser = new Browser(risky);

        _ = await browser.SendAsync("GET", "/auth/session");

        Answer held = await browser.SendAsync("POST", "/auth/link", ("identifier", Flow.Number));
        Answer nobodys = await browser.SendAsync("POST", "/auth/link", ("identifier", "+441632960099"));

        Assert.Equal(StatusCodes.Status202Accepted, held.Status);
        Assert.Equal(held.Status, nobodys.Status);
        Assert.Equal(held.Body, nobodys.Body);
        Assert.Equal(sent, risky.Sms.Taken.Count);
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
