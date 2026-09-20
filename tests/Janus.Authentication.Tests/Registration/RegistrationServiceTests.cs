using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Sending;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Registration;

/// <summary>
/// The registration session and the six steps that run against it: what each one
/// admits, what the terms step writes, and what is left behind when nothing is
/// written at all (REG-SESS-001 to REG-SESS-008, REG-PROF-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class RegistrationServiceTests : IAsyncDisposable
{
    private const string Client = "web";
    private const string Language = "en";
    private const string Source = "198.51.100.7";
    private const string Address = "person@example.test";
    private const string Number = "+441632960011";
    private const string Mistyped = "+441632960012";
    private const string Chosen = "orangemarmaladeandtoast";
    private const string Terms = "terms-3";
    private const string Notice = "notice-2";
    private const string Short = "tenletters12";
    private const string Floor = "orangemarmalade";

    private static readonly DateTimeOffset Noon =
        new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Adult = new(1990, 1, 1);

    private static readonly DateOnly Minor = new(2015, 1, 1);

    private static readonly DeviceDescription Browser = new("Firefox", "Fedora");

    private static readonly Dictionary<string, bool> Unticked = [];

    private readonly RegistrationSessionStoreInMemory _sessions = new();
    private readonly RegistrationDirectoryInMemory _directory = new();
    private readonly SendLedgerInMemory _ledger = new();
    private readonly NoticeLedgerInMemory _notices = new();
    private readonly MessageTemplatesInMemory _templates = new();
    private readonly MailTransportInMemory _mail = new();
    private readonly SmsTransportInMemory _sms = new();
    private readonly SmsBalanceLedgerInMemory _balances = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly LeakedPasswordCorpusInMemory _corpus = new();
    private readonly WordListInMemory _words = new();
    private readonly ScreeningLogInMemory _screening = new();
    private readonly RecoveryCodeStoreInMemory _sets = new();
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly DeviceStoreInMemory _devices = new();
    private readonly SessionStoreInMemory _live = new();
    private readonly SessionAuditInMemory _audit = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment that has named the one key with no default, and a catalogue whose
    /// verification message carries exactly what the message carries in production:
    /// the code and the link token.
    /// </summary>
    public RegistrationServiceTests()
    {
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);

        foreach (SendKind kind in Enum.GetValues<SendKind>())
        {
            foreach (MessageKind message in new[] { MessageKind.VerificationCode, MessageKind.AccountExists })
            {
                _templates.Set(
                    message,
                    kind,
                    Language,
                    new MessageTemplate(kind is SendKind.Email ? "subject" : null, "{code} {token}"));
            }
        }
    }

    private RegistrationService Service =>
        new(
            _sessions,
            _directory,
            new SendingService(
                _configuration,
                _ledger,
                _templates,
                _mail,
                _sms,
                RestrictionKeySuppliers.None,
                new SmsBalance(_configuration, _sms, _balances, _work, _events, _clock),
                _work,
                _events,
                _clock,
                _randomness),
            _notices,
            new PasswordService(
                _passwords,
                new PasswordScreening(_corpus, _words, _configuration, _screening),
                new Argon2idHasher(_randomness),
                _configuration,
                _work,
                _clock),
            _passwords,
            new RecoveryCodeService(
                _sets,
                new Argon2idHasher(_randomness),
                _configuration,
                _work,
                _clock,
                _randomness),
            _sets,
            _authenticators,
            new PolicyResolution(_memberships, _configuration),
            new SessionService(
                _live,
                _audit,
                new PolicyResolution(_memberships, _configuration),
                _configuration,
                _gate,
                _work,
                _clock,
                _randomness),
            new DeviceService(_devices, _configuration, _work, _events, _clock, _randomness),
            _configuration,
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
    /// REG-SESS-001 AC1: abandoning after both identifiers are verified leaves no
    /// account and nothing reserved, so the same addresses start a fresh one.
    /// </summary>
    [Fact]
    public async Task REG_SESS_001_AC1_AbandoningAfterVerifyingLeavesNothingBehindAsync()
    {
        RegistrationSessionId session = await StagedAsync();

        Result abandoned = await Service.AbandonAsync(
            session,
            linkToken: null,
            TestContext.Current.CancellationToken);

        Assert.True(abandoned.Match(() => true, _ => false));
        Assert.Empty(_sessions.All);
        Assert.Empty(_directory.Created);

        Later();

        RegistrationSessionId again = await StagedAsync();

        Assert.True(Identity(again, IdentifierKind.Email).IsVerified);
        Assert.True(Identity(again, IdentifierKind.Phone).IsVerified);
    }

    /// <summary>
    /// REG-SESS-001 AC3: the sweep leaves no session older than the lifetime.
    /// </summary>
    [Fact]
    public async Task REG_SESS_001_AC3_NoSessionOutlivesTheLifetimeAsync()
    {
        _ = await StartedAsync();

        _clock.Advance(Settings.RegistrationSessionLifetime.Default + TimeSpan.FromMinutes(1));

        Assert.Equal(1, await Service.SweepAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_sessions.All);
    }

    /// <summary>
    /// REG-SESS-001 AC4: one account, one event, and no field by which the account
    /// could be created in any state but the one the directory gives it.
    /// </summary>
    [Fact]
    public async Task REG_SESS_001_AC4_OneEventFiresPerCreatedAccountAsync()
    {
        RegistrationSessionId session = await SecuredAsync();

        _ = Ok(await AcceptedAsync(session));

        Assert.Single(_directory.Created);
        Assert.Single(_events.Of<AccountRegistered>());
        Assert.DoesNotContain(
            typeof(NewAccount).GetProperties(),
            property => property.PropertyType == typeof(AccountState));
    }

    /// <summary>
    /// REG-SESS-002 AC1: a step is out of reach until the one before it is done, and
    /// a step that is done does not reopen.
    /// </summary>
    [Fact]
    public async Task REG_SESS_002_AC1_NoStepIsReachedOutOfOrderAsync()
    {
        RegistrationSessionId session = await StartedAsync();

        Assert.Equal(
            ErrorCodes.AffirmationRequired,
            Refused(await Service.StageAsync(
                session,
                IdentifierKind.Email,
                Address,
                TestContext.Current.CancellationToken)));

        _ = Ok(await Service.RecordAgeAsync(session, Adult, TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.RegistrationIncomplete,
            Refused(await Service.ConfirmAsync(session, TestContext.Current.CancellationToken)));

        Assert.Equal(
            ErrorCodes.RegistrationIncomplete,
            Refused(await Service.RecordAgeAsync(
                session,
                Adult,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// REG-SESS-002 AC2: at the terms step, with every earlier step complete, the
    /// session store is still the only place anything has been written.
    /// </summary>
    [Fact]
    public async Task REG_SESS_002_AC2_NothingIsWrittenOutsideTheSessionStoreAsync()
    {
        RegistrationSessionId session = await SecuredAsync();
        SubjectId provisional = Live(session).Provisional;

        Assert.Equal(RegistrationStep.Terms, Live(session).Step);
        Assert.Empty(_directory.Created);
        Assert.Empty(_authenticators.All);
        Assert.Empty(_devices.All);
        Assert.Empty(_live.All);
        Assert.Null(await _passwords.FindAsync(provisional, TestContext.Current.CancellationToken));
        Assert.Null(await _sets.FindAsync(provisional, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-SESS-002 AC3: the phone step is skippable where the policy calls the
    /// number optional, and is not where it calls it required.
    /// </summary>
    [Fact]
    public async Task REG_SESS_002_AC3_ThePhoneStepIsSkippableOnlyWhereItIsOptionalAsync()
    {
        RegistrationSessionId required = await AgedAsync();

        _ = Ok(await Service.StageAsync(
            required,
            IdentifierKind.Email,
            Address,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.RegistrationIncomplete,
            Refused(await Service.SkipPhoneAsync(required, TestContext.Current.CancellationToken)));

        _configuration.Set(Settings.RegistrationPhone, AttributeRequirement.Optional);

        RegistrationState skipped =
            Ok(await Service.SkipPhoneAsync(required, TestContext.Current.CancellationToken));

        Assert.Equal(RegistrationStep.Confirm, skipped.Step);
    }

    /// <summary>
    /// REG-SESS-006 AC1: with the default policy a password of fifteen characters
    /// stands alone and one of twelve leaves a second step outstanding.
    /// </summary>
    [Fact]
    public async Task REG_SESS_006_AC1_APasswordAtTheFloorStandsAloneAndOneBelowItDoesNotAsync()
    {
        RegistrationSessionId below = await ConfirmedAsync();

        _ = Ok(await Service.SetPasswordAsync(
            below,
            Short,
            TestContext.Current.CancellationToken));

        Assert.Equal(RegistrationStep.Security, Live(below).Step);
        Assert.Equal(
            ErrorCodes.RegistrationIncomplete,
            Refused(await AcceptedAsync(below)));

        Later();

        RegistrationSessionId atTheFloor = await ConfirmedAsync();

        _ = Ok(await Service.SetPasswordAsync(
            atTheFloor,
            Floor,
            TestContext.Current.CancellationToken));

        Assert.Equal(RegistrationStep.Terms, Live(atTheFloor).Step);
    }

    /// <summary>
    /// REG-SESS-006 AC1: a passkey alone completes the step, with no password on the
    /// account at all.
    /// </summary>
    [Fact]
    public async Task REG_SESS_006_AC1_APasskeyAloneCompletesTheStepAsync()
    {
        RegistrationSessionId session = await ConfirmedAsync();

        RegistrationState settled = Ok(await Service.EnrolAsync(
            session,
            Passkey(),
            TestContext.Current.CancellationToken));

        Assert.Equal(RegistrationStep.Terms, settled.Step);
        Assert.False(settled.Security.Password);
        Assert.Null(settled.Security.RecoveryCodes);
    }

    /// <summary>
    /// REG-SESS-006 AC2: a verified email completes the step alone where the policy
    /// admits the factor that rides it, and does not where it does not.
    /// </summary>
    [Fact]
    public async Task REG_SESS_006_AC2_AVerifiedEmailStandsAloneOnlyWhereTheFactorIsAdmittedAsync()
    {
        RegistrationSessionId closed = await ConfirmedAsync();

        Assert.Equal(ErrorCodes.RegistrationIncomplete, Refused(await AcceptedAsync(closed)));

        _configuration.Set(Settings.PolicyDefault, Admitting(Factor.EmailCode));

        Later();

        RegistrationSessionId open = await ConfirmedAsync();

        _ = Ok(await AcceptedAsync(open));

        Assert.Single(_directory.Created);
    }

    /// <summary>
    /// REG-SESS-006 AC3: lengthening the password on the same screen lifts the
    /// second step the short one made mandatory.
    /// </summary>
    [Fact]
    public async Task REG_SESS_006_AC3_LengtheningThePasswordLiftsTheSecondStepAsync()
    {
        RegistrationSessionId session = await ConfirmedAsync();

        _ = Ok(await Service.SetPasswordAsync(session, Short, TestContext.Current.CancellationToken));

        Assert.Equal(RegistrationStep.Security, Live(session).Step);

        _ = Ok(await Service.SetPasswordAsync(session, Floor, TestContext.Current.CancellationToken));

        Assert.Equal(RegistrationStep.Terms, Live(session).Step);
    }

    /// <summary>
    /// REG-SESS-006 AC4: a second step beside a password draws the recovery codes
    /// and shows them before the step is done with.
    /// </summary>
    [Fact]
    public async Task REG_SESS_006_AC4_ASecondStepBesideAPasswordDrawsRecoveryCodesAsync()
    {
        RegistrationSessionId session = await ConfirmedAsync();

        _ = Ok(await Service.SetPasswordAsync(session, Short, TestContext.Current.CancellationToken));

        RegistrationState settled = Ok(await Service.EnrolAsync(
            session,
            SecondStepCredential(),
            TestContext.Current.CancellationToken));

        Assert.Equal(RegistrationStep.Terms, settled.Step);
        Assert.NotNull(settled.Security.RecoveryCodes);
        Assert.NotEmpty(settled.Security.RecoveryCodes);
    }

    /// <summary>
    /// REG-SESS-003 AC1: a press in the browser that started the registration
    /// verifies; a plain open, which is what a scanner does, changes nothing.
    /// </summary>
    [Fact]
    public async Task REG_SESS_003_AC1_APressVerifiesAndAPlainOpenDoesNotAsync()
    {
        RegistrationSessionId session = await AwaitingAsync();
        string token = Token(IdentifierKind.Email);

        LinkLanding opened = Ok(await Service.LandAsync(
            session,
            token,
            press: false,
            TestContext.Current.CancellationToken));

        Assert.False(opened.Verified);
        Assert.False(Identity(session, IdentifierKind.Email).IsVerified);

        LinkLanding pressed = Ok(await Service.LandAsync(
            session,
            token,
            press: true,
            TestContext.Current.CancellationToken));

        Assert.True(pressed.Verified);
        Assert.True(Identity(session, IdentifierKind.Email).IsVerified);
    }

    /// <summary>
    /// REG-SESS-003 AC2: opened anywhere else the page shows the code and verifies
    /// nothing, and the control beside it ends the session and the code with it.
    /// </summary>
    [Fact]
    public async Task REG_SESS_003_AC2_ElsewhereTheLinkShowsTheCodeAndEndsTheSessionAsync()
    {
        RegistrationSessionId session = await AwaitingAsync();
        string token = Token(IdentifierKind.Email);
        string code = Code(session, IdentifierKind.Email);

        LinkLanding elsewhere = Ok(await Service.LandAsync(
            session: null,
            token,
            press: true,
            TestContext.Current.CancellationToken));

        Assert.False(elsewhere.Verified);
        Assert.False(elsewhere.SameBrowser);
        Assert.Equal(code, elsewhere.Code);
        Assert.False(Identity(session, IdentifierKind.Email).IsVerified);

        Result ended = await Service.AbandonAsync(
            session: null,
            token,
            TestContext.Current.CancellationToken);

        Assert.True(ended.Match(() => true, _ => false));
        Assert.Empty(_sessions.All);
        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.VerifyAsync(
                session,
                IdentifierId.New(_clock),
                code,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// REG-SESS-003 AC3: the code dies of wrong tries and takes the right one with
    /// it; a fresh one is drawn by entering the address again.
    /// </summary>
    [Fact]
    public async Task REG_SESS_003_AC3_TheSixthWrongCodeEndsTheCodeAsync()
    {
        RegistrationSessionId session = await AwaitingAsync();
        IdentifierId staged = Identity(session, IdentifierKind.Email).Id;
        string code = Code(session, IdentifierKind.Email);

        for (int attempt = 0; attempt < Settings.CodeVerificationAttempts.Default + 1; attempt++)
        {
            Assert.Equal(
                ErrorCodes.CodeInvalid,
                Refused(await Service.VerifyAsync(
                    session,
                    staged,
                    "000000",
                    TestContext.Current.CancellationToken)));
        }

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.VerifyAsync(
                session,
                staged,
                code,
                TestContext.Current.CancellationToken)));

        Later();

        _ = Ok(await Service.ChangeAsync(
            session,
            staged,
            Address,
            TestContext.Current.CancellationToken));

        _ = Ok(await Service.VerifyAsync(
            session,
            staged,
            Code(session, IdentifierKind.Email),
            TestContext.Current.CancellationToken));

        Assert.True(Identity(session, IdentifierKind.Email).IsVerified);
    }

    /// <summary>
    /// REG-SESS-003 AC4: the state the waiting screen reads carries the verification
    /// the press made, with nothing else asked of the browser.
    /// </summary>
    [Fact]
    public async Task REG_SESS_003_AC4_TheStateCarriesAVerificationMadeByLinkAsync()
    {
        RegistrationSessionId session = await AwaitingAsync();

        _ = Ok(await Service.LandAsync(
            session,
            Token(IdentifierKind.Email),
            press: true,
            TestContext.Current.CancellationToken));

        RegistrationState watched =
            Ok(await Service.StateAsync(session, TestContext.Current.CancellationToken));

        Assert.True(watched.Identifiers.Single(staged =>
            staged.Kind is IdentifierKind.Email).Verified);
    }

    /// <summary>
    /// REG-SESS-004 AC1: the step waits for every identifier on the screen, and
    /// dropping an unverified extra lets the rest through.
    /// </summary>
    [Fact]
    public async Task REG_SESS_004_AC1_AnUnverifiedExtraHoldsTheStepUntilItIsDroppedAsync()
    {
        RegistrationSessionId session = await StagedAsync();

        Later();

        _ = Ok(await Service.AddAsync(
            session,
            IdentifierKind.Email,
            "second@example.test",
            TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.RegistrationIncomplete,
            Refused(await Service.ConfirmAsync(session, TestContext.Current.CancellationToken)));

        _ = Ok(await Service.DiscardAsync(
            session,
            Identity(session, IdentifierKind.Email).Id,
            TestContext.Current.CancellationToken));

        _ = Ok(await Service.ConfirmAsync(session, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-SESS-004 AC2: a corrected number is the one sent to, and the number it
    /// replaced keeps the one message it was sent and no more.
    /// </summary>
    [Fact]
    public async Task REG_SESS_004_AC2_ACorrectedNumberIsTheOneSentToAsync()
    {
        RegistrationSessionId session = await AwaitingAsync();

        await VerifiedAsync(session, IdentifierKind.Email);

        _ = Ok(await Service.StageAsync(
            session,
            IdentifierKind.Phone,
            Mistyped,
            TestContext.Current.CancellationToken));

        _ = Ok(await Service.ChangeAsync(
            session,
            Identity(session, IdentifierKind.Phone).Id,
            Number,
            TestContext.Current.CancellationToken));

        Assert.Equal(Number, _sms.Taken[^1].Destination.Value);
        Assert.Equal(1, _sms.Taken.Count(message => message.Destination.Value == Mistyped));
    }

    /// <summary>
    /// REG-SESS-005 AC1: the state after staging an address another account holds is
    /// the state after staging one nobody holds, field for field, and no code is
    /// drawn for it.
    /// </summary>
    [Fact]
    public async Task REG_SESS_005_AC1_ADuplicateAnswersExactlyAsAFreshAddressDoesAsync()
    {
        // Both sessions are opened at the same instant, so that the wait the shipped
        // restriction imposes between two messages to one address does not move the
        // expiry the state carries.
        RegistrationSessionId fresh = await AgedAsync();
        RegistrationSessionId duplicate = await AgedAsync();

        RegistrationState first = Ok(await Service.StageAsync(
            fresh,
            IdentifierKind.Email,
            Address,
            TestContext.Current.CancellationToken));

        _directory.Held(IdentifierKind.Email, Address, SubjectId.New(_randomness));

        Later();

        RegistrationState second = Ok(await Service.StageAsync(
            duplicate,
            IdentifierKind.Email,
            Address,
            TestContext.Current.CancellationToken));

        Assert.Equal(first.Step, second.Step);
        Assert.Equal(first.ExpiresAt, second.ExpiresAt);
        Assert.Equal(first.Security.Password, second.Security.Password);
        Assert.Equal(first.Security.SecondStep, second.Security.SecondStep);
        Assert.Equal(first.Security.RecoveryCodes, second.Security.RecoveryCodes);

        StagedIdentifier one = Assert.Single(first.Identifiers);
        StagedIdentifier two = Assert.Single(second.Identifiers);

        Assert.Equal(one with { Id = two.Id }, two);
        Assert.Null(Identity(duplicate, IdentifierKind.Email).Code);
        Assert.Null(Identity(duplicate, IdentifierKind.Email).Link);
    }

    /// <summary>
    /// REG-SESS-005 AC2: what reaches the holder carries neither the code nor the
    /// link, which is what the unfilled placeholders show.
    /// </summary>
    [Fact]
    public async Task REG_SESS_005_AC2_TheHoldersNoticeCarriesNoCodeAndNoLinkAsync()
    {
        _directory.Held(IdentifierKind.Email, Address, SubjectId.New(_randomness));

        RegistrationSessionId session = await AgedAsync();

        _ = Ok(await Service.StageAsync(
            session,
            IdentifierKind.Email,
            Address,
            TestContext.Current.CancellationToken));

        Assert.Equal("{code} {token}", Assert.Single(_mail.Taken).Body);
    }

    /// <summary>
    /// REG-SESS-005 AC3: nothing the session can do completes it, and it goes the
    /// way an abandoned one goes.
    /// </summary>
    [Fact]
    public async Task REG_SESS_005_AC3_TheDuplicateSessionExpiresWithoutAnAccountAsync()
    {
        _directory.Held(IdentifierKind.Email, Address, SubjectId.New(_randomness));

        RegistrationSessionId session = await AgedAsync();

        _ = Ok(await Service.StageAsync(
            session,
            IdentifierKind.Email,
            Address,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.VerifyAsync(
                session,
                Identity(session, IdentifierKind.Email).Id,
                "000000",
                TestContext.Current.CancellationToken)));

        _clock.Advance(Settings.RegistrationSessionLifetime.Default + TimeSpan.FromMinutes(1));

        Assert.Equal(1, await Service.SweepAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_directory.Created);
    }

    /// <summary>
    /// REG-SESS-007 AC1: a consent control left as it was drawn holds nothing up.
    /// </summary>
    [Fact]
    public async Task REG_SESS_007_AC1_EveryConsentLeftUntickedCompletesTheRegistrationAsync()
    {
        RegistrationSessionId session = await SecuredAsync();

        Dictionary<string, bool> untouched = new(StringComparer.Ordinal)
        {
            ["analytics"] = false,
            ["marketing"] = false,
        };

        RegistrationCompleted completed = Ok(await Service.AcceptTermsAsync(
            session,
            Terms,
            Notice,
            untouched,
            Browser,
            location: null,
            TestContext.Current.CancellationToken));

        Assert.Equal(Assert.Single(_directory.Created).Subject, completed.Subject);
    }

    /// <summary>
    /// REG-SESS-007 AC2: what was accepted and what was presented are on the account
    /// the step creates.
    /// </summary>
    [Fact]
    public async Task REG_SESS_007_AC2_TheAccountCarriesTheVersionsAcceptedAndPresentedAsync()
    {
        RegistrationSessionId session = await SecuredAsync();

        _ = Ok(await AcceptedAsync(session));

        NewAccount created = Assert.Single(_directory.Created);

        Assert.Equal(Terms, created.TermsVersion);
        Assert.Equal(Notice, created.NoticeVersion);
        Assert.Equal(Noon, created.AnsweredAgeAt);
    }

    /// <summary>
    /// REG-SESS-007 AC3: the session carries what the enrolled methods support and
    /// not a level above them.
    /// </summary>
    [Fact]
    public async Task REG_SESS_007_AC3_TheSessionCarriesWhatTheMethodsSupportAsync()
    {
        RegistrationSessionId password = await SecuredAsync();

        _ = Ok(await AcceptedAsync(password));

        Session signedIn = Assert.Single(_live.All);

        Assert.Equal(AssuranceLevel.Aal1, signedIn.Attained);
        Assert.False(signedIn.PhishingResistant);
    }

    /// <summary>
    /// REG-SESS-007 AC3: a passkey carries its own level and its own resistance,
    /// which is what the same path reads from the catalogue.
    /// </summary>
    [Fact]
    public async Task REG_SESS_007_AC3_APasskeyCarriesTheLevelItSupportsAsync()
    {
        RegistrationSessionId session = await ConfirmedAsync();

        _ = Ok(await Service.EnrolAsync(
            session,
            Passkey(),
            TestContext.Current.CancellationToken));

        _ = Ok(await AcceptedAsync(session));

        Session signedIn = Assert.Single(_live.All);

        Assert.Equal(AssuranceLevel.Aal2, signedIn.Attained);
        Assert.True(signedIn.PhishingResistant);
    }

    /// <summary>
    /// REG-SESS-007 AC4: the browser that registered is not asked for a new-device
    /// code at the next sign-in, and one that did not register is.
    /// </summary>
    [Fact]
    public async Task REG_SESS_007_AC4_TheRegisteringBrowserIsNotHeldForACodeAsync()
    {
        RegistrationSessionId session = await SecuredAsync();

        RegistrationOutcome outcome = Ok(await Service.CompleteAsync(
            session,
            Terms,
            Notice,
            Unticked,
            Browser,
            location: null,
            TestContext.Current.CancellationToken));

        var devices = new DeviceService(
            _devices,
            _configuration,
            _work,
            _events,
            _clock,
            _randomness);

        var single = new Assurance(AssuranceLevel.Aal1, PhishingResistant: false);

        Assert.False(Ok(await devices.ChecksAsync(
            outcome.Subject,
            single,
            single,
            outcome.Browser.Value,
            TestContext.Current.CancellationToken)));

        Assert.True(Ok(await devices.ChecksAsync(
            outcome.Subject,
            single,
            single,
            browser: null,
            TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// REG-SESS-008 AC1: no step of the contract takes a destination, so there is
    /// nothing for a link to smuggle one into.
    /// </summary>
    [Fact]
    public void REG_SESS_008_AC1_NoStepTakesADestination()
    {
        string[] destinations =
            ["DESTINATION", "REDIRECT", "REDIRECTURI", "RETURNTO", "RETURNURL", "NEXT", "URL"];

        IEnumerable<string> taken = typeof(IRegistration)
            .GetMethods()
            .SelectMany(step => step.GetParameters())
            .Select(parameter => parameter.Name ?? string.Empty);

        Assert.All(
            taken,
            name => Assert.DoesNotContain(
                name.ToUpperInvariant(),
                destinations,
                StringComparer.Ordinal));
    }

    /// <summary>
    /// REG-SESS-008 AC2: the client captured at the first request is the one the
    /// session carries to the end, and the end is a session the person holds.
    /// </summary>
    [Fact]
    public async Task REG_SESS_008_AC2_TheReturnIsDecidedByTheClientCapturedAtTheStartAsync()
    {
        RegistrationSessionId session = await SecuredAsync();

        Assert.Equal(Client, Live(session).Client);

        RegistrationCompleted completed = Ok(await AcceptedAsync(session));

        Session signedIn = Assert.Single(_live.All);

        Assert.Equal(completed.Subject, signedIn.Subject);
        Assert.Equal(completed.Session, signedIn.Id);
    }

    /// <summary>
    /// REG-PROF-002 AC1: the age screen comes first, and an identifier offered
    /// before it is answered is not taken.
    /// </summary>
    [Fact]
    public async Task REG_PROF_002_AC1_NoIdentifierIsTakenBeforeTheAgeScreenAsync()
    {
        RegistrationSessionId session = await StartedAsync();

        Assert.Equal(
            ErrorCodes.AffirmationRequired,
            Refused(await Service.StageAsync(
                session,
                IdentifierKind.Email,
                Address,
                TestContext.Current.CancellationToken)));

        Assert.Empty(Live(session).Identifiers);
        Assert.Empty(_mail.Taken);
    }

    /// <summary>
    /// REG-PROF-002 AC2: on an adults-only host an under-age date ends the session,
    /// and the screen does not take a second date.
    /// </summary>
    [Fact]
    public async Task REG_PROF_002_AC2_AnUnderAgeDateEndsTheSessionAndLocksTheScreenAsync()
    {
        _configuration.Set(Settings.RegistrationAdultAffirmation, AttributeRequirement.Required);

        RegistrationSessionId session = await StartedAsync();

        Assert.Equal(
            ErrorCodes.ProfileUnderage,
            Refused(await Service.RecordAgeAsync(
                session,
                Minor,
                TestContext.Current.CancellationToken)));

        Assert.Equal(
            ErrorCodes.ProfileUnderage,
            Refused(await Service.RecordAgeAsync(
                session,
                Adult,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// REG-PROF-002 AC3: the account carries the affirmation and the instant it was
    /// derived; the date itself only where the deployment keeps it.
    /// </summary>
    [Fact]
    public async Task REG_PROF_002_AC3_TheAccountCarriesTheAffirmationAndNotTheDateAsync()
    {
        RegistrationSessionId withheld = await SecuredAsync();

        _ = Ok(await AcceptedAsync(withheld));

        NewAccount created = Assert.Single(_directory.Created);

        Assert.True(created.AdultAffirmed);
        Assert.Equal(Noon, created.AnsweredAgeAt);
        Assert.Null(created.DateOfBirth);
    }

    /// <summary>
    /// REG-PROF-002 AC3: with the date kept, the date is on the account beside the
    /// affirmation.
    /// </summary>
    [Fact]
    public async Task REG_PROF_002_AC3_TheDateIsKeptWhereTheDeploymentKeepsItAsync()
    {
        _configuration.Set(Settings.ProfileDateOfBirth, AttributeRequirement.Optional);

        RegistrationSessionId session = await SecuredAsync();

        _ = Ok(await AcceptedAsync(session));

        Assert.Equal(Adult, Assert.Single(_directory.Created).DateOfBirth);
    }

    /// <summary>
    /// REG-PROF-002 AC4: with the affirmation off the screen records the band and
    /// affirms nothing.
    /// </summary>
    [Fact]
    public async Task REG_PROF_002_AC4_WithTheAffirmationOffTheBandIsRecordedAsync()
    {
        _configuration.Set(Settings.RegistrationAdultAffirmation, AttributeRequirement.Off);

        RegistrationSessionId session = await SecuredAsync();

        _ = Ok(await AcceptedAsync(session));

        NewAccount created = Assert.Single(_directory.Created);

        Assert.Null(created.AdultAffirmed);
        Assert.Equal(AgeGroup.Adult, created.Group);
    }


    /// <summary>
    /// REG-IDENT-009 AC1: the username is chosen later, so a registration that never
    /// names one completes and the account holds none.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_009_AC1_RegistrationCompletesWithNoUsernameAsync()
    {
        _configuration.Set(Settings.IdentifiersUsernameEnabled, value: true);

        RegistrationSessionId session = await SecuredAsync();

        _ = Ok(await AcceptedAsync(session));

        Assert.DoesNotContain(
            Assert.Single(_directory.Created).Identifiers,
            identifier => identifier.Kind is IdentifierKind.Username);
    }

    /// <summary>
    /// REG-IDENT-010 AC1: a locked identifier is marked as such on the confirm step
    /// and refuses the change the screen therefore does not offer.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_010_AC1_TheConfirmStepOffersNoChangeOnALockedIdentifierAsync()
    {
        RegistrationSessionId session = await AgedAsync();

        Locked(session, Address);

        StagedIdentity staged = Identity(session, IdentifierKind.Email);

        RegistrationState state = Ok(await Service.StateAsync(
            session,
            TestContext.Current.CancellationToken));

        Assert.True(Assert.Single(state.Identifiers).Locked);

        Assert.Equal(
            ErrorCodes.IdentifierLocked,
            Refused(await Service.ChangeAsync(
                session,
                staged.Id,
                "other@example.test",
                TestContext.Current.CancellationToken)));

        Assert.Equal(Address, Identity(session, IdentifierKind.Email).Canonical);
    }

    /// <summary>
    /// REG-IDENT-010 AC2: an unlocked identifier changes, and what the old value
    /// proved does not carry over to the new one.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_010_AC2_ChangingAnIdentifierResetsItsVerificationAsync()
    {
        RegistrationSessionId session = await StagedAsync(phone: false);

        StagedIdentity staged = Identity(session, IdentifierKind.Email);

        Assert.True(staged.IsVerified);

        _ = Ok(await Service.ChangeAsync(
            session,
            staged.Id,
            "other@example.test",
            TestContext.Current.CancellationToken));

        StagedIdentity changed = Identity(session, IdentifierKind.Email);

        Assert.Equal("other@example.test", changed.Canonical);
        Assert.False(changed.IsVerified);
    }

    // The steps a test is not about, run the way a browser runs them, so that each
    // test says only what it is checking.
    private async Task<RegistrationSessionId> StartedAsync()
    {
        Result<RegistrationSessionId> begun = await Service
            .BeginAsync(Client, Language, Source, TestContext.Current.CancellationToken);

        return begun.Match(session => session, Throw<RegistrationSessionId>);
    }

    private async Task<RegistrationSessionId> AgedAsync()
    {
        RegistrationSessionId session = await StartedAsync();

        _ = Ok(await Service.RecordAgeAsync(session, Adult, TestContext.Current.CancellationToken));

        return session;
    }

    private async Task<RegistrationSessionId> AwaitingAsync()
    {
        RegistrationSessionId session = await AgedAsync();

        _ = Ok(await Service.StageAsync(
            session,
            IdentifierKind.Email,
            Address,
            TestContext.Current.CancellationToken));

        return session;
    }

    private async Task<RegistrationSessionId> StagedAsync(bool phone = true)
    {
        RegistrationSessionId session = await AwaitingAsync();

        await VerifiedAsync(session, IdentifierKind.Email);

        if (!phone)
        {
            return session;
        }

        _ = Ok(await Service.StageAsync(
            session,
            IdentifierKind.Phone,
            Number,
            TestContext.Current.CancellationToken));

        await VerifiedAsync(session, IdentifierKind.Phone);

        return session;
    }

    private async Task<RegistrationSessionId> ConfirmedAsync(bool phone = true)
    {
        RegistrationSessionId session = await StagedAsync(phone);

        _ = Ok(await Service.ConfirmAsync(session, TestContext.Current.CancellationToken));

        return session;
    }

    private async Task<RegistrationSessionId> SecuredAsync(string password = Chosen)
    {
        RegistrationSessionId session = await ConfirmedAsync();

        _ = Ok(await Service.SetPasswordAsync(
            session,
            password,
            TestContext.Current.CancellationToken));

        return session;
    }

    private async Task VerifiedAsync(RegistrationSessionId session, IdentifierKind kind)
    {
        StagedIdentity staged = Identity(session, kind);

        _ = Ok(await Service.VerifyAsync(
            session,
            staged.Id,
            VerificationCode.Read(staged.Code!),
            TestContext.Current.CancellationToken));
    }

    private async Task<Result<RegistrationCompleted>> AcceptedAsync(RegistrationSessionId session) =>
        await Service.AcceptTermsAsync(
            session,
            Terms,
            Notice,
            Unticked,
            Browser,
            location: null,
            TestContext.Current.CancellationToken);

    // What a bound invitation leaves on the session: an address the person did not
    // type and therefore does not change (REG-IDENT-010).
    private void Locked(RegistrationSessionId session, string address)
    {
        Live(session).Stage(StagedIdentity.Of(
            IdentifierId.New(_clock),
            IdentifierKind.Email,
            address,
            address,
            isLocked: true));
    }

    private RegistrationSession Live(RegistrationSessionId session) =>
        _sessions.All.Single(held => held.Id == session);

    private string Code(RegistrationSessionId session, IdentifierKind kind) =>
        VerificationCode.Read(Identity(session, kind).Code!);

    private StagedIdentity Identity(RegistrationSessionId session, IdentifierKind kind) =>
        Live(session).Identifiers.Last(staged => staged.Kind == kind);

    // The link token never touches the session: it is in the message, as it is for
    // the person reading it.
    private string Token(IdentifierKind kind) =>
        (kind is IdentifierKind.Email ? _mail.Taken[^1].Body : _sms.Taken[^1].Text).Split(' ')[1];

    // What a completed WebAuthn ceremony stages, with the material a test does not
    // care about drawn once.
    private StagedCredential Passkey() =>
        new(
            AuthenticatorId.New(_clock),
            Factor.Passkey,
            Label("this phone"),
            Totp: null,
            new WebAuthnMaterial(
                Secret(),
                Secret(),
                Algorithm: -7,
                "example.test",
                Counter: null,
                BackupEligible: true,
                BackupState: true));

    private StagedCredential SecondStepCredential() =>
        new(
            AuthenticatorId.New(_clock),
            Factor.Totp,
            Label("authenticator"),
            new TotpMaterial(Secret(), ConsumedStep: null),
            WebAuthn: null);

    private byte[] Secret()
    {
        byte[] drawn = new byte[32];

        _randomness.GetBytes(drawn);

        return drawn;
    }

    private static Policy Admitting(Factor extra) =>
        Core.Policies.SystemDefault with
        {
            LoginFactors = Core.Policies.SystemDefault.LoginFactors.Append(extra).ToFrozenSet(),
        };

    private static CredentialLabel Label(string written) =>
        CredentialLabel.TryParse(written, out CredentialLabel label)
            ? label
            : throw new Xunit.Sdk.XunitException(written);

    // The shipped restriction lets one message a minute reach an address, so a test
    // that registers twice waits as a person would (AUTH-ABUSE-004).
    private void Later() => _clock.Advance(TimeSpan.FromMinutes(2));

    private static TValue Ok<TValue>(Result<TValue> outcome) =>
        outcome.Match(value => value, Throw<TValue>);

    private static ErrorCode Refused<TValue>(Result<TValue> outcome) =>
        outcome.Match(_ => throw new Xunit.Sdk.XunitException("The step was admitted."), error => error.Code);

    private static TValue Throw<TValue>(Error error) =>
        throw new Xunit.Sdk.XunitException(error.Code.ToString());
}
