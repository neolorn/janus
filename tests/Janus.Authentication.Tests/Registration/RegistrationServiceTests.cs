using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Invitations;
using Janus.Authentication.Organizations;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Invitations;
using Janus.Authentication.Tests.Oidc;
using Janus.Authentication.Tests.Organizations;
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
/// written at all (REG-SESS-001 to REG-SESS-008, REG-PROF-002), and a registration an
/// invitation opens (REG-INV-001, REG-MAIL-001, REG-DOM-001, IDN-LIFE-009a).
/// </summary>
[Trait("kind", "unit")]
public sealed class RegistrationServiceTests : IAsyncDisposable
{
    private const string Client = "web";
    private const string Registered = "https://app.example.test/welcome";
    private const string Language = "en";

    private static readonly string[] Arabic = ["ar"];
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
    private readonly NoticeLedgerInMemory _notices = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly LeakedPasswordCorpusInMemory _corpus = new();
    private readonly WordListInMemory _words = new();
    private readonly ScreeningLogInMemory _screening = new();
    private readonly RecoveryCodeStoreInMemory _sets = new();
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly OidcClientStoreInMemory _clients = new();
    private readonly DeviceStoreInMemory _devices = new();
    private readonly ConsentsInMemory _consents = new();
    private readonly SessionStoreInMemory _live = new();
    private readonly SessionAuditInMemory _audit = new();
    private readonly CredentialAuditInMemory _credentials = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly InvitationStoreInMemory _invitations = new();
    private readonly DomainStoreInMemory _domains = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly LocationResolverInMemory _locations = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly NotificationHandlerInMemory _notifications = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment that has named the keys with no default: the gateway's balance
    /// floor and the languages it writes in.
    /// </summary>
    public RegistrationServiceTests()
    {
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);
        _configuration.Set(Settings.NotificationLanguages, [Language, "ar"]);
    }

    private RegistrationService Service =>
        new(
            _sessions,
            _directory,
            _notifications,
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
            _clients,
            new PolicyResolution(_memberships, _configuration, _raises),
            _invitations,
            new DomainLock(_memberships, _configuration, _domains),
            new SessionService(
                _live,
                _audit,
                _authenticators,
                _credentials,
                new PolicyResolution(_memberships, _configuration, _raises),
                _configuration,
                new AdministrativeScope(_gate, _administrative),
                _locations,
                _work,
                _clock,
                _randomness),
            new DeviceService(_devices, _configuration, _work, _events, _clock, _randomness),
            _consents,
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

        Assert.Equal(Number, _notifications.Texts[^1].Destination.Canonical);
        Assert.Equal(1, _notifications.Texts.Count(sent => sent.Destination.Canonical == Mistyped));
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
    /// link, so the deployment's template has nothing to put either in.
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

        Assert.Empty(Assert.Single(_notifications.Mail).Values);
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
            TestContext.Current.CancellationToken));

        Assert.Equal(Assert.Single(_directory.Created).Subject, completed.Subject);
    }

    /// <summary>
    /// PRIV-CONS-003 AC1, AC2: nothing is ticked for the person, so a step submitted
    /// with every control as it was drawn records no consent, and a purpose the
    /// person was never shown a control for records none either.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_003_AC1_NoConsentIsRecordedForAControlLeftUntickedAsync()
    {
        _consents.Take("marketing");
        _consents.Take("analytics");

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
            TestContext.Current.CancellationToken));

        Assert.Empty(_consents.Of(completed.Subject));
    }

    /// <summary>
    /// PRIV-CONS-001 AC1, PRIV-CONS-002 AC1: each control the person ticked is its
    /// own record naming its own purpose, and the mechanism says where it was given.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_002_AC1_EachTickedControlIsItsOwnRecordAsync()
    {
        _consents.Take("marketing");
        _consents.Take("analytics");

        RegistrationSessionId session = await SecuredAsync();

        Dictionary<string, bool> ticked = new(StringComparer.Ordinal)
        {
            ["analytics"] = true,
            ["marketing"] = true,
        };

        RegistrationCompleted completed = Ok(await Service.AcceptTermsAsync(
            session,
            Terms,
            Notice,
            ticked,
            Browser,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            ["analytics", "marketing"],
            _consents.Of(completed.Subject).Select(record => record.Purpose));
        Assert.All(
            _consents.Of(completed.Subject),
            record => Assert.Equal(ConsentMechanism.Registration, record.Mechanism));
    }

    /// <summary>
    /// PRIV-CONS-001 AC1: a record names a purpose the deployment takes consent for,
    /// so a control naming anything else is a request that should not have been made
    /// and the step refuses rather than recording a consent to nothing.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_001_AC1_AControlForAPurposeTakingNoConsentIsRefusedAsync()
    {
        RegistrationSessionId session = await SecuredAsync();

        Dictionary<string, bool> ticked = new(StringComparer.Ordinal)
        {
            ["performance"] = true,
        };

        Result<RegistrationCompleted> refused = await Service.AcceptTermsAsync(
            session,
            Terms,
            Notice,
            ticked,
            Browser,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.Denied, Refused(refused));
        Assert.Equal(0, _consents.Recorded);
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
    /// PRIV-CONS-008a AC2: what is recorded of the privacy notice is that a version
    /// was presented at a time. The account carries the version and the instant, and
    /// no field of what the step writes records an acceptance of it; the terms, which
    /// are a contract, are the only thing accepted.
    /// </summary>
    [Fact]
    public async Task PRIV_CONS_008a_AC2_ThePresentationRecordCarriesTheVersionAndTheInstantAsync()
    {
        RegistrationSessionId session = await SecuredAsync();

        _ = Ok(await AcceptedAsync(session));

        NewAccount created = Assert.Single(_directory.Created);

        Assert.Equal(Notice, created.NoticeVersion);
        Assert.Equal(Noon, created.CreatedAt);

        Assert.DoesNotContain(
            typeof(NewAccount).GetProperties(),
            property => property.Name.Contains("NoticeAccepted", StringComparison.Ordinal)
                || property.Name.Contains("AcceptedNotice", StringComparison.Ordinal));
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
    /// REG-SESS-008 AC2 and API-REDIR-002 AC1, AC4: the client captured at the first
    /// request is the one the session carries to the end, the end is a session the
    /// person holds, and where they are returned is the address the registry holds
    /// for that client.
    /// </summary>
    [Fact]
    public async Task REG_SESS_008_AC2_TheReturnIsDecidedByTheClientCapturedAtTheStartAsync()
    {
        await RegisteredAsync();

        RegistrationSessionId session = await SecuredAsync();

        Assert.Equal(Client, Live(session).Client);

        RegistrationCompleted completed = Ok(await AcceptedAsync(session));

        Session signedIn = Assert.Single(_live.All);

        Assert.Equal(completed.Subject, signedIn.Subject);
        Assert.Equal(completed.Session, signedIn.Id);
        Assert.Equal(Registered, completed.Landing);
    }

    /// <summary>
    /// API-REDIR-002 AC1: the identifier is resolved against the registry where it is
    /// captured, so the session carries a client the deployment registered and not a
    /// string the request chose.
    /// </summary>
    [Fact]
    public async Task API_REDIR_002_AC1_TheIdentifierIsResolvedWhereItIsCapturedAsync()
    {
        await RegisteredAsync();

        Assert.Equal(Client, Live(await StartedAsync()).Client);
    }

    /// <summary>
    /// API-REDIR-002 AC2: an identifier the registry does not hold registers a person
    /// exactly as a registered one does, and a deployment that named no default has
    /// nothing to fall back to, so the return is left to the frontend.
    /// </summary>
    [Fact]
    public async Task API_REDIR_002_AC2_AnUnrecognisedIdentifierIsTheDefaultAndNoRefusalAsync()
    {
        RegistrationSessionId session = await SecuredAsync();

        Assert.Empty(Live(session).Client);
        Assert.Empty(Ok(await AcceptedAsync(session)).Landing);
    }

    /// <summary>
    /// API-REDIR-002 AC2, API-REDIR-001: where the deployment named a default client,
    /// an identifier the registry does not hold is stored as that client at capture,
    /// and the return is the one the registry holds for it.
    /// </summary>
    [Fact]
    public async Task API_REDIR_002_AC2_AnUnrecognisedIdentifierIsStoredAsTheNamedDefaultAsync()
    {
        await _clients.RecordAsync(
            new OidcClient(
                "fallback",
                "fallback",
                OidcClientKind.BrowserApplication,
                "https://fallback.example.test/welcome",
                ["openid"]),
            [7, 8, 9],
            TestContext.Current.CancellationToken);

        _configuration.Set(Settings.RedirectDefaultClient, "fallback");

        RegistrationSessionId session = await SecuredAsync();

        Assert.Equal("fallback", Live(session).Client);
        Assert.Equal("https://fallback.example.test/welcome", Ok(await AcceptedAsync(session)).Landing);
    }

    /// <summary>
    /// API-REDIR-002 AC4: the return is resolved from the client the session stored,
    /// so a deployment holding several clients returns the person to the one that
    /// began the registration.
    /// </summary>
    [Fact]
    public async Task API_REDIR_002_AC4_TheReturnIsTheStoredClientsAndNoOthersAsync()
    {
        await RegisteredAsync();
        await _clients.RecordAsync(
            new OidcClient(
                "another",
                "another",
                OidcClientKind.BrowserApplication,
                "https://elsewhere.example.test/welcome",
                ["openid"]),
            [4, 5, 6],
            TestContext.Current.CancellationToken);

        Assert.Equal(Registered, Ok(await AcceptedAsync(await SecuredAsync())).Landing);
    }

    /// <summary>
    /// API-REDIR-002 AC3: no step after the first takes a destination, so there is
    /// nothing at the end to validate and nothing a later request could replace.
    /// </summary>
    [Fact]
    public void API_REDIR_002_AC3_NoLaterStepTakesADestination() =>
        Assert.DoesNotContain(
            typeof(IRegistration).GetMethods().SelectMany(method => method.GetParameters()),
            parameter => parameter.Name is not null
                && (parameter.Name.Contains("redirect", StringComparison.OrdinalIgnoreCase)
                    || parameter.Name.Contains("destination", StringComparison.OrdinalIgnoreCase)
                    || parameter.Name.Contains("return", StringComparison.OrdinalIgnoreCase)));

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
        Assert.Empty(_notifications.Mail);
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
    /// IDN-ATTR-001: registration settles the account's language from the locale it
    /// was begun under, and its codes go out in that language.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_001_RegistrationSettlesTheLanguageItWasBegunInAsync()
    {
        _ = Ok(await AcceptedAsync(await SecuredAsync()));

        Assert.Equal(Language, Assert.Single(_directory.Created).Language);
        Assert.All(_notifications.Sent, sent => Assert.Equal(Language, sent.Language));
    }

    /// <summary>
    /// IDN-ATTR-001: a locale that finds no language the deployment writes in settles
    /// none, and the codes go out in every declared language.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_001_ALocaleTheDeploymentDoesNotWriteInSettlesNoneAsync()
    {
        _configuration.Set(Settings.NotificationLanguages, Arabic);

        _ = Ok(await AcceptedAsync(await SecuredAsync()));

        Assert.Null(Assert.Single(_directory.Created).Language);
        Assert.All(_notifications.Sent, sent => Assert.Null(sent.Language));
    }

    /// <summary>
    /// IDN-ATTR-001 and REG-SESS-005: the holder of an address someone tried to
    /// register is told in the language the holder settled on, not the one the person
    /// registering reads.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_001_TheHolderIsToldInTheirOwnLanguageAsync()
    {
        var holder = SubjectId.New(_randomness);

        _directory.Held(IdentifierKind.Email, Address, holder);
        _directory.Reads(holder, "ar");

        _ = await AwaitingAsync();

        SendRequest told = Assert.Single(_notifications.Sent);

        Assert.Equal(MessageKind.AccountExists, told.Message);
        Assert.Equal("ar", told.Language);
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
    /// PRIV-MINOR-001 AC1, AC3: on an adults-only deployment nothing is taken before
    /// the affirmation is derived, and an under-age answer ends the session, so no
    /// account exists whose subject has not affirmed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_MINOR_001_AC1_NoAccountExistsWithoutTheDerivedAffirmationAsync()
    {
        _configuration.Set(Settings.RegistrationAdultAffirmation, AttributeRequirement.Required);

        RegistrationSessionId unanswered = await StartedAsync();

        Assert.Equal(
            ErrorCodes.AffirmationRequired,
            Refused(await Service.StageAsync(
                unanswered,
                IdentifierKind.Email,
                Address,
                TestContext.Current.CancellationToken)));

        RegistrationSessionId underage = await StartedAsync();

        Assert.Equal(
            ErrorCodes.ProfileUnderage,
            Refused(await Service.RecordAgeAsync(
                underage,
                Minor,
                TestContext.Current.CancellationToken)));

        Assert.Empty(_directory.Created);

        RegistrationSessionId affirmed = await SecuredAsync();

        _ = Ok(await AcceptedAsync(affirmed));

        Assert.True(Assert.Single(_directory.Created).AdultAffirmed);
    }

    /// <summary>
    /// PRIV-MINOR-001 AC2: with the date off, the affirmation is derived from the age
    /// screen and the date itself reaches no record.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_MINOR_001_AC2_TheDateIsNotKeptWhereTheDeploymentDoesNotKeepItAsync()
    {
        _ = Ok(await AcceptedAsync(await SecuredAsync()));

        NewAccount withheld = Assert.Single(_directory.Created);

        Assert.True(withheld.AdultAffirmed);
        Assert.Null(withheld.DateOfBirth);
    }

    /// <summary>
    /// PRIV-MINOR-001 AC2: with the date on, it is on the account beside the
    /// affirmation, as the personal field PRIV-RIGHT-005a holds under the subject key.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_MINOR_001_AC2_TheDateIsKeptWhereTheDeploymentKeepsItAsync()
    {
        _configuration.Set(Settings.ProfileDateOfBirth, AttributeRequirement.Optional);

        _ = Ok(await AcceptedAsync(await SecuredAsync()));

        NewAccount kept = Assert.Single(_directory.Created);

        Assert.True(kept.AdultAffirmed);
        Assert.Equal(Adult, kept.DateOfBirth);
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

    /// <summary>
    /// REG-MAIL-001 AC4: the press on the invitation link that opens the registration
    /// is what verifies the email the invitation bound, and no code is sent to it; its
    /// step has nothing left to collect.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_001_AC4_ThePressVerifiesTheBoundEmailWithoutACodeAsync()
    {
        RegistrationSessionId session = Ok(await InvitedAsync(Issued(email: Address)));

        StagedIdentity email = Identity(session, IdentifierKind.Email);

        Assert.True(email.IsVerified);
        Assert.True(email.IsLocked);
        Assert.Empty(_notifications.Mail);

        _ = Ok(await Service.RecordAgeAsync(session, Adult, TestContext.Current.CancellationToken));

        Assert.Equal(RegistrationStep.Phone, Live(session).Step);
        Assert.Equal(session, _invitations.Held.Single().Session);
    }

    /// <summary>
    /// REG-INV-001 AC1 and REG-IDENT-010: an identifier the invitation binds is taken
    /// at its step only as it was bound and cannot be changed, while one it leaves open
    /// is the person's to choose and change.
    /// </summary>
    [Fact]
    public async Task REG_INV_001_AC1_ABoundIdentifierCannotBeChangedAndAnOpenOneCanAsync()
    {
        RegistrationSessionId bound = Ok(await InvitedAsync(Issued(email: Address, phone: Number)));

        _ = Ok(await Service.RecordAgeAsync(bound, Adult, TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.IdentifierLocked,
            Refused(await Service.ChangeAsync(
                bound,
                Identity(bound, IdentifierKind.Email).Id,
                "other@example.test",
                TestContext.Current.CancellationToken)));
        Assert.Equal(
            ErrorCodes.IdentifierLocked,
            Refused(await Service.StageAsync(
                bound,
                IdentifierKind.Phone,
                Mistyped,
                TestContext.Current.CancellationToken)));

        RegistrationState taken = Ok(await Service.StageAsync(
            bound,
            IdentifierKind.Phone,
            Number,
            TestContext.Current.CancellationToken));

        Assert.Equal(RegistrationStep.Confirm, taken.Step);
        Assert.All(taken.Identifiers, identifier => Assert.True(identifier.Locked));
        Assert.Equal(Number, Assert.Single(_notifications.Texts).Destination.Canonical);

        RegistrationSessionId open = Ok(await InvitedAsync(Issued(email: "other@example.test")));

        _ = Ok(await Service.RecordAgeAsync(open, Adult, TestContext.Current.CancellationToken));
        _ = Ok(await Service.StageAsync(open, IdentifierKind.Phone, Mistyped, TestContext.Current.CancellationToken));
        _ = Ok(await Service.ChangeAsync(
            open,
            Identity(open, IdentifierKind.Phone).Id,
            Number,
            TestContext.Current.CancellationToken));

        Assert.Equal(Number, Identity(open, IdentifierKind.Phone).Canonical);
        Assert.False(Identity(open, IdentifierKind.Phone).IsLocked);
    }

    /// <summary>
    /// REG-MAIL-001 AC3: a phone the invitation binds is verified before the
    /// registration goes on: its step cannot be passed over, and the confirm step
    /// waits for its code.
    /// </summary>
    [Fact]
    public async Task REG_MAIL_001_AC3_ABoundPhoneIsVerifiedBeforeTheRegistrationGoesOnAsync()
    {
        _configuration.Set(Settings.RegistrationPhone, AttributeRequirement.Optional);

        RegistrationSessionId session = Ok(await InvitedAsync(Issued(email: Address, phone: Number)));

        _ = Ok(await Service.RecordAgeAsync(session, Adult, TestContext.Current.CancellationToken));

        _ = Refused(await Service.SkipPhoneAsync(session, TestContext.Current.CancellationToken));

        _ = Ok(await Service.StageAsync(session, IdentifierKind.Phone, Number, TestContext.Current.CancellationToken));

        _ = Refused(await Service.ConfirmAsync(session, TestContext.Current.CancellationToken));

        await VerifiedAsync(session, IdentifierKind.Phone);

        _ = Ok(await Service.ConfirmAsync(session, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-LIFE-009a AC2: the link opens its invitation once and only within its
    /// lifetime; a token that opens nothing, one already used, one expired and one
    /// revoked are refused alike.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_009a_AC2_TheLinkOpensItsInvitationOnceAndInTimeAsync()
    {
        string used = Issued(email: Address);
        string lapsed = Issued(email: "late@example.test");
        string revoked = Issued(email: "gone@example.test");

        _invitations.Held.Last().Revoke(_clock.GetUtcNow());

        _ = Ok(await InvitedAsync(used));

        Assert.Equal(ErrorCodes.InvitationExpired, Refused(await InvitedAsync(used)));
        Assert.Equal(ErrorCodes.InvitationExpired, Refused(await InvitedAsync(revoked)));
        Assert.Equal(ErrorCodes.InvitationExpired, Refused(await InvitedAsync("no-such-token")));

        _clock.Advance(Settings.LinkInvitationLifetime.Default);

        Assert.Equal(ErrorCodes.InvitationExpired, Refused(await InvitedAsync(lapsed)));
    }

    /// <summary>
    /// REG-INV-002: an email the invitation binds that an account holds already is
    /// refused, so its holder accepts by signing in; the invitation is not spent and
    /// no registration is opened.
    /// </summary>
    [Fact]
    public async Task REG_INV_002_ABoundEmailAnAccountHoldsOpensNoRegistrationAsync()
    {
        _directory.Held(IdentifierKind.Email, Address, SubjectId.New(_randomness));

        string token = Issued(email: Address);

        Assert.Equal(ErrorCodes.InvitationIdentifierMismatch, Refused(await InvitedAsync(token)));
        Assert.Empty(_sessions.All);
        Assert.True(_invitations.Held.Single().Opens(_clock.GetUtcNow()));
    }

    /// <summary>
    /// REG-DOM-001 AC2: an email the person chooses at an invitation, where the
    /// invitation left the email open, is refused outside the inviting organization's
    /// verified domains.
    /// </summary>
    [Fact]
    public async Task REG_DOM_001_AC2_AnOpenEmailOutsideTheListIsRefusedAsync()
    {
        var organization = OrganizationId.New(_clock);

        await LockedAsync(organization, "example.test");

        RegistrationSessionId session = Ok(await InvitedAsync(Issued(organization, email: null)));

        _ = Ok(await Service.RecordAgeAsync(session, Adult, TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.IdentifierDomainNotAllowed,
            Refused(await Service.StageAsync(
                session,
                IdentifierKind.Email,
                "person@elsewhere.test",
                TestContext.Current.CancellationToken)));

        _ = Ok(await Service.StageAsync(session, IdentifierKind.Email, Address, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-LIFE-009a: from the moment the token attaches, the inviting organization's
    /// policy governs the registration, so a password its policy does not admit
    /// completes nothing where a public registration would go on.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_009a_TheInvitingOrganizationsPolicyGovernsTheRegistrationAsync()
    {
        var organization = OrganizationId.New(_clock);

        _configuration.Set(
            Settings.OrganizationPolicy,
            organization.ToString(),
            PolicyOverride.None with { LoginFactors = new[] { Factor.Passkey }.ToFrozenSet() });

        RegistrationSessionId invited = Ok(await InvitedAsync(Issued(organization, email: Address, phone: Number)));

        _ = Ok(await Service.RecordAgeAsync(invited, Adult, TestContext.Current.CancellationToken));
        _ = Ok(await Service.StageAsync(invited, IdentifierKind.Phone, Number, TestContext.Current.CancellationToken));
        await VerifiedAsync(invited, IdentifierKind.Phone);
        _ = Ok(await Service.ConfirmAsync(invited, TestContext.Current.CancellationToken));

        RegistrationState held = Ok(await Service.SetPasswordAsync(invited, Chosen, TestContext.Current.CancellationToken));

        Assert.Equal(RegistrationStep.Security, held.Step);

        Later();

        RegistrationSessionId open = await SecuredAsync();

        Assert.Equal(RegistrationStep.Terms, Live(open).Step);
    }

    /// <summary>
    /// REG-INV-001 AC2: the account a registration through an invitation creates holds
    /// the invitation and nothing of its organization: no membership until the person
    /// acknowledges it.
    /// </summary>
    [Fact]
    public async Task REG_INV_001_AC2_TheAccountHoldsTheInvitationAndNoMembershipAsync()
    {
        await RegisteredAsync();

        RegistrationSessionId session = Ok(await InvitedAsync(Issued(email: Address, phone: Number)));

        _ = Ok(await Service.RecordAgeAsync(session, Adult, TestContext.Current.CancellationToken));
        _ = Ok(await Service.StageAsync(session, IdentifierKind.Phone, Number, TestContext.Current.CancellationToken));
        await VerifiedAsync(session, IdentifierKind.Phone);
        _ = Ok(await Service.ConfirmAsync(session, TestContext.Current.CancellationToken));
        _ = Ok(await Service.SetPasswordAsync(session, Chosen, TestContext.Current.CancellationToken));

        SubjectId subject = Ok(await AcceptedAsync(session)).Subject;

        Invitation invitation = _invitations.Held.Single();

        Assert.Equal(subject, invitation.Invitee);
        Assert.Null(invitation.Session);
        Assert.False(invitation.IsAcknowledged);
        Assert.Empty(await _memberships.OfAsync(subject, TestContext.Current.CancellationToken));
    }

    // An invitation into an organization, issued now, binding what it is given; the
    // token is what its link carries.
    private string Issued(OrganizationId? organization = null, string? email = null, string? phone = null)
    {
        var token = OpaqueToken.Draw(_randomness);

        _invitations.Held.Add(Invitation.Issued(
            InvitationId.New(_clock),
            organization ?? OrganizationId.New(_clock),
            SubjectId.New(_randomness),
            new InvitedIdentifiers(email, phone, CorporateEmail: null),
            roles: [],
            documents: [],
            mailbox: null,
            token.Fingerprint(),
            _clock.GetUtcNow(),
            Settings.LinkInvitationLifetime.Default));

        return token.Value;
    }

    private async Task<Result<RegistrationSessionId>> InvitedAsync(string token) =>
        await Service.BeginAsync(Client, Language, Source, token, TestContext.Current.CancellationToken);

    // An organization locked to one domain, verified.
    private async Task LockedAsync(OrganizationId organization, string domain)
    {
        _configuration.Set(
            Settings.OrganizationPolicy,
            organization.ToString(),
            PolicyOverride.None with { EmailDomains = [domain] });

        var listed = LockedDomain.Listed(organization, domain, _randomness, Noon);

        listed.Checked(passed: true, Noon);

        await _domains.AddAsync(listed, TestContext.Current.CancellationToken);
    }

    // The steps a test is not about, run the way a browser runs them, so that each
    // test says only what it is checking.
    private Task RegisteredAsync() => _clients
        .RecordAsync(
            new OidcClient(Client, Client, OidcClientKind.BrowserApplication, Registered, ["openid"]),
            [1, 2, 3],
            TestContext.Current.CancellationToken)
        .AsTask();

    private async Task<RegistrationSessionId> StartedAsync()
    {
        Result<RegistrationSessionId> begun = await Service
            .BeginAsync(Client, Language, Source, invitationToken: null, TestContext.Current.CancellationToken);

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
        (kind is IdentifierKind.Email ? _notifications.Mail[^1] : _notifications.Texts[^1])
            .Values["token"];

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

    /// <summary>
    /// AUTH-PASS-001a AC1: a password below the single-factor floor with nothing
    /// beside it leaves the registration where it was, so no account is written.
    /// </summary>
    [Fact]
    public async Task AUTH_PASS_001a_AC1_AShortPasswordAloneNeverCreatesTheAccountAsync()
    {
        RegistrationSessionId session = await SecuredAsync(Short);

        Assert.Equal(
            ErrorCodes.RegistrationIncomplete,
            Refused(await AcceptedAsync(session)));

        Assert.Empty(_directory.Created);
        Assert.Empty(_live.All);
    }

    /// <summary>
    /// FE-REG-003 AC4: the second step is enrolled beside the password and not
    /// instead of it, so the password the person set stands afterwards.
    /// </summary>
    [Fact]
    public async Task FE_REG_003_AC4_ASecondStepAfterAPasswordLeavesItStandingAsync()
    {
        RegistrationSessionId session = await SecuredAsync(Short);

        RegistrationState settled = Ok(await Service.EnrolAsync(
            session,
            SecondStepCredential(),
            TestContext.Current.CancellationToken));

        Assert.True(settled.Security.Password);

        RegistrationCompleted completed = Ok(await AcceptedAsync(session));
        NewAccount written = Assert.Single(_directory.Created);

        Assert.Equal(completed.Subject, written.Subject);
        Assert.NotNull(await _passwords.FindAsync(completed.Subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-004 AC2: a verification code is held on the registration session
    /// under a lifetime of its own, and nothing about it reaches the credential
    /// store an authentication code would answer from.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC2_TheVerificationCodeIsTheSessionsAndLivesItsOwnLifetimeAsync()
    {
        _configuration.Set(Settings.CodeVerificationLifetime, TimeSpan.FromMinutes(5));

        RegistrationSessionId session = await AwaitingAsync();

        Assert.NotNull(Identity(session, IdentifierKind.Email).Code);
        Assert.Empty(_authenticators.All);

        _clock.Advance(TimeSpan.FromMinutes(6));

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await Service.VerifyAsync(
                session,
                Identity(session, IdentifierKind.Email).Id,
                Code(session, IdentifierKind.Email),
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-004 AC3: the attempt after the cap is refused, the right code is
    /// refused with it, and the replacement the person asks for leaves the dead one
    /// dead.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC3_TheCapKillsTheCodeAndAReplacementReplacesItAsync()
    {
        _configuration.Set(Settings.CodeVerificationAttempts, 5);

        RegistrationSessionId session = await AwaitingAsync();
        IdentifierId staged = Identity(session, IdentifierKind.Email).Id;
        string right = Code(session, IdentifierKind.Email);

        for (int attempt = 0; attempt < 6; attempt++)
        {
            Assert.Equal(
                ErrorCodes.CodeInvalid,
                Refused(await Service.VerifyAsync(
                    session,
                    staged,
                    "000000",
                    TestContext.Current.CancellationToken)));
        }

        Assert.True(Identity(session, IdentifierKind.Email).CodeSpent);

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.VerifyAsync(
                session,
                staged,
                right,
                TestContext.Current.CancellationToken)));

        Later();

        _ = Ok(await Service.ChangeAsync(
            session,
            staged,
            Address,
            TestContext.Current.CancellationToken));

        Assert.NotEqual(right, Code(session, IdentifierKind.Email));

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.VerifyAsync(
                session,
                Identity(session, IdentifierKind.Email).Id,
                right,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-016 AC7: the browser the terms step was completed in is seen, so the
    /// first sign-in after registration is not held for a code until the remembering
    /// runs out.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_016_AC7_TheRegisteringBrowserIsSeenForTheLifetimeAsync()
    {
        _configuration.Set(Settings.DeviceVerificationLifetime, TimeSpan.FromDays(90));

        RegistrationSessionId session = await SecuredAsync(Floor);

        RegistrationOutcome completed = Ok(await Service.CompleteAsync(
            session,
            Terms,
            Notice,
            Unticked,
            Browser,
            TestContext.Current.CancellationToken));

        var single = new Assurance(AssuranceLevel.Aal1, PhishingResistant: false);
        var devices = new DeviceService(_devices, _configuration, _work, _events, _clock, _randomness);

        Assert.False(Ok(await devices.ChecksAsync(
            completed.Subject,
            single,
            single,
            completed.Browser.Value,
            TestContext.Current.CancellationToken)));

        _clock.Advance(TimeSpan.FromDays(91));

        Assert.True(Ok(await devices.ChecksAsync(
            completed.Subject,
            single,
            single,
            completed.Browser.Value,
            TestContext.Current.CancellationToken)));
    }
}
