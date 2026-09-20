using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.SignIn;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Sending;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.SignIn;

/// <summary>
/// Signing in: what a challenge discloses, what each factor reaches, when a second
/// step is asked for, when the new-device check holds a sign-in, and what a sign-in
/// link does where it is opened (AUTH-FACT-002b, AUTH-FACT-003, AUTH-FACT-015 to
/// AUTH-FACT-017, AUTH-ABUSE-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class AuthenticationServiceTests : IAsyncDisposable
{
    private const string Language = "en";
    private const string Source = "198.51.100.7";
    private const string Address = "person@example.test";
    private const string Elsewhere = "nobody@example.test";
    private const string Number = "+441632960011";
    private const string Secret = "orangemarmaladeandtoast";

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly DeviceDescription Browser = new("Firefox", "Fedora");

    private readonly ChallengeStoreInMemory _challenges = new();
    private readonly PendingSignInStoreInMemory _pending = new();
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly AccountDirectoryInMemory _accounts = new(PreferenceDeclarations.None);
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly LeakedPasswordCorpusInMemory _corpus = new();
    private readonly WordListInMemory _words = new();
    private readonly ScreeningLogInMemory _screening = new();
    private readonly RecoveryCodeStoreInMemory _sets = new();
    private readonly CredentialAuditInMemory _credentials = new();
    private readonly DeviceStoreInMemory _devices = new();
    private readonly SessionStoreInMemory _live = new();
    private readonly SessionAuditInMemory _audit = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly ThrottleLedgerInMemory _throttle = new();
    private readonly SendLedgerInMemory _ledger = new();
    private readonly NoticeLedgerInMemory _notices = new();
    private readonly MessageTemplatesInMemory _templates = new();
    private readonly MailTransportInMemory _mail = new();
    private readonly SmsTransportInMemory _sms = new();
    private readonly SmsBalanceLedgerInMemory _balances = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment that has named the one key with no default and holds a template
    /// for every message a sign-in sends, in the shape the message goes out in.
    /// </summary>
    public AuthenticationServiceTests()
    {
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);
        _configuration.Set(Settings.WebAuthnRelyingPartyId, "example.test");
        _configuration.Set(Settings.WebAuthnOrigins, ["https://example.test"]);

        MessageKind[] messages =
        [
            MessageKind.VerificationCode,
            MessageKind.SignInLink,
            MessageKind.NoAccount,
        ];

        foreach (SendKind kind in Enum.GetValues<SendKind>())
        {
            foreach (MessageKind message in messages)
            {
                _templates.Set(
                    message,
                    kind,
                    Language,
                    new MessageTemplate(kind is SendKind.Email ? "subject" : null, "{code} {token}"));
            }
        }
    }

    private AuthenticationService Service =>
        new(
            _challenges,
            Links,
            _identifiers,
            _accounts,
            _authenticators,
            _passwords,
            Passwords,
            new TotpService(_authenticators, _passwords, _configuration, _work, _clock, _randomness),
            new RecoveryCodeService(
                _sets,
                new Argon2idHasher(_randomness),
                _configuration,
                _work,
                _clock,
                _randomness),
            new WebAuthnService(
                _authenticators,
                _passwords,
                _credentials,
                _configuration,
                _work,
                _clock,
                _randomness),
            Devices,
            _live,
            Sessions,
            Policies,
            Throttle,
            Sending,
            _configuration,
            _work,
            _clock,
            _randomness);

    private SignInLinks Links =>
        new(
            _pending,
            _identifiers,
            _accounts,
            Policies,
            Sending,
            new NonExistenceNotice(_configuration, Sending, _notices, _work, _events, _clock),
            Throttle,
            _configuration,
            _work,
            _clock,
            _randomness);

    private PolicyResolution Policies => new(_memberships, _configuration, _raises);

    private DeviceService Devices =>
        new(_devices, _configuration, _work, _events, _clock, _randomness);

    private SessionService Sessions =>
        new(_live, _audit, Policies, _configuration, _gate, _work, _clock, _randomness);

    private ThrottleService Throttle =>
        new(_configuration, _throttle, _work, _events, _clock);

    private PasswordService Passwords =>
        new(
            _passwords,
            new PasswordScreening(_corpus, _words, _configuration, _screening),
            new Argon2idHasher(_randomness),
            _configuration,
            _work,
            _clock);

    private SendingService Sending =>
        new(
            _configuration,
            _ledger,
            _templates,
            _mail,
            _sms,
            RestrictionKeySuppliers.None,
            Considered.Nothing(_work, _clock),
            new SmsBalance(_configuration, _sms, _balances, _work, _events, _clock),
            _work,
            _events,
            _clock,
            _randomness);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-ABUSE-003 AC1: the challenge an identifier no account holds opens carries
    /// the same factors, and the same shape of answer, as one an account holds.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_003_AC1_AnIdentifierNoAccountHoldsOpensTheSameChallengeAsync()
    {
        await AccountAsync();

        SignInChallenge held = await BeganAsync(Address);
        SignInChallenge none = await BeganAsync(Elsewhere);

        Assert.Equal(held.Available, none.Available);
        Assert.Equal(held.WebAuthn.RelyingPartyId, none.WebAuthn.RelyingPartyId);
        Assert.NotEqual(held.Challenge, none.Challenge);
    }

    /// <summary>
    /// AUTH-ABUSE-003 AC2: a factor presented against a challenge that resolved to no
    /// account is refused with what a wrong password is refused with.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_003_AC2_AFactorAgainstNoAccountIsRefusedTheSameWayAsync()
    {
        SubjectId subject = await AccountAsync();

        SignInChallenge none = await BeganAsync(Elsewhere);
        Result<SignInProgress> nowhere = await PresentAsync(none.Challenge, Factor.Password, Secret);

        SignInChallenge held = await BeganAsync(Address);
        Result<SignInProgress> wrong = await PresentAsync(held.Challenge, Factor.Password, "wrong" + Secret);

        Assert.Equal(ErrorCodes.FactorRejected, Refused(nowhere));
        Assert.Equal(ErrorCodes.FactorRejected, Refused(wrong));
        Assert.NotEqual(default, subject);
    }

    /// <summary>
    /// AUTH-FACT-002b AC4: a second-step challenge offers the account's preferred
    /// method first and the others from it.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC4_TheSecondStepChallengeOffersThePreferredMethodFirstAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Holds(subject, Factor.SecurityKey, isPreferred: true);

        SignInProgress reached = await SignedInAsync(subject, Factor.Password, Secret);

        Assert.Equal(SignInStatus.FactorRequired, reached.Status);
        Assert.Equal([Factor.SecurityKey, Factor.Totp], reached.Required);
    }

    /// <summary>
    /// IDN-ATTR-008 AC4: the order the challenge presents is the account's preference
    /// and not a fixed one, and every other enrolled method is still offered from it.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_008_AC4_TheChallengeFollowsThePreferenceAndOffersTheRestAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.SecurityKey);
        Holds(subject, Factor.Totp, isPreferred: true);

        SignInProgress reached = await SignedInAsync(subject, Factor.Password, Secret);

        Assert.Equal(SignInStatus.FactorRequired, reached.Status);
        Assert.Equal([Factor.Totp, Factor.SecurityKey], reached.Required);
    }

    /// <summary>
    /// AUTH-RECOV-007a AC2: a password an invalidation left below the single-factor
    /// floor signs in and is told to change it, rather than being locked out.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_007a_AC2_ABelowFloorPasswordSignsInAndIsToldToChangeItAsync()
    {
        SubjectId subject = await AccountAsync();

        Remembered(subject);

        Password held = await _passwords.FindAsync(subject, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The account holds no password.");

        held.RequireChange();

        await _passwords.SetAsync(held, TestContext.Current.CancellationToken);

        SignInProgress reached = await SignedInAsync(subject, Factor.Password, Secret);

        Assert.Equal(SignInStatus.Complete, reached.Status);
        Assert.True(reached.PasswordChangeRequired);
    }

    /// <summary>
    /// AUTH-FACT-002b AC2: an account holding no second step reaches its session with
    /// the password alone, because there is nothing for a second step to be second to.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC2_APasswordOnlyAccountIsAskedForNoSecondStepAsync()
    {
        SubjectId subject = await AccountAsync();

        Remembered(subject);

        SignInProgress reached = await SignedInAsync(subject, Factor.Password, Secret);

        Assert.Equal(SignInStatus.Complete, reached.Status);
        Assert.Empty(reached.Required);
    }

    /// <summary>
    /// AUTH-FACT-003 AC2: a session a sign-in link alone authenticated records AAL1.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_003_AC2_ASignInLinkAloneRecordsAal1Async()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailLink);
        Remembered(subject);

        SignInLanding landed = await PressedAsync(subject);

        Assert.NotNull(landed.SignedIn);
        Assert.Equal(SignInStatus.Complete, landed.SignedIn.Status);
        Assert.Equal(AssuranceLevel.Aal1, landed.SignedIn.AssuranceLevel);
        Assert.False(landed.SignedIn.PhishingResistant);
    }

    /// <summary>
    /// AUTH-FACT-003 AC4: a plain open of the link changes nothing, a press from the
    /// requesting browser signs in, and a press from another browser shows the code
    /// and signs nothing in.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_003_AC4_TheLinkCompletesOnAPressInOneBrowserOnlyAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailLink);
        Remembered(subject);

        (string challenge, string token, string browser) = await AskedAsync(subject);

        SignInLanding opened = await LandedAsync(challenge, browser, token, press: false);

        Assert.Null(opened.SignedIn);
        Assert.True(opened.SameBrowser);
        Assert.Null(opened.Code);

        SignInLanding away = await LandedAsync(challenge, browser: null, token, press: true);

        Assert.Null(away.SignedIn);
        Assert.False(away.SameBrowser);
        Assert.NotNull(away.Code);

        SignInLanding pressed = await LandedAsync(challenge, browser, token, press: true);

        Assert.NotNull(pressed.SignedIn);
        Assert.Equal(SignInStatus.Complete, pressed.SignedIn.Status);
    }

    /// <summary>
    /// AUTH-FACT-003 AC4: the code the link carried, typed where the sign-in began,
    /// completes it.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_003_AC4_TheCodeTypedWhereTheSignInBeganCompletesItAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailLink);
        Remembered(subject);

        (string challenge, string token, string browser) = await AskedAsync(subject);

        SignInLanding away = await LandedAsync(challenge, browser: null, token, press: true);

        Assert.NotNull(away.Code);

        Result<SignInProgress> typed = await PresentAsync(challenge, Factor.EmailLink, away.Code);

        Assert.Equal(SignInStatus.Complete, Reached(typed).Status);
    }

    /// <summary>
    /// AUTH-FACT-003 AC5: asking for a link on a channel whose factor the policy has
    /// not enabled answers as an identifier no account holds does, and sends nothing.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_003_AC5_ALinkOnADisabledChannelSendsNothingAndSaysSoAsync()
    {
        SubjectId subject = await AccountAsync();

        Result asked = await Service.SendLinkAsync(
            Address,
            Language,
            Source,
            browser: null,
            TestContext.Current.CancellationToken);

        Assert.True(asked.Match(() => true, _ => false));
        Assert.Null(await _pending.FindAsync(subject, Factor.EmailLink, TestContext.Current.CancellationToken));
        Assert.Empty(_mail.Taken);
    }

    /// <summary>
    /// INT-SMS-001 AC1: a code that proved control of a channel is no credential, so
    /// presenting it as one is refused; only the entry the sign-in issued completes it.
    /// </summary>
    [Fact]
    public async Task INT_SMS_001_AC1_NothingSentBySmsButTheSignInsOwnEntryIsAcceptedAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneLink);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Number);

        _ = await Service.SendLinkAsync(
            Number,
            Language,
            Source,
            browser: null,
            TestContext.Current.CancellationToken);

        string carried = _sms.Taken[^1].Text.Split(' ')[1];

        Assert.NotNull(await _pending.FindAsync(
            subject,
            Factor.PhoneLink,
            TestContext.Current.CancellationToken));

        // The value a text carried is the entry the sign-in issued and nothing else,
        // so it opens neither another entry nor another sign-in.
        Assert.NotNull(Refused(await PresentAsync(began.Challenge, Factor.PhoneCode, carried)));
        Assert.NotNull(Refused(await PresentAsync(
            (await BeganAsync(Number)).Challenge,
            Factor.PhoneLink,
            carried)));
    }

    /// <summary>
    /// INT-SMS-001 AC2: with the two SMS entries off in the policy, asking for a link
    /// at a number sends nothing and leaves no sign-in in flight.
    /// </summary>
    [Fact]
    public async Task INT_SMS_001_AC2_WithTheSmsEntriesOffNothingIsSentToANumberAsync()
    {
        SubjectId subject = await AccountAsync();

        Assert.True((await Service.SendLinkAsync(
                Number,
                Language,
                Source,
                browser: null,
                TestContext.Current.CancellationToken))
            .Match(() => true, _ => false));

        Assert.Empty(_sms.Taken);
        Assert.Null(await _pending.FindAsync(
            subject,
            Factor.PhoneLink,
            TestContext.Current.CancellationToken));

        Enables(Factor.PhoneLink);

        _ = await Service.SendLinkAsync(
            Number,
            Language,
            Source,
            browser: null,
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(_sms.Taken);
    }

    /// <summary>
    /// AUTH-FACT-015: a browser the account has trusted is not asked for the second
    /// step, and the session still records what was presented on it.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_015_ATrustedBrowserIsNotAskedForTheSecondStepAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Remembered(subject);

        OpaqueToken trusted = (await Devices
                .TrustAsync(subject, Browser, TestContext.Current.CancellationToken)
                .ConfigureAwait(true))
            .Match(token => token, error => throw new InvalidOperationException(error.Code.ToString()));

        await _work.CommitAsync(TestContext.Current.CancellationToken);

        SignInChallenge began = await BeganAsync(Address);

        Result<SignInOutcome> reached = await Service.PresentAsync(
            began.Challenge,
            new FactorPresentation(Factor.Password) { Value = Secret },
            new SessionOrigin(Source, Browser, null),
            remembered: null,
            trusted.Value,
            TestContext.Current.CancellationToken);

        SignInOutcome outcome = reached.Match(
            value => value,
            error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(SignInStatus.Complete, outcome.Progress.Status);
        Assert.Equal(AssuranceLevel.Aal1, outcome.Progress.AssuranceLevel);
    }

    /// <summary>
    /// AUTH-FACT-016 AC1: a password-only sign-in from a browser the account has not
    /// been seen on is held for a code, and no session is begun.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_016_AC1_AnUnseenBrowserHoldsAPasswordOnlySignInAsync()
    {
        await AccountAsync();

        SignInChallenge began = await BeganAsync(Address);
        SignInProgress reached = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));

        Assert.Equal(SignInStatus.DeviceVerificationRequired, reached.Status);
        Assert.Null(reached.Session);
        Assert.Single(_mail.Taken);
    }

    /// <summary>
    /// AUTH-FACT-016 AC2: the code sent to the primary address completes the held
    /// sign-in, and the session it begins records AAL1.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_016_AC2_TheEmailedCodeCompletesTheHeldSignInAsync()
    {
        await AccountAsync();

        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        Result<SignInProgress> completed = await Service.VerifyDeviceAsync(
            began.Challenge,
            Code(),
            Browser,
            location: null,
            Source,
            TestContext.Current.CancellationToken);

        SignInProgress reached = Reached(completed);

        Assert.Equal(SignInStatus.Complete, reached.Status);
        Assert.Equal(AssuranceLevel.Aal1, reached.AssuranceLevel);
        Assert.NotNull(reached.Session);
    }

    /// <summary>
    /// AUTH-FACT-016 AC3: a wrong code does not complete the sign-in, and after
    /// <c>code.verification.attempts</c> of them the right one is refused too.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_016_AC3_WrongCodesInvalidateTheHeldSignInAsync()
    {
        await AccountAsync();
        _configuration.Set(Settings.CodeVerificationAttempts, 2);

        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        string right = Code();

        for (int attempt = 0; attempt < 2; attempt++)
        {
            Result<SignInProgress> wrong = await Service.VerifyDeviceAsync(
                began.Challenge,
                "000000",
                Browser,
                location: null,
                Source,
                TestContext.Current.CancellationToken);

            Assert.NotNull(Refused(wrong));
        }

        Result<SignInProgress> spent = await Service.VerifyDeviceAsync(
            began.Challenge,
            right,
            Browser,
            location: null,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.CodeExpired, Refused(spent));
    }

    /// <summary>
    /// AUTH-FACT-017 AC1: with the run-up at zero, raising the required assurance
    /// stops a non-compliant sign-in at enrolment.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_017_AC1_WithNoGraceARaiseHoldsTheSignInAtEnrolmentAsync()
    {
        SubjectId subject = await AccountAsync();

        Remembered(subject);
        await RaisedAsync(AssuranceLevel.Aal2);

        SignInChallenge began = await BeganAsync(Address);
        Result<SignInProgress> stopped = await PresentAsync(began.Challenge, Factor.Password, Secret);

        Assert.Equal(ErrorCodes.PolicyGraceExpired, Refused(stopped));
    }

    /// <summary>
    /// AUTH-FACT-017 AC2: inside the run-up the sign-in completes and carries the
    /// requirement and the deadline; after it the sign-in stops at enrolment.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_017_AC2_InsideTheGraceTheSignInIsToldAndContinuesAsync()
    {
        SubjectId subject = await AccountAsync();

        Remembered(subject);
        _configuration.Set(Settings.PolicyEnforcementGrace, TimeSpan.FromDays(7));
        await RaisedAsync(AssuranceLevel.Aal2);

        SignInProgress inside = await SignedInAsync(subject, Factor.Password, Secret);

        Assert.Equal(SignInStatus.Complete, inside.Status);
        Assert.NotNull(inside.Requirement);
        Assert.Equal(PolicyField.RequiredAssurance, inside.Requirement.Field);
        Assert.Equal(Noon + TimeSpan.FromDays(7), inside.Requirement.Deadline);

        _clock.Advance(TimeSpan.FromDays(8));

        SignInChallenge began = await BeganAsync(Address);
        Result<SignInProgress> after = await PresentAsync(began.Challenge, Factor.Password, Secret);

        Assert.Equal(ErrorCodes.PolicyGraceExpired, Refused(after));
    }

    /// <summary>
    /// AUTH-FACT-017 AC3: an account created after the change is held at enrolment at
    /// its first sign-in, whatever run-up the deployment allows.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_017_AC3_AnAccountCreatedAfterTheChangeIsHeldAtOnceAsync()
    {
        _configuration.Set(Settings.PolicyEnforcementGrace, TimeSpan.FromDays(7));
        await RaisedAsync(AssuranceLevel.Aal2);

        _clock.Advance(TimeSpan.FromDays(1));

        SubjectId subject = await AccountAsync();

        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);
        Result<SignInProgress> stopped = await PresentAsync(began.Challenge, Factor.Password, Secret);

        Assert.Equal(ErrorCodes.PolicyGraceExpired, Refused(stopped));
    }

    /// <summary>
    /// AUTH-FACT-017 AC4: lowering a requirement leaves no run-up and holds nothing.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_017_AC4_LoweringARequirementHoldsNothingAsync()
    {
        SubjectId subject = await AccountAsync();

        Remembered(subject);
        await RaisedAsync(AssuranceLevel.Aal2);
        await LoweredAsync(AssuranceLevel.Aal2);

        SignInProgress reached = await SignedInAsync(subject, Factor.Password, Secret);

        Assert.Equal(SignInStatus.Complete, reached.Status);
        Assert.Null(reached.Requirement);
    }

    private async ValueTask<SubjectId> AccountAsync()
    {
        var subject = new SubjectId(Guid.NewGuid());

        _accounts.Stands(subject, AccountState.Active);
        _accounts.Registered(subject, _clock.GetUtcNow());
        _identifiers.Reads(subject, Language);

        IdentifierId email = _identifiers.Verified(subject, IdentifierKind.Email, Address);

        _ = _identifiers.Verified(subject, IdentifierKind.Phone, Number);

        await _identifiers.PromoteAsync(subject, email, TestContext.Current.CancellationToken);

        byte[] password = Encoding.UTF8.GetBytes(Secret);

        _ = await Passwords.SetAsync(
            subject,
            password,
            [],
            AssuranceLevel.Aal1,
            TestContext.Current.CancellationToken);

        await _work.CommitAsync(TestContext.Current.CancellationToken);

        return subject;
    }

    // The account has been seen on this browser, so the new-device check is not what
    // the sign-in under test is answering (AUTH-FACT-016).
    private void Remembered(SubjectId subject) =>
        _configuration.Set(Settings.DeviceVerificationEnabled, false);

    private void Enables(Factor factor) =>
        _configuration.Set(
            Settings.PolicyDefault,
            Janus.Core.Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>(
                    [.. Janus.Core.Policies.SystemDefault.LoginFactors, factor]),
            });

    private void Holds(SubjectId subject, Factor factor, bool isPreferred = false) =>
        _authenticators.Hold(Authenticator.Existing(
            AuthenticatorId.New(_clock),
            subject,
            factor,
            Label(factor),
            AuthenticatorState.Active,
            _clock.GetUtcNow(),
            null,
            null,
            confirmed: true,
            factor is Factor.Totp ? new TotpMaterial(new byte[20], null) : null,
            factor is Factor.SecurityKey
                ? new WebAuthnMaterial(new byte[] { 1 }, new byte[] { 2 }, -7, "example.test", 0, false, false)
                : null,
            isPreferred));

    private static CredentialLabel Label(Factor factor) =>
        CredentialLabel.TryParse(factor.ToString(), out CredentialLabel label)
            ? label
            : throw new InvalidOperationException("The catalogue entry is no label.");

    private async ValueTask RaisedAsync(AssuranceLevel required)
    {
        Policy after = Janus.Core.Policies.SystemDefault with { RequiredAssurance = required };

        _configuration.Set(Settings.PolicyDefault, after);

        _ = await Policies.RaisedAsync(
            null,
            Janus.Core.Policies.SystemDefault,
            after,
            _clock.GetUtcNow(),
            TestContext.Current.CancellationToken);
    }

    private async ValueTask LoweredAsync(AssuranceLevel was)
    {
        Policy before = Janus.Core.Policies.SystemDefault with { RequiredAssurance = was };

        _configuration.Set(Settings.PolicyDefault, Janus.Core.Policies.SystemDefault);

        _ = await Policies.RaisedAsync(
            null,
            before,
            Janus.Core.Policies.SystemDefault,
            _clock.GetUtcNow(),
            TestContext.Current.CancellationToken);
    }

    private async ValueTask<SignInChallenge> BeganAsync(string identifier) =>
        (await Service.BeginAsync(identifier, Source, TestContext.Current.CancellationToken))
        .Match(
            challenge => challenge,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private ValueTask<Result<SignInProgress>> PresentAsync(
        string challenge,
        Factor factor,
        string? value) =>
        Service.PresentAsync(
            challenge,
            new FactorPresentation(factor) { Value = value },
            Browser,
            location: null,
            Source,
            TestContext.Current.CancellationToken);

    private async ValueTask<SignInProgress> SignedInAsync(
        SubjectId subject,
        Factor factor,
        string value)
    {
        SignInChallenge began = await BeganAsync(Address);

        Assert.NotEqual(default, subject);

        return Reached(await PresentAsync(began.Challenge, factor, value));
    }

    private async ValueTask<(string Challenge, string Token, string Browser)> AskedAsync(
        SubjectId subject)
    {
        SignInChallenge began = await BeganAsync(Address);
        var browser = OpaqueToken.Draw(_randomness);

        Result asked = await Service.SendLinkAsync(
            Address,
            Language,
            Source,
            browser.Value,
            TestContext.Current.CancellationToken);

        Assert.True(asked.Match(() => true, _ => false));
        Assert.NotNull(await _pending.FindAsync(subject, Factor.EmailLink, TestContext.Current.CancellationToken));

        return (began.Challenge, Token(), browser.Value);
    }

    private async ValueTask<SignInLanding> PressedAsync(SubjectId subject)
    {
        (string challenge, string token, string browser) = await AskedAsync(subject);

        return await LandedAsync(challenge, browser, token, press: true);
    }

    private async ValueTask<SignInLanding> LandedAsync(
        string challenge,
        string? browser,
        string token,
        bool press) =>
        (await Service.LandAsync(
                challenge,
                browser,
                token,
                press,
                Browser,
                location: null,
                Source,
                TestContext.Current.CancellationToken))
        .Match(landing => landing, error => throw new InvalidOperationException(error.Code.ToString()));

    // The message body is the code and the link token in that order, so the test reads
    // what the person reads rather than what the store holds.
    private string Code() => _mail.Taken[^1].Body.Split(' ')[0];

    private string Token() => _mail.Taken[^1].Body.Split(' ')[1];

    private static SignInProgress Reached(Result<SignInProgress> outcome) =>
        outcome.Match(
            progress => progress,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private static ErrorCode? Refused(Result<SignInProgress> outcome) =>
        outcome.Match(_ => (ErrorCode?)null, error => error.Code);
}
