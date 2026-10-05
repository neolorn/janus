using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Factors;
using Janus.Authentication.Invitations;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OtpNet;
using Xunit;

namespace Janus.Hosting.Tests.Registration;

/// <summary>
/// The registration flow as a browser drives it: the pre-authentication session it
/// is bound to, what a browser without that cookie may do, where the tokens are, and
/// that it asks nothing of the mail server (BFF-CSRF-005a, BFF-CSRF-005b,
/// REG-SESS-001, INT-MAIL-006).
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
    /// REG-SESS-008, API-REDIR-002 (D-166, 145): the session the terms step established
    /// keeps the client the registration captured, and the done step reads its return
    /// from the session as <c>landing</c>, the origin of that client's registered
    /// address and nothing more of it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_SESS_008_TheDoneStepReadsItsReturnFromTheSessionAsync()
    {
        await _deployment.Clients.AddAsync(
            new OidcClient(
                "web",
                "web",
                OidcClientKind.BrowserApplication,
                "https://app.example.test:8443/signin/callback",
                ["openid"]),
            Encoding.UTF8.GetBytes("a-secret-the-deployment-set"),
            _deployment.Clock.GetUtcNow(),
            TestContext.Current.CancellationToken);

        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer read = await browser.SendAsync("GET", "/auth/session");

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.Equal("https://app.example.test:8443", read.Text("landing"));
    }

    /// <summary>
    /// REG-SESS-008: a registration that captured no client the registry holds leaves
    /// nothing on its session, so the session answers no <c>landing</c> at all.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_SESS_008_ASessionThatCapturedNoClientAnswersNoLandingAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer read = await browser.SendAsync("GET", "/auth/session");

        Assert.Equal(StatusCodes.Status200OK, read.Status);
        Assert.False(read.Json().TryGetProperty("landing", out _));
    }

    /// <summary>
    /// REG-IDENT-006 AC2, REG-SESS-005: an address its owner removed is held out of
    /// reach for the undo, so a registration that tries it meanwhile is answered as for
    /// a held one, sent nothing, and the owner's undo then restores the address.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_IDENT_006_AC2_TheUndoRestoresAnAddressARegistrationTriedToTakeAsync()
    {
        const string second = "second@example.test";

        _deployment.Templates.Set(
            MessageKind.IdentifierRemoved,
            SendKind.Email,
            "en",
            new MessageTemplate("removed", "{link}"));

        Browser owner = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        Guid going = _deployment.Identifiers.Verified(subject, IdentifierKind.Email, second).Value;

        Assert.Equal(
            StatusCodes.Status204NoContent,
            (await owner.SendAsync("DELETE", "/account/identifiers/" + going)).Status);

        string undo = Landing.Token(_deployment.Mail.Taken
            .Last(sent => string.Equals(sent.Subject, "removed", StringComparison.Ordinal))
            .Body);
        int sent = _deployment.Mail.Taken.Count;

        Browser other = await Flow.BegunAsync(_deployment);

        _ = await other.SendAsync("PUT", "/register/age", ("dateOfBirth", "1990-01-01"));

        Answer staged = await other.SendAsync("PUT", "/register/email", ("value", second));

        Assert.Equal(StatusCodes.Status202Accepted, staged.Status);
        Assert.Equal(sent, _deployment.Mail.Taken.Count);

        Answer restored = await owner.SendAsync(
            "POST",
            "/account/identifiers/" + going + "/undo",
            ("linkToken", undo));

        Assert.Equal(StatusCodes.Status204NoContent, restored.Status);
        Assert.Equal(
            subject,
            await _deployment.Identifiers.OwnerAsync(
                IdentifierKind.Email,
                second,
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// INT-MAIL-006 AC2: a customer who registers is given no mailbox: none is
    /// reserved, none is written down, and nothing reaches the mail server.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_006_AC2_RegisteringACustomerProvisionsNoMailboxAsync()
    {
        _ = await Flow.SignedInAsync(_deployment);

        Assert.Empty(_deployment.Mailboxes.Held);
        Assert.Equal(0, _deployment.Mailboxes.Recorded);
        Assert.Empty(_deployment.MailServer.Received);
    }

    /// <summary>
    /// INT-MAIL-006 AC4: registration asks nothing of the mail server, so a customer
    /// registers while it cannot be reached.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_006_AC4_AnUnreachableMailServerDoesNotBlockRegistrationAsync()
    {
        _deployment.MailServer.Unreachable = true;

        _ = await Flow.SignedInAsync(_deployment);

        Assert.Single(_deployment.Directory.Created);
        Assert.Empty(_deployment.MailServer.Received);
    }

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

        Assert.True(browser.Cookies.ContainsKey("__Host-identity-preauth"));
        Assert.True(browser.Cookies.ContainsKey("__Host-identity-csrf"));
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
    /// IDN-LIFE-009a AC2 and API-LAND-001 AC2: an invitation token that opens no
    /// invitation begins no registration, and the landing is answered with the code
    /// it renders.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_009a_AC2_AnInvitationTokenThatOpensNothingIsRefusedAsync()
    {
        var browser = new Browser(_deployment);

        _ = await browser.SendAsync("GET", "/register");

        Answer refused = await browser.SendAsync(
            "POST",
            "/register",
            ("clientId", "web"),
            ("invitationToken", "no-such-invitation"));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal(ErrorCodes.InvitationExpired.ToString(), refused.Text("code"));
        Assert.Empty(_deployment.Registrations.All);
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
        Assert.Equal(ErrorCodes.ResourceNotFound.ToString(), stream.Text("code"));

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
    /// REG-SESS-006 AC1, API section 4: the WebAuthn enrolment endpoints accept the
    /// registration session at the security step, the ceremony runs under the staged
    /// email, and a passkey alone carries the registration to an account that holds it
    /// and no password.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_SESS_006_AC1_APasskeyAloneCompletesTheSecurityStepOverTheWireAsync()
    {
        Browser browser = await Flow.ConfirmedAsync(_deployment);

        Answer begun = await browser.SendAsync(
            "POST",
            "/auth/webauthn/register/begin",
            ("kind", "passkey"));
        Answer created = await browser.SendAsync(
            "POST",
            "/auth/webauthn/register/complete",
            Attested(begun.Text("challenge")));
        Answer state = await browser.SendAsync("GET", "/register");
        Answer completed = await browser.SendAsync(
            "POST",
            "/register/terms",
            ("termsVersion", "terms-3"),
            ("noticeVersion", "notice-2"));

        Assert.Equal(StatusCodes.Status200OK, begun.Status);
        Assert.Equal(Flow.Address, begun.Json().GetProperty("user").GetProperty("name").GetString());
        Assert.Equal(StatusCodes.Status200OK, created.Status);
        Assert.Equal("terms", state.Text("step"));
        Assert.Equal(StatusCodes.Status201Created, completed.Status);
        Authenticator held = Assert.Single(_deployment.Authenticators.All);
        Assert.Equal(Factor.Passkey, held.Factor);
        Assert.Equal(_deployment.Directory.Created[^1].Subject, held.Subject);
        Assert.Null(await _deployment.Passwords.FindAsync(held.Subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-SESS-006 AC4, API section 4: the TOTP enrolment endpoints accept the
    /// registration session at the security step, and a generator confirmed beside a
    /// password that does not stand alone answers with the recovery codes and
    /// completes the step.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_SESS_006_AC4_AGeneratorBesideAPasswordShowsRecoveryCodesOverTheWireAsync()
    {
        _deployment.Configuration.Set(Settings.ServiceName, "Example");
        Browser browser = await Flow.ConfirmedAsync(_deployment);
        Answer set = await browser.SendAsync("PUT", "/register/security", ("password", "tenletters12"));

        Answer begun = await browser.SendAsync(
            "POST",
            "/account/factors/totp/begin",
            ("label", "Authenticator"));
        Answer confirmed = await browser.SendAsync(
            "POST",
            "/account/factors/totp/confirm",
            ("credentialId", begun.Text("id")),
            ("code", Generated(begun.Text("secret"))));
        Answer state = await browser.SendAsync("GET", "/register");

        Assert.Equal("security", set.Text("step"));
        Assert.Equal(StatusCodes.Status200OK, begun.Status);
        Assert.Equal(StatusCodes.Status200OK, confirmed.Status);
        Assert.Equal(
            Settings.FactorRecoveryCodesCount.Default,
            confirmed.Json().GetProperty("recoveryCodes").GetArrayLength());
        Assert.Equal("terms", state.Text("step"));
        Assert.Empty(_deployment.Authenticators.All);
    }

    /// <summary>
    /// AUTH-FACT-008 AC4, REG-SESS-006: the set a registration's security step returned
    /// is read from the account as viewed at the instant that step returned it, not at
    /// the instant the terms step wrote it, and as not exported.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AC4_ARegistrationsSetIsReadFromTheAccountAsViewedAtTheSecurityStepAsync()
    {
        _deployment.Configuration.Set(Settings.ServiceName, "Example");
        Browser browser = await Flow.ConfirmedAsync(_deployment);
        _ = await browser.SendAsync("PUT", "/register/security", ("password", "tenletters12"));
        Answer begun = await browser.SendAsync("POST", "/account/factors/totp/begin", ("label", "Authenticator"));
        DateTimeOffset returned = _deployment.Clock.GetUtcNow();
        Answer confirmed = await browser.SendAsync(
            "POST",
            "/account/factors/totp/confirm",
            ("credentialId", begun.Text("id")),
            ("code", Generated(begun.Text("secret"))));
        _deployment.Clock.Advance(TimeSpan.FromMinutes(4));

        Answer completed = await browser.SendAsync(
            "POST",
            "/register/terms",
            ("termsVersion", "terms-3"),
            ("noticeVersion", "notice-2"));
        Flow.Carried(_deployment);
        Answer account = await browser.SendAsync("GET", "/account");

        Assert.Equal(StatusCodes.Status200OK, confirmed.Status);
        Assert.Equal(StatusCodes.Status201Created, completed.Status);
        Assert.Equal(StatusCodes.Status200OK, account.Status);
        Assert.Equal(
            returned,
            account.Json().GetProperty("recoveryCodes").GetProperty("viewedAt").GetDateTimeOffset());
        Assert.Equal(
            JsonValueKind.Null,
            account.Json().GetProperty("recoveryCodes").GetProperty("exportedAt").ValueKind);
    }

    /// <summary>
    /// REG-SESS-006: the registration session is accepted in place of an account's
    /// session for the security step alone, so before it the enrolment endpoints
    /// answer a registering browser as they answer one that holds nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_SESS_006_TheRegistrationSessionEnrolsAtNoEarlierStepAsync()
    {
        Browser browser = await Flow.AwaitingAsync(_deployment);

        Answer key = await browser.SendAsync(
            "POST",
            "/auth/webauthn/register/begin",
            ("kind", "passkey"));
        Answer generator = await browser.SendAsync(
            "POST",
            "/account/factors/totp/begin",
            ("label", "Authenticator"));

        Assert.Equal(StatusCodes.Status401Unauthorized, key.Status);
        Assert.Equal(StatusCodes.Status401Unauthorized, generator.Status);
    }

    /// <summary>
    /// REG-SESS-005 AC5: over the wire, a code presented for an address an account
    /// holds is answered in the same bytes as a wrong code for a fresh address:
    /// <c>auth.code.invalid</c> for each try up to the cap, then
    /// <c>auth.code.expired</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_SESS_005_AC5_ACodeForAHeldAddressIsAnsweredInTheBytesOfAWrongOneAsync()
    {
        const string trace = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        _ = await Flow.SignedInAsync(_deployment);
        _deployment.Clock.Advance(TimeSpan.FromMinutes(2));
        Browser fresh = await Flow.BegunAsync(_deployment);
        Browser duplicate = await Flow.BegunAsync(_deployment);
        _ = await fresh.SendAsync("PUT", "/register/age", ("dateOfBirth", "1990-01-01"));
        _ = await duplicate.SendAsync("PUT", "/register/age", ("dateOfBirth", "1990-01-01"));
        Answer staged = await fresh.SendAsync("PUT", "/register/email", ("value", "fresh@example.test"));
        Answer taken = await duplicate.SendAsync("PUT", "/register/email", ("value", Flow.Address));
        string wrong = string.Equals(Flow.Code(_deployment, IdentifierKind.Email), "000000", StringComparison.Ordinal)
            ? "111111"
            : "000000";
        string one = Flow.Waiting(await fresh.SendAsync("GET", "/register"), IdentifierKind.Email);
        string other = Flow.Waiting(await duplicate.SendAsync("GET", "/register"), IdentifierKind.Email);
        var answers = new List<(Answer Fresh, Answer Duplicate)>();

        for (int attempt = 0; attempt < Settings.CodeVerificationAttempts.Default + 1; attempt++)
        {
            _deployment.Clock.Advance(TimeSpan.FromSeconds(30));

            answers.Add((
                await fresh.SendAsync(IPAddress.Parse("198.51.100.7"), trace, "POST", "/register/verify/" + one, ("code", wrong)),
                await duplicate.SendAsync(IPAddress.Parse("203.0.113.9"), trace, "POST", "/register/verify/" + other, ("code", wrong))));
        }

        Assert.Equal(StatusCodes.Status202Accepted, staged.Status);
        Assert.Equal(staged.Status, taken.Status);
        Assert.All(answers, answer =>
        {
            Assert.Equal(answer.Fresh.Status, answer.Duplicate.Status);
            Assert.Equal(answer.Fresh.Body, answer.Duplicate.Body);
        });
        Assert.All(
            answers.Take(Settings.CodeVerificationAttempts.Default),
            answer => Assert.Equal("auth.code.invalid", answer.Duplicate.Text("code")));
        Assert.Equal("auth.code.expired", answers[^1].Duplicate.Text("code"));
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
        Assert.True(browser.Cookies.ContainsKey("__Host-identity-session"));
        Assert.False(browser.Cookies.ContainsKey("__Host-identity-preauth"));
        Assert.Empty(_deployment.Contacts.All);
        Assert.Single(_deployment.Sessions.All);
    }

    /// <summary>
    /// PRIV-CONS-001 AC1, REG-SESS-007, 09 section 3: a ticked control whose purpose
    /// takes no consent is refused 422 <c>privacy.purpose.noconsent</c> by the privacy
    /// area's own consent operation, and the browser is signed in to nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_CONS_001_AC1_AControlForAPurposeTakingNoConsentIsRefusedAsync()
    {
        Browser browser = await Flow.SecuredAsync(_deployment);

        Answer refused = await browser.SendAsync(
            "POST",
            "/register/terms",
            ("termsVersion", "terms-3"),
            ("noticeVersion", "notice-2"),
            ("consents", new Dictionary<string, bool>(StringComparer.Ordinal) { ["performance"] = true }));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal(ErrorCodes.PurposeNoConsent.ToString(), refused.Text("code"));
        Assert.False(browser.Cookies.ContainsKey("__Host-identity-session"));
    }

    /// <summary>
    /// A browser that already holds a session is refused, nothing is staged for it, and
    /// no account document crosses a registration route (REG-SESS-002).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BeginAsync_ABrowserAlreadySignedIn_IsRefusedAndStagesNothingAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        int staged = _deployment.Registrations.All.Count;

        Answer refused = await browser.SendAsync("POST", "/register", ("clientId", "web"));

        Assert.Equal(StatusCodes.Status409Conflict, refused.Status);
        Assert.Equal(ErrorCodes.RegistrationSignedIn.ToString(), refused.Text("code"));
        Assert.Equal(staged, _deployment.Registrations.All.Count);

        Answer account = await browser.SendAsync("GET", "/account");

        Assert.NotEqual(account.Body, refused.Body);
    }

    /// <summary>
    /// REG-INV-002 AC1: a signed-in browser that presses an invitation link is sent to
    /// its account with the invitation attached there, and no registration is staged;
    /// a token that opens nothing is answered with its code.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_INV_002_AC1_ALinkPressedWhileSignedInAttachesToTheAccountAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId holder = _deployment.Directory.Created[^1].Subject;
        using var randomness = RandomNumberGenerator.Create();
        var token = OpaqueToken.Draw(randomness);

        _deployment.Invitations.Held.Add(Invitation.Issued(
            InvitationId.New(TimeProvider.System),
            OrganizationId.New(TimeProvider.System),
            SubjectId.New(randomness),
            new InvitedIdentifiers(Email: null, Phone: null, CorporateEmail: null),
            roles: [],
            documents: [],
            mailbox: null,
            token.Fingerprint(),
            _deployment.Clock.GetUtcNow(),
            TimeSpan.FromDays(7)));

        int staged = _deployment.Registrations.All.Count;

        Answer landed = await browser.SendAsync(
            "POST",
            "/register",
            ("clientId", "web"),
            ("invitationToken", token.Value));

        Assert.Equal(ErrorCodes.RegistrationSignedIn.ToString(), landed.Text("code"));
        Assert.Equal(holder, _deployment.Invitations.Held.Single().Invitee);
        Assert.Equal(staged, _deployment.Registrations.All.Count);

        Answer spent = await browser.SendAsync(
            "POST",
            "/register",
            ("clientId", "web"),
            ("invitationToken", token.Value));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, spent.Status);
        Assert.Equal(ErrorCodes.InvitationExpired.ToString(), spent.Text("code"));
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
        Assert.Equal(ErrorCodes.ResourceNotFound.ToString(), claimed.Text("code"));
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
    /// REG-SESS-003: what wakes the stream is the session being signalled, and the
    /// interval is the fallback. With the interval set far enough out that the test
    /// would still be waiting for it, what reaches the waiting screen reaches it
    /// because the session was signalled.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_SESS_003_TheSignalWakesTheStreamBeforeTheIntervalAsync()
    {
        _deployment.Configuration.Set(
            Settings.RegistrationEventsPollInterval,
            TimeSpan.FromMinutes(5));

        Browser browser = await Flow.AwaitingAsync(_deployment);

        using var abort = new CancellationTokenSource();

        (Task running, ResponseBody written) = browser.Open("/register/events", abort.Token);

        await SettledAsync(written);
        await Flow.VerifiedAsync(_deployment, browser, IdentifierKind.Email);

        // The step is verified and the screen has heard nothing: the interval it would
        // otherwise read on is five minutes out.
        await SettledAsync(written);

        _deployment.Signals.Raise(_deployment.Registrations.All.Single().Id);

        string streamed = await SentAsync(written);

        await abort.CancelAsync();

        try
        {
            await running;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.Equal((await browser.SendAsync("GET", "/register")).Body, streamed);
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

    /// <summary>
    /// REG-SESS-003 AC6 (D-189): a code for an identifier the session does not hold is
    /// answered 422 <c>auth.code.invalid</c>, and the identifier the session does hold
    /// is left waiting.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_SESS_003_AC6_ACodeForAnIdentifierTheSessionDoesNotHoldIsAnsweredInvalidAsync()
    {
        Browser browser = await Flow.AwaitingAsync(_deployment);
        string identifier = Flow.Waiting(await browser.SendAsync("GET", "/register"), IdentifierKind.Email);

        Answer presented = await browser.SendAsync(
            "POST",
            "/register/verify/" + Guid.CreateVersion7().ToString("D"),
            ("code", "000000"));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, presented.Status);
        Assert.Equal(ErrorCodes.CodeInvalid.ToString(), presented.Text("code"));
        Assert.Equal(identifier, Flow.Waiting(await browser.SendAsync("GET", "/register"), IdentifierKind.Email));
    }

    /// <summary>
    /// REG-SESS-003 AC6, REG-SESS-001 AC2: a press of a link token that opens nothing,
    /// from a browser holding no registration session, is taken without that session's
    /// cookie, answered 422 <c>auth.code.expired</c> and counted against the source of
    /// the request, so that source's presses are refused 429 <c>auth.throttled</c>
    /// with <c>retryAt</c> once its delay stands, and another source's are not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_SESS_003_AC6_APressedTokenThatOpensNothingIsCountedOverTheWireAsync()
    {
        var pressing = IPAddress.Parse("2001:db8:1:1::9");
        var browser = new Browser(_deployment);
        string identifier = Guid.CreateVersion7().ToString();

        _ = await browser.SendAsync("GET", "/auth/session", source: pressing);

        for (int press = 0; press < Settings.AbuseThrottleThreshold.Default; press++)
        {
            Answer gone = await PressedAsync(browser, pressing);

            Assert.Equal(StatusCodes.Status422UnprocessableEntity, gone.Status);
            Assert.Equal(ErrorCodes.CodeExpired.ToString(), gone.Text("code"));
        }

        Answer held = await PressedAsync(browser, IPAddress.Parse("2001:db8:1:1::a"));
        Answer elsewhere = await PressedAsync(browser, IPAddress.Parse("2001:db8:1:2::9"));

        Assert.Equal(StatusCodes.Status429TooManyRequests, held.Status);
        Assert.Equal(ErrorCodes.Throttled.ToString(), held.Text("code"));
        Assert.Equal(
            _deployment.Clock.GetUtcNow() + Settings.AbuseThrottleDelayInitial.Default,
            held.Json().GetProperty("details").GetProperty("retryAt").GetDateTimeOffset());
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, elsewhere.Status);

        Task<Answer> PressedAsync(Browser from, IPAddress source) =>
            from.SendAsync(
                source,
                "pressed",
                "POST",
                "/register/verify/" + identifier,
                ("linkToken", "a-token-no-registration-sent"),
                ("press", true));
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

    private static string Carried(Browser browser) => browser.Cookies["__Host-identity-preauth"];

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

    // What a browser sends back from a creation ceremony: the challenge the server
    // issued, an origin the relying party admits, and a key the runtime can read.
    private static string Attested(string challenge)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        byte[] clientData = Encoding.UTF8.GetBytes(
            "{\"type\":\"webauthn.create\",\"challenge\":\""
            + challenge
            + "\",\"origin\":\"https://identity.example.test\"}");

        byte[] authenticatorData = new byte[37];

        SHA256.HashData(Encoding.UTF8.GetBytes("identity.example.test")).CopyTo(authenticatorData, 0);

        // User present and user verified, with the two backup flags of a synced
        // credential (AUTH-FACT-013).
        authenticatorData[32] = 0x1D;

        return "{\"credential\":{\"credentialId\":\""
            + Base64Url.EncodeToString(Guid.NewGuid().ToByteArray())
            + "\",\"clientDataJson\":\""
            + Base64Url.EncodeToString(clientData)
            + "\",\"authenticatorData\":\""
            + Base64Url.EncodeToString(authenticatorData)
            + "\",\"publicKey\":\""
            + Base64Url.EncodeToString(key.ExportSubjectPublicKeyInfo())
            + "\",\"algorithm\":-7},\"label\":\"This phone\"}";
    }

    // The code an authenticator app shows now for the secret it was given.
    private string Generated(string secret) =>
        new Totp(
                Base32Encoding.ToBytes(secret),
                TotpCodes.StepSeconds,
                OtpHashMode.Sha1,
                TotpCodes.Digits)
            .ComputeTotp(_deployment.Clock.GetUtcNow().UtcDateTime);
}
