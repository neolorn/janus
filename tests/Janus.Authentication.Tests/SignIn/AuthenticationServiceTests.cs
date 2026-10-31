using System;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Organizations;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.SignIn;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Oidc;
using Janus.Authentication.Tests.Organizations;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Sending;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using OtpNet;
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
    private const string Fresh = "203.0.113.9";
    private const string Address = "person@example.test";
    private const string Elsewhere = "nobody@example.test";
    private const string Second = "second@example.test";
    private const string Number = "+441632960011";
    private const string Secret = "orangemarmaladeandtoast";

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly DeviceDescription Browser = new("Firefox", "Fedora");

    private static readonly OrganizationId Locked = new(Guid.NewGuid());

    private static readonly byte[] CredentialId = [7, 7, 7];

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
    private readonly DomainStoreInMemory _domains = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly LocationResolverInMemory _locations = new();
    private readonly ThrottleLedgerInMemory _throttle = new();
    private readonly NoticeLedgerInMemory _notices = new();
    private readonly VerificationCodeStoreInMemory _codes = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly GovernedSendInMemory _notifications = new();
    private readonly SendingRestrictionsInMemory _restrictions = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly PhoneSignalAuditInMemory _considered = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private PhoneSignalProvider? _provider;

    /// <summary>
    /// A deployment that has named the keys with no default, the languages it writes
    /// in among them.
    /// </summary>
    public AuthenticationServiceTests()
    {
        _notifications.Work = _work;
        _restrictions.Work = _work;
        _configuration.Set(Settings.NotificationLanguages, [Language]);
        _configuration.Set(Settings.WebAuthnRelyingPartyId, "example.test");
        _configuration.Set(Settings.WebAuthnOrigins, ["https://example.test"]);
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
            _audit,
            Policies,
            Lock,
            Throttle,
            _notifications,
            Signals,
            Guard,
            Codes,
            _configuration,
            _work,
            _clock,
            _randomness);

    private StepUpGuard Guard =>
        new(_live, _authenticators, _passwords, Policies, _identifiers, Signals, _clock);

    private PhoneSignals Signals => new(_provider, _considered, _work, _clock);

    private VerificationCodes Codes =>
        new(_codes, _configuration, _work, _clock, _randomness);

    private SignInLinks Links =>
        new(
            _pending,
            _challenges,
            _identifiers,
            _authenticators,
            _accounts,
            Policies,
            Lock,
            _notifications,
            _restrictions,
            Landing.Links,
            new NonExistenceNotice(
                _configuration,
                _notifications,
                _restrictions,
                _notices,
                _work,
                _events,
                _clock),
            Signals,
            Throttle,
            _configuration,
            _work,
            _clock,
            _randomness);

    private PolicyResolution Policies => new(_memberships, _configuration, _raises);

    private DomainLock Lock => new(_memberships, _configuration, _domains);

    private DeviceService Devices =>
        new(_devices, _configuration, _work, _events, _clock, _randomness);

    private SessionService Sessions =>
        new(
            _live,
            _audit,
            _authenticators,
            _credentials,
            Policies,
            _configuration,
            new AdministrativeScope(_gate, _administrative),
            new StepUpGuard(_live, _authenticators, _passwords, Policies, _identifiers, Signals, _clock),
            _accounts,
            _locations,
            new ConcurrentSessions(_live, _configuration, _events),
            new OidcClientStoreInMemory(),
            new PendingAuthorizationStoreInMemory(),
            _events,
            _work,
            _clock,
            _randomness);

    private ThrottleService Throttle =>
        new(_configuration, _throttle, _work, _events, _clock);

    private PasswordService Passwords =>
        new(
            _passwords,
            new PasswordScreening(_corpus, _words, _configuration, _screening, _events, _clock),
            new Argon2idHasher(_randomness),
            _events,
            _configuration,
            _work,
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// IDN-ACCT-007 AC2 (D-166): a restricted account signs in with its password as an
    /// active one does: a session is issued, and no failure is counted or recorded.
    /// </summary>
    [Fact]
    public async Task IDN_ACCT_007_AC2_ARestrictedAccountSignsInWithItsFactorAsync()
    {
        SubjectId subject = await AccountAsync();

        _accounts.Stands(subject, AccountState.Restricted);
        Remembered(subject);

        SignInProgress reached = await SignedInAsync(subject, Factor.Password, Secret);

        Assert.Equal(SignInStatus.Complete, reached.Status);
        Assert.NotNull(await _live.FindAsync(
            reached.Session ?? throw new InvalidOperationException("No session was issued."),
            TestContext.Current.CancellationToken));
        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// IDN-ACCT-007 AC2 (D-166): a restricted account signs in with the provider it
    /// linked as an active one does.
    /// </summary>
    [Fact]
    public async Task IDN_ACCT_007_AC2_ARestrictedAccountSignsInWithItsProviderAsync()
    {
        SubjectId subject = await AccountAsync();

        await _authenticators.LinkAsync(
            Authenticator.Linked(
                AuthenticatorId.New(_clock),
                subject,
                Factor.Google,
                Label(Factor.Google),
                _clock.GetUtcNow()),
            "linked-at-the-provider",
            TestContext.Current.CancellationToken);

        _accounts.Stands(subject, AccountState.Restricted);

        SignInOutcome outcome = (await Service.DelegatedAsync(
                Factor.Google,
                "linked-at-the-provider",
                new SessionOrigin(Source, Browser),
                TestContext.Current.CancellationToken))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(SignInStatus.Complete, outcome.Progress.Status);
        Assert.NotNull(outcome.Session);
        Assert.Empty(_audit.Failed);
    }

    /// <summary>
    /// IDN-ACCT-007 AC2 (D-166): a restricted account is sent its sign-in link, and the
    /// link signs it in.
    /// </summary>
    [Fact]
    public async Task IDN_ACCT_007_AC2_ARestrictedAccountIsSentItsSignInLinkAsync()
    {
        SubjectId subject = await AccountAsync();

        _accounts.Stands(subject, AccountState.Restricted);
        Enables(Factor.EmailLink);
        Remembered(subject);

        SignInLanding landed = await PressedAsync(subject);

        Assert.Single(_notifications.Mail);
        Assert.Equal(SignInStatus.Complete, landed.SignedIn?.Status);
    }

    /// <summary>
    /// IDN-ACCT-007 (D-166): only the restricted state joins the active one at sign-in;
    /// a suspended, deleting or deleted account is still refused as a wrong password is.
    /// </summary>
    /// <param name="state">Where the account stands.</param>
    [Theory]
    [InlineData(AccountState.Suspended)]
    [InlineData(AccountState.Deleting)]
    [InlineData(AccountState.Deleted)]
    public async Task IDN_ACCT_007_ASuspendedDeletingOrDeletedAccountIsStillRefusedAsync(AccountState state)
    {
        SubjectId subject = await AccountAsync();

        _accounts.Stands(subject, state);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        Assert.Equal(ErrorCodes.FactorRejected, Refused(await PresentAsync(began.Challenge, Factor.Password, Secret)));
        Assert.Empty(await _live.LiveOfAsync(subject, _clock.GetUtcNow(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-015 AC9 (D-224): the answer that asks for the second step tells whether
    /// the call completing it may trust the browser, and the completion carries the
    /// same; a sign-in completed by its first factor alone is offered nothing.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_015_AC9_TheSecondStepsAnswerTellsWhetherItsCompletionMayTrustTheBrowserAsync()
    {
        SubjectId subject = await AccountAsync();

        Remembered(subject);

        SignInProgress alone = await SignedInAsync(subject, Factor.Password, Secret);

        Holds(subject, Factor.Totp);

        SignInChallenge began = await BeganAsync(Address);
        SignInProgress asked = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));
        SignInOutcome completed = await TrustedAsync();

        Assert.Equal(SignInStatus.Complete, alone.Status);
        Assert.False(alone.TrustDeviceOffered);
        Assert.Equal(SignInStatus.FactorRequired, asked.Status);
        Assert.True(asked.TrustDeviceOffered);
        Assert.Equal(SignInStatus.Complete, completed.Progress.Status);
        Assert.True(completed.Progress.TrustDeviceOffered);
    }

    /// <summary>
    /// AUTH-FACT-015 AC9 (D-224): the second step's answer offers no trust where its
    /// completion could record none: on a restricted account, and while the password is
    /// below the single-factor floor.
    /// </summary>
    /// <param name="restricted">
    /// Whether the account is restricted; otherwise its password is below the floor.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AUTH_FACT_015_AC9_TheSecondStepsAnswerOffersNoTrustItsCompletionCouldNotRecordAsync(bool restricted)
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Remembered(subject);

        if (restricted)
        {
            _accounts.Stands(subject, AccountState.Restricted);
        }
        else
        {
            Password held = await _passwords.FindAsync(subject, TestContext.Current.CancellationToken)
                ?? throw new InvalidOperationException("The account holds no password.");

            await _passwords.SetAsync(
                Password.Existing(subject, held.Hash, meetsSingleFactorFloor: false, held.SetAt),
                TestContext.Current.CancellationToken);
        }

        SignInChallenge began = await BeganAsync(Address);
        SignInProgress asked = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));

        Assert.Equal(SignInStatus.FactorRequired, asked.Status);
        Assert.False(asked.TrustDeviceOffered);
    }

    /// <summary>
    /// IDN-ACCT-007 (D-166): trusting a browser writes to the account, so a restricted
    /// account's sign-in is not offered it and records none where it asks, while an
    /// active account's same sign-in is offered it.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_015_AC8_ARestrictedAccountsSignInTrustsNoDeviceAsync()
    {
        SubjectId active = await AccountAsync();

        Holds(active, Factor.Totp);
        Remembered(active);

        SignInOutcome offered = await TrustedAsync();

        _accounts.Stands(active, AccountState.Restricted);

        SignInOutcome withheld = await TrustedAsync();

        Assert.True(offered.Progress.TrustDeviceOffered);
        Assert.NotNull(offered.Trusted);
        Assert.Equal(SignInStatus.Complete, withheld.Progress.Status);
        Assert.False(withheld.Progress.TrustDeviceOffered);
        Assert.Null(withheld.Trusted);
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
    /// CONV-LOG-005: a refused factor is written to the trail whether or not an account
    /// holds the identifier, against the account where one does and against none where
    /// none does, so the two refusals of AUTH-ABUSE-003 do the same work.
    /// </summary>
    [Fact]
    public async Task PresentAsync_ARefusedFactor_IsRecordedWhetherOrNotAnAccountHoldsTheIdentifierAsync()
    {
        SubjectId subject = await AccountAsync();

        SignInChallenge none = await BeganAsync(Elsewhere);
        _ = await PresentAsync(none.Challenge, Factor.Password, Secret);

        SignInChallenge held = await BeganAsync(Address);
        _ = await PresentAsync(held.Challenge, Factor.Password, "wrong" + Secret);

        Assert.Equal<(SubjectId?, Factor)>(
            [(null, Factor.Password), (subject, Factor.Password)],
            _audit.Failed);
    }

    /// <summary>
    /// CONV-LOG-005 AC1: a handle that opens no sign-in is a refused factor naming no
    /// account, recorded as one; its source's delay is asked first, so once the delay
    /// stands nothing more is recorded until it lifts (AUTH-ABUSE-001).
    /// </summary>
    [Fact]
    public async Task CONV_LOG_005_AC1_AHandleThatOpensNothingIsRecordedBehindTheDelayAsync()
    {
        await AccountAsync();

        for (int attempt = 0; attempt < 3; attempt++)
        {
            Assert.Equal(
                ErrorCodes.FactorRejected,
                Refused(await PresentAsync("a-handle-nothing-opened", Factor.Password, Secret)));
        }

        Assert.Equal(
            ErrorCodes.Throttled,
            Refused(await PresentAsync("a-handle-nothing-opened", Factor.Password, Secret)));
        Assert.Equal<(SubjectId?, Factor)>(
            [(null, Factor.Password), (null, Factor.Password), (null, Factor.Password)],
            _audit.Failed);
    }

    /// <summary>
    /// CONV-LOG-005 AC1: a wrong code of the new-device check is recorded as that
    /// verification against the account whose sign-in it would complete, naming no
    /// factor.
    /// </summary>
    [Fact]
    public async Task CONV_LOG_005_AC1_AWrongDeviceCodeIsRecordedAgainstTheAccountAsync()
    {
        SubjectId subject = await AccountAsync();
        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        Result<SignInProgress> wrong = await Service.VerifyDeviceAsync(
            began.Challenge,
            Other(Code()),
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.CodeInvalid, Refused(wrong));
        Assert.Equal([subject], _audit.DeviceVerificationsFailed);
        Assert.Empty(_audit.Failed);
    }

    /// <summary>
    /// CONV-LOG-005 AC1: a new-device code presented for a handle that opens nothing is
    /// recorded as that verification against no account.
    /// </summary>
    [Fact]
    public async Task CONV_LOG_005_AC1_ADeviceCodeForAHandleThatOpensNothingIsRecordedAsync()
    {
        Result<SignInProgress> nowhere = await Service.VerifyDeviceAsync(
            "a-handle-nothing-opened",
            "123456",
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.CodeExpired, Refused(nowhere));
        Assert.Equal([(SubjectId?)null], _audit.DeviceVerificationsFailed);
        Assert.Empty(_audit.Failed);
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC1: wrong device-verification codes count against the delay as
    /// any refused factor does, so the right code inside it is refused unread, and
    /// once it lifts the same code completes the sign-in.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AC1_WrongDeviceCodesAreHeldByTheDelayAsync()
    {
        await AccountAsync();

        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        string right = Code();

        for (int attempt = 0; attempt < 3; attempt++)
        {
            _ = await Service.VerifyDeviceAsync(
                began.Challenge,
                Other(right),
                Browser,
                Source,
                TestContext.Current.CancellationToken);
        }

        Result<SignInProgress> delayed = await Service.VerifyDeviceAsync(
            began.Challenge,
            right,
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromSeconds(1));

        Result<SignInProgress> completed = await Service.VerifyDeviceAsync(
            began.Challenge,
            right,
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.Throttled, Refused(delayed));
        Assert.Equal(SignInStatus.Complete, Reached(completed).Status);
        Assert.Equal(3, _audit.DeviceVerificationsFailed.Count);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC7: a service method that returns a result returns the failure
    /// its transaction answers, at the opening and at the commit alike.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC7_AServiceReturnsTheFailureItsTransactionAnswersAsync()
    {
        await AccountAsync();

        _work.RefusesBegin = Error.From(ErrorCodes.SystemFault);

        Result<SignInChallenge> unopened = await Service.BeginAsync(
            Address,
            Fresh,
            remembered: null,
            trusted: null,
            browser: null,
            TestContext.Current.CancellationToken);

        _work.RefusesCommit = Error.From(ErrorCodes.SystemFault);

        Result<SignInChallenge> uncommitted = await Service.BeginAsync(
            Address,
            Fresh,
            remembered: null,
            trusted: null,
            browser: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.SystemFault, unopened.Match(_ => (ErrorCode?)null, error => error.Code));
        Assert.Equal(ErrorCodes.SystemFault, uncommitted.Match(_ => (ErrorCode?)null, error => error.Code));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a sign-in whose challenge another completion removed while
    /// this one waited for its lock is refused, begins no session, and its transaction
    /// is rolled back and left closed.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ASignInCompletedMeanwhileIsRolledBackAsync()
    {
        SubjectId subject = await AccountAsync();

        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _challenges.RemovedMeanwhile = true;
        _work.Reset();

        Result<SignInProgress> refused = await PresentAsync(began.Challenge, Factor.Password, Secret);

        Assert.Equal(ErrorCodes.FactorRejected, Refused(refused));
        Assert.Empty(await _live.LiveOfAsync(subject, _clock.GetUtcNow(), TestContext.Current.CancellationToken));
        Assert.False(_work.Open);
        Assert.Equal((_work.Opened - 1, 1), (_work.Committed, _work.RolledBack));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8: a session refused inside a sign-in's completion, for an
    /// account suspended while it waited, rolls its own level back and the completion
    /// with it, so nothing of the completion is committed.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_ASessionRefusedInsideACompletionRollsTheCompletionBackAsync()
    {
        SubjectId subject = await AccountAsync();

        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _accounts.Holding = held =>
        {
            _accounts.Holding = null;
            _accounts.Stands(held, AccountState.Suspended);
        };
        _work.Reset();

        Result<SignInProgress> refused = await PresentAsync(began.Challenge, Factor.Password, Secret);

        Assert.Equal(ErrorCodes.FactorRejected, Refused(refused));
        Assert.False(_work.Open);
        Assert.Equal((_work.Opened - 2, 2), (_work.Committed, _work.RolledBack));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8, AUTHZ-INHERIT-002: a factor presented at a sign-in, whose
    /// refusal keeps its record and its counts, called inside a unit of work another
    /// opened is a fault of the calling code: it ends the level it joined before it
    /// writes anything, so nothing of the outer unit of work commits and nothing is
    /// recorded or counted.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_AFactorPresentedInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        _ = await AccountAsync();

        SignInChallenge began = await BeganAsync(Address);

        await InsideAsync(async () => await PresentAsync(began.Challenge, Factor.Password, "wrong" + Secret));

        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8, AUTHZ-INHERIT-002: a factor presented against a handle that
    /// opens nothing, refused and counted before any unit of work of its own, is the
    /// same fault inside one another opened.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_AFactorForAHandleThatOpensNothingInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        _ = await AccountAsync();

        await InsideAsync(async () => await PresentAsync("a-handle-nothing-opened", Factor.Password, Secret));

        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8, AUTHZ-INHERIT-002: a new-device code, whose wrong try keeps
    /// its count, its record and the delay's counts, presented inside a unit of work
    /// another opened is a fault of the calling code.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_ADeviceCodeInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        _ = await AccountAsync();

        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        await InsideAsync(async () => await Service.VerifyDeviceAsync(
            began.Challenge,
            Other(Code()),
            Browser,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Empty(_audit.DeviceVerificationsFailed);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8, AUTHZ-INHERIT-002: a factor presented at a step-up, whose
    /// refusal keeps <c>auth.stepup.failed</c> and its counts, called inside a unit of
    /// work another opened is a fault of the calling code.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_AStepUpInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        SubjectId subject = await AccountAsync();
        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);

        await InsideAsync(async () => await Service.StepUpAsync(
            AccessContext.Of(subject),
            session,
            began.Challenge,
            new FactorPresentation(Factor.Password) { Value = "not the password" },
            Source,
            TestContext.Current.CancellationToken));

        Assert.Empty(_audit.StepUpsFailed);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8, AUTHZ-INHERIT-002: a pressed sign-in link that opens
    /// nothing, whose refusal keeps its record and its count, landed inside a unit of
    /// work another opened is a fault of the calling code.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_APressedLinkInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        await InsideAsync(async () => await Service.LandAsync(
            browser: null,
            "a-token-nothing-issued",
            Factor.PhoneLink,
            press: true,
            Browser,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8, AUTHZ-INHERIT-002: a delegated sign-in, whose refusal keeps
    /// its record and its counts, called inside a unit of work another opened is a
    /// fault of the calling code.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_ADelegatedSignInInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        _ = await AccountAsync();

        await InsideAsync(async () => await Service.DelegatedAsync(
            Factor.Google,
            "linked-to-nothing",
            new SessionOrigin(Source, Browser),
            TestContext.Current.CancellationToken));

        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8, AUTHZ-INHERIT-002: an ask for the second step's text code,
    /// whose refusal keeps the number's consideration where the credential left before
    /// the code was issued (AUTH-FACT-002 AC11), called inside a unit of work another
    /// opened is a fault of the calling code: nothing is issued, sent or recorded.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_AnAskInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        _ = Held(subject, Factor.PhoneCode);
        Remembered(subject);
        Answers(PhoneSignal.Clear);

        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        await InsideAsync(async () => await AskedAsync(began.Challenge, stepping: null));

        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Empty(_considered.Records);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a step-up whose challenge another raise removed while this
    /// one waited for its lock is refused, and its transaction is rolled back and left
    /// closed.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AStepUpWhoseChallengeIsGoneIsRolledBackAsync()
    {
        SubjectId subject = await AccountAsync();

        Remembered(subject);

        SignInProgress signedIn = await SignedInAsync(subject, Factor.Password, Secret);
        SignInChallenge began = await BeganAsync(Address);

        _challenges.RemovedMeanwhile = true;
        _work.Reset();

        Result<SignInProgress> refused = await Service.StepUpAsync(
            AccessContext.Of(subject),
            Assert.IsType<SessionId>(signedIn.Session),
            began.Challenge,
            new FactorPresentation(Factor.Password) { Value = Secret },
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.FactorRejected, Refused(refused));
        Assert.False(_work.Open);
        Assert.Equal((_work.Opened - 1, 1), (_work.Committed, _work.RolledBack));
    }

    /// <summary>
    /// IDN-LIFE-009b AC5, AUTH-SESS-009 AC5, AUTH-STEP-002 AC9: a session the account
    /// held before a membership attached passes no gate once it is downgraded; a factor
    /// the organization's policy does not permit is refused at the step-up before it is
    /// verified, right or wrong, and counted; a permitted factor presented then lifts
    /// the downgrade and leaves what the session had attained as it was.
    /// </summary>
    [Fact]
    public async Task IDN_LIFE_009b_AC5_ASessionHeldBeforeTheMembershipIsDowngradedAsync()
    {
        SubjectId subject = await AccountAsync();

        Remembered(subject);

        SessionId held = Assert.IsType<SessionId>((await SignedInAsync(subject, Factor.Password, Secret)).Session);
        Session session = Assert.IsType<Session>(await _live.FindAsync(held, TestContext.Current.CancellationToken));
        (AssuranceLevel attained, DateTimeOffset reachedAt) = (session.Attained, session.DelegatedAt);

        _memberships.Place(subject, Locked);
        Permits(Factor.Passkey);
        _clock.Advance(TimeSpan.FromMinutes(1));
        _ = await _live.DowngradeAsync(subject, _clock.GetUtcNow(), TestContext.Current.CancellationToken);

        Result<SignInProgress> right = await SteppedUpAsync(subject, held, Secret);
        Result<SignInProgress> wrong = await SteppedUpAsync(subject, held, "not the password at all");

        Assert.Equal(ErrorCodes.FactorNotPermitted, Refused(right));
        Assert.Equal(ErrorCodes.FactorNotPermitted, Refused(wrong));
        Assert.Equal([Factor.Password, Factor.Password], _audit.StepUpsFailed.Select(failed => failed.Presented));
        Assert.Equal(2, _throttle.Counted.Count);
        Assert.False(session.Counts(session.DelegatedAt));
        Assert.Equal(
            (attained, reachedAt, (DateTimeOffset?)reachedAt),
            (session.Attained, session.DelegatedAt, session.Aal1At));

        Permits(Factor.Passkey, Factor.Password);
        _clock.Advance(TimeSpan.FromMinutes(1));

        _ = Reached(await SteppedUpAsync(subject, held, Secret));

        Assert.True(session.Counts(Assert.IsType<DateTimeOffset>(session.Aal1At)));
        Assert.Equal(attained, session.Attained);

        void Permits(params Factor[] factors) =>
            _configuration.Set(
                Settings.OrganizationPolicy,
                Locked.ToString(),
                PolicyOverride.None with { LoginFactors = new HashSet<Factor>(factors) });

        async ValueTask<Result<SignInProgress>> SteppedUpAsync(SubjectId stepping, SessionId on, string password) =>
            await Service.StepUpAsync(
                AccessContext.Of(stepping),
                on,
                (await BeganAsync(Address)).Challenge,
                new FactorPresentation(Factor.Password) { Value = password },
                Source,
                TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// AUTH-STEP-002 AC4c: a password at one call of a step-up and a generated code at
    /// the next, against the one challenge, pass a gate declared <c>aal2</c>: the
    /// password is held on the challenge, which goes on asking, and the code beside it
    /// raises the session to what the two reach together.
    /// </summary>
    [Fact]
    public async Task AUTH_STEP_002_AC4c_APasswordAtOneCallAndAGeneratedCodeAtTheNextPassAGateDeclaredAal2Async()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Declares(new Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(5)));

        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);
        string generated = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);

        Result<SignInProgress> first = await SteppedAsync(new FactorPresentation(Factor.Password) { Value = Secret });
        Error? between = await Guard.PassedAsync(
            subject,
            session,
            StepUpAction.AccountSuspend,
            TestContext.Current.CancellationToken);
        Result<SignInProgress> second = await SteppedAsync(new FactorPresentation(Factor.Totp) { Value = generated });

        Assert.Equal(ErrorCodes.StepUpRequired, between?.Code);
        Assert.Null(Refused(second));
        Assert.Null(await Guard.PassedAsync(
            subject,
            session,
            StepUpAction.AccountSuspend,
            TestContext.Current.CancellationToken));
        Assert.Equal(
            (SignInStatus.FactorRequired, AssuranceLevel.Aal1),
            (Reached(first).Status, Reached(first).AssuranceLevel));
        Assert.Equal([Factor.Totp], Reached(first).Required);
        Assert.Equal(
            (SignInStatus.Complete, AssuranceLevel.Aal2, false),
            (Reached(second).Status, Reached(second).AssuranceLevel, Reached(second).PhishingResistant));
        Assert.Empty(Reached(second).Required);
        Assert.Empty(_audit.StepUpsFailed);
        Assert.False(_work.Open);

        ValueTask<Result<SignInProgress>> SteppedAsync(FactorPresentation presented) =>
            Service.StepUpAsync(
                AccessContext.Of(subject),
                session,
                began.Challenge,
                presented,
                Source,
                TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// AUTH-SESS-001 AC3: a bare password presented at a step-up under a session that
    /// reached <c>aal2</c> earlier leaves the instant of <c>aal2</c> unchanged, and an
    /// <c>aal2</c> gate whose maximum age has passed since then is not met.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_001_AC3_ABarePasswordLeavesTheInstantOfAal2UnchangedAndAnAgedAal2GateUnmetAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Declares(new Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(5)));

        SessionId held = Opened(subject, AssuranceLevel.Aal2);
        Session session = Assert.IsType<Session>(await _live.FindAsync(held, TestContext.Current.CancellationToken));
        DateTimeOffset? reachedAt = session.Aal2At;

        _clock.Advance(TimeSpan.FromMinutes(6));

        Error? aged = await PassedAsync();
        Result<SignInProgress> presented = await Service.StepUpAsync(
            AccessContext.Of(subject),
            held,
            (await BeganAsync(Address)).Challenge,
            new FactorPresentation(Factor.Password) { Value = Secret },
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.StepUpRequired, aged?.Code);
        Assert.Null(Refused(presented));
        Assert.Equal((AssuranceLevel.Aal2, reachedAt), (session.Attained, session.Aal2At));
        Assert.Equal(_clock.GetUtcNow(), session.Aal1At);
        Assert.NotNull(reachedAt);
        Assert.Equal(ErrorCodes.StepUpRequired, (await PassedAsync())?.Code);

        ValueTask<Error?> PassedAsync() =>
            Guard.PassedAsync(subject, held, StepUpAction.AccountSuspend, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// AUTH-STEP-002 AC4d: at a step-up whose strictest gate asks <c>aal2</c>, on an
    /// account holding a second step, a password alone is answered
    /// <c>factorRequired</c> reporting <c>aal1</c>, which is what the factors accepted
    /// reach and not what the session reached earlier, and renews the instant of
    /// <c>aal1</c> alone.
    /// </summary>
    [Fact]
    public async Task AUTH_STEP_002_AC4d_APasswordAloneIsAnsweredFactorRequiredReportingAal1AndRenewsAal1AloneAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Declares(new Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(5)));

        SessionId held = Opened(subject, AssuranceLevel.Aal2);
        Session session = Assert.IsType<Session>(await _live.FindAsync(held, TestContext.Current.CancellationToken));
        (DateTimeOffset? aal2At, DateTimeOffset? aal3At, DateTimeOffset? resistedAt) =
            (session.Aal2At, session.Aal3At, session.PhishingResistantAt);

        _clock.Advance(TimeSpan.FromMinutes(6));

        SignInProgress answered = Reached(await Service.StepUpAsync(
            AccessContext.Of(subject),
            held,
            (await BeganAsync(Address)).Challenge,
            new FactorPresentation(Factor.Password) { Value = Secret },
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            (SignInStatus.FactorRequired, AssuranceLevel.Aal1, false),
            (answered.Status, answered.AssuranceLevel, answered.PhishingResistant));
        Assert.Equal([Factor.Totp], answered.Required);
        Assert.Equal(_clock.GetUtcNow(), session.Aal1At);
        Assert.Equal((aal2At, aal3At, resistedAt), (session.Aal2At, session.Aal3At, session.PhishingResistantAt));
        Assert.NotNull(aal2At);
        Assert.NotEqual(session.Aal1At, session.Aal2At);
    }

    /// <summary>
    /// AUTH-STEP-002 AC3: at a step-up, which names no action, a session is not
    /// challenged where it last reached the level of the strictest of the policy's
    /// gates within that gate's maximum age, and is challenged once that age has
    /// passed, though it still meets the weakest of them.
    /// </summary>
    [Fact]
    public async Task AUTH_STEP_002_AC3_AtAStepUpASessionThatMeetsTheStrictestOfThePolicysGatesIsNotChallengedAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Declares(
            new Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(5)),
            new Gate(GateLevel.Aal1, PhishingResistant: false, TimeSpan.FromMinutes(30)));

        SessionId session = Opened(subject, AssuranceLevel.Aal2);

        _clock.Advance(TimeSpan.FromMinutes(4));

        StepUpChallenge within = await OfferedAsync(subject, session);

        _clock.Advance(TimeSpan.FromMinutes(2));

        StepUpChallenge aged = await OfferedAsync(subject, session);

        Assert.Equal(StepUpOutcome.Satisfied, within.Outcome);
        Assert.Empty(within.Combinations);
        Assert.Equal(StepUpOutcome.Present, aged.Outcome);
        Assert.NotEmpty(aged.Combinations);
    }

    /// <summary>
    /// AUTH-STEP-002 AC4a: at a step-up a password with a text code is offered where
    /// the weakest of the policy's gates asks no phishing resistance, one gate asking
    /// it, and is not offered where the weakest asks it, every gate asking it.
    /// </summary>
    /// <param name="weakestAsks">Whether every gate of the policy asks phishing resistance.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUTH_STEP_002_AC4a_AtAStepUpAPasswordWithATextCodeIsOfferedUnlessTheWeakestGateAsksPhishingResistanceAsync(
        bool weakestAsks)
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.PhoneCode);
        Declares(
            new Gate(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5)),
            new Gate(GateLevel.Aal2, weakestAsks, TimeSpan.FromMinutes(15)),
            Factor.PhoneCode);

        StepUpChallenge offered = await OfferedAsync(subject, Opened(subject));

        if (weakestAsks)
        {
            Assert.Equal(StepUpOutcome.Enrol, offered.Outcome);
            Assert.Empty(offered.Combinations);
        }
        else
        {
            Assert.Equal(StepUpOutcome.Present, offered.Outcome);
            Assert.Equal([Named(Factor.Password, Factor.PhoneCode)], Named(offered));
        }

        Assert.Equal((AssuranceLevel.Aal2, weakestAsks), (offered.Required, offered.PhishingResistant));
    }

    /// <summary>
    /// AUTH-STEP-002 AC4b: at a step-up whose weakest gate asks <c>aal2</c> with
    /// phishing resistance, a password with a security key and a passkey alone are
    /// offered, and a password with a generated code is not; where one gate asks it and
    /// the weakest does not, the password with a generated code is offered beside them.
    /// </summary>
    /// <param name="weakestAsks">Whether every gate of the policy asks phishing resistance.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AUTH_STEP_002_AC4b_AtAStepUpOnlyPhishingResistantCombinationsAreOfferedWhereTheWeakestGateAsksItAsync(
        bool weakestAsks)
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Holds(subject, Factor.SecurityKey);
        Holds(subject, Factor.Passkey);
        Declares(
            new Gate(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5)),
            new Gate(GateLevel.Aal2, weakestAsks, TimeSpan.FromMinutes(15)));

        string[] offered = Named(await OfferedAsync(subject, Opened(subject)));

        Assert.Contains(Named(Factor.Password, Factor.SecurityKey), offered);
        Assert.Contains(Named(Factor.Passkey), offered);
        Assert.DoesNotContain(Named(Factor.Password), offered);
        Assert.Equal(!weakestAsks, offered.Contains(Named(Factor.Password, Factor.Totp)));
    }

    /// <summary>
    /// AUTH-STEP-002 AC4f: where one gate of the policy asks phishing resistance and
    /// another does not, a step-up on an account holding a password and a generator
    /// offers the password with a generated code, goes on asking after the password, and
    /// presenting both writes <c>aal2</c> without phishing resistance into the session
    /// and ends the step-up, no combination still offered being left to complete.
    /// </summary>
    [Fact]
    public async Task AUTH_STEP_002_AC4f_WhereOneGateAsksPhishingResistanceAPasswordAndAGeneratedCodeAreOfferedAndWriteAal2Async()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Declares(
            new Gate(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5)),
            new Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(15)));

        SessionId held = Opened(subject);
        Session session = Assert.IsType<Session>(await _live.FindAsync(held, TestContext.Current.CancellationToken));
        SignInChallenge began = await BeganAsync(Address);
        string generated = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);

        string[] offered = Named(await OfferedAsync(subject, held));
        SignInProgress first = Reached(await SteppedAsync(new FactorPresentation(Factor.Password) { Value = Secret }));
        SignInProgress second = Reached(await SteppedAsync(new FactorPresentation(Factor.Totp) { Value = generated }));

        Assert.Equal([Named(Factor.Password, Factor.Totp)], offered);
        Assert.Equal((SignInStatus.FactorRequired, AssuranceLevel.Aal1), (first.Status, first.AssuranceLevel));
        Assert.Equal([Factor.Totp], first.Required);
        Assert.Equal(
            (SignInStatus.Complete, AssuranceLevel.Aal2, false),
            (second.Status, second.AssuranceLevel, second.PhishingResistant));
        Assert.Empty(second.Required);
        Assert.Equal(AssuranceLevel.Aal2, session.Attained);
        Assert.Equal(_clock.GetUtcNow(), session.Aal2At);
        Assert.Null(session.PhishingResistantAt);
        Assert.Empty(_audit.StepUpsFailed);
        Assert.False(_work.Open);

        ValueTask<Result<SignInProgress>> SteppedAsync(FactorPresentation presented) =>
            Service.StepUpAsync(
                AccessContext.Of(subject),
                held,
                began.Challenge,
                presented,
                Source,
                TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// AUTH-STEP-002 AC9: a factor the policy in force does not permit is in no
    /// combination a gate offers, though the account holds it and it would reach the
    /// gate, and presenting it is refused before it is verified, right or wrong, and
    /// counted.
    /// </summary>
    [Fact]
    public async Task AUTH_STEP_002_AC9_AFactorThePolicyDoesNotPermitIsNotOfferedAndIsRefusedWhenPresentedAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Declares(
            new Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(15)),
            new Gate(GateLevel.Aal1, PhishingResistant: false, TimeSpan.FromMinutes(15)));

        SessionId held = Opened(subject);
        string[] permitted = Named(await OfferedAsync(subject, held));

        _configuration.Set(
            Settings.PolicyDefault,
            Janus.Core.Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>(
                    Janus.Core.Policies.SystemDefault.LoginFactors.Where(factor => factor != Factor.Totp)),
                Gates = Janus.Core.Policies.SystemDefault.Gates.ToDictionary(
                    gate => gate.Key,
                    gate => gate.Key is StepUpAction.AccountSuspend
                        ? new Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(15))
                        : new Gate(GateLevel.Aal1, PhishingResistant: false, TimeSpan.FromMinutes(15))),
            });

        string[] offered = Named(await OfferedAsync(subject, held));
        string generated = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);

        Result<SignInProgress> right = await SteppedAsync(generated);
        Result<SignInProgress> wrong = await SteppedAsync("000000");

        Assert.Contains(Named(Factor.Password, Factor.Totp), permitted);
        Assert.Equal([Named(Factor.Password)], offered);
        Assert.Equal(ErrorCodes.FactorNotPermitted, Refused(right));
        Assert.Equal(ErrorCodes.FactorNotPermitted, Refused(wrong));
        Assert.Equal([Factor.Totp, Factor.Totp], _audit.StepUpsFailed.Select(failed => failed.Presented));
        Assert.Equal(2, _throttle.Counted.Count);
        Assert.False(_work.Open);

        async ValueTask<Result<SignInProgress>> SteppedAsync(string code) =>
            await Service.StepUpAsync(
                AccessContext.Of(subject),
                held,
                (await BeganAsync(Address)).Challenge,
                new FactorPresentation(Factor.Totp) { Value = code },
                Source,
                TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// AUTH-STEP-002 AC6: at a step-up every combination that reaches the weakest of
    /// the policy's gates, field by field, is offered, and none that does not: the
    /// lowest level any gate asks, phishing resistance only where every gate asks it,
    /// whichever gate states each.
    /// </summary>
    /// <param name="lowest">The lowest level a gate of the policy asks.</param>
    /// <param name="alone">Whether a password alone is offered.</param>
    [Theory]
    [InlineData(GateLevel.Aal1, true)]
    [InlineData(GateLevel.Aal2, false)]
    public async Task AUTH_STEP_002_AC6_AtAStepUpEveryCombinationThatReachesTheWeakestGateFieldByFieldIsOfferedAndNoOtherAsync(
        GateLevel lowest,
        bool alone)
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Declares(
            new Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(30)),
            new Gate(lowest, PhishingResistant: true, TimeSpan.FromMinutes(5)));

        StepUpChallenge offered = await OfferedAsync(subject, Opened(subject));

        string[] expected = alone
            ? [Named(Factor.Password), Named(Factor.Password, Factor.Totp)]
            : [Named(Factor.Password, Factor.Totp)];

        Assert.Equal(StepUpOutcome.Present, offered.Outcome);
        Assert.Equal(expected, Named(offered));
        Assert.Equal(
            (lowest is GateLevel.Aal1 ? AssuranceLevel.Aal1 : AssuranceLevel.Aal2, false, TimeSpan.FromMinutes(30)),
            (offered.Required, offered.PhishingResistant, offered.MaximumAge));
    }

    /// <summary>
    /// AUTH-STEP-002 step 2, `09` `POST /auth/step-up`: a step-up goes on answering
    /// <c>factorRequired</c> until the strictest of the policy's gates is reached,
    /// though the factor accepted already reaches the weakest of them, and answers
    /// <c>complete</c> once it is.
    /// </summary>
    [Fact]
    public async Task AUTH_STEP_002_AStepUpAnswersFactorRequiredUntilTheStrictestOfThePolicysGatesIsReachedAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Declares(
            new Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(5)),
            new Gate(GateLevel.Aal1, PhishingResistant: false, TimeSpan.FromMinutes(30)));

        SessionId held = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);
        string generated = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);

        SignInProgress first = Reached(await SteppedAsync(new FactorPresentation(Factor.Password) { Value = Secret }));
        Error? weakest = await PassedAsync(StepUpAction.AccountDelete);
        Error? strictest = await PassedAsync(StepUpAction.AccountSuspend);
        SignInProgress second = Reached(await SteppedAsync(new FactorPresentation(Factor.Totp) { Value = generated }));

        Assert.Equal((SignInStatus.FactorRequired, AssuranceLevel.Aal1), (first.Status, first.AssuranceLevel));
        Assert.Equal([Factor.Totp], first.Required);
        Assert.Null(weakest);
        Assert.Equal(ErrorCodes.StepUpRequired, strictest?.Code);
        Assert.Equal((SignInStatus.Complete, AssuranceLevel.Aal2), (second.Status, second.AssuranceLevel));
        Assert.Null(await PassedAsync(StepUpAction.AccountSuspend));

        ValueTask<Error?> PassedAsync(StepUpAction action) =>
            Guard.PassedAsync(subject, held, action, TestContext.Current.CancellationToken);

        ValueTask<Result<SignInProgress>> SteppedAsync(FactorPresentation presented) =>
            Service.StepUpAsync(
                AccessContext.Of(subject),
                held,
                began.Challenge,
                presented,
                Source,
                TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// AUTH-STEP-002 step 3, `09` `POST /auth/step-up`: a refusal the step-up itself
    /// answers is judged against the weakest of the policy's gates, field by field, and
    /// carries that gate's values: an account that reaches it and cannot present it is
    /// told to report the loss, where the strictest gate, which asks phishing
    /// resistance the account never held, would have told it to enrol.
    /// </summary>
    [Fact]
    public async Task AUTH_STEP_002_ARefusalOfAStepUpIsJudgedAgainstAndCarriesTheWeakestOfThePolicysGatesAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.PhoneCode);
        Answers(PhoneSignal.Risk);
        Declares(
            new Gate(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5)),
            new Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(30)),
            Factor.PhoneCode);

        SignInChallenge began = await BeganAsync(Address);

        Error refused = Refusal(await AskedAsync(began.Challenge, subject, Opened(subject)));

        Assert.Equal(ErrorCodes.StepUpRequired, refused.Code);
        Assert.Equal("report-loss", refused.Details["outcome"].GetString());
        Assert.Equal(
            ("aal2", false, 1800L),
            (
                refused.Details["required"].GetProperty("level").GetString(),
                refused.Details["required"].GetProperty("phishingResistant").GetBoolean(),
                refused.Details["required"].GetProperty("maxAge").GetInt64()));
        Assert.Empty(refused.Details["options"].Deserialize<string[][]>()!);
        Assert.Empty(_notifications.Texts);
    }

    /// <summary>
    /// AUTH-FACT-002 AC7, `09` `POST /auth/step-up`: a text code asked for at a step-up
    /// whose number answers <c>risk</c>, leaving no combination to complete, is answered
    /// <c>complete</c> where the factors accepted on the challenge reach the weakest of
    /// the policy's gates, reporting what they reach, with nothing sent.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC7_AReportedChangeAtAStepUpLeavingNoCombinationIsCompleteWhereTheAcceptedFactorsReachTheWeakestGateAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.PhoneCode);
        Declares(
            new Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(5)),
            new Gate(GateLevel.Aal1, PhishingResistant: false, TimeSpan.FromMinutes(30)),
            Factor.PhoneCode);

        SessionId held = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);
        SignInProgress presented = Reached(await Service.StepUpAsync(
            AccessContext.Of(subject),
            held,
            began.Challenge,
            new FactorPresentation(Factor.Password) { Value = Secret },
            Source,
            TestContext.Current.CancellationToken));

        Answers(PhoneSignal.Risk);

        SignInProgress? left = (await AskedAsync(began.Challenge, subject, held)).Match(
            progress => progress,
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        Assert.Equal(SignInStatus.FactorRequired, presented.Status);
        Assert.Equal([Factor.PhoneCode], presented.Required);
        Assert.Equal(SignInStatus.Complete, left?.Status);
        Assert.Empty(Assert.IsType<SignInProgress>(left).Required);
        Assert.Equal((AssuranceLevel.Aal1, false), (left?.AssuranceLevel, left?.PhishingResistant));
        Assert.Null(left?.Session);
        Assert.Empty(_notifications.Texts);
    }

    /// <summary>
    /// AUTH-STEP-002 step 2: a step-up no combination still offered can be completed at
    /// is answered <c>complete</c> with what the factors accepted reach, short of the
    /// gate, and its challenge ends.
    /// </summary>
    [Fact]
    public async Task AUTH_STEP_002_AStepUpNoCombinationCanCompleteIsAnsweredCompleteWithWhatWasReachedAndEndsAsync()
    {
        SubjectId subject = await AccountAsync();

        Declares(new Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(5)));

        SessionId held = Opened(subject, AssuranceLevel.Aal2);
        SignInChallenge began = await BeganAsync(Address);

        _clock.Advance(TimeSpan.FromMinutes(6));

        Result<SignInProgress> first = await SteppedAsync();
        Result<SignInProgress> again = await SteppedAsync();

        Assert.Equal(
            (SignInStatus.Complete, AssuranceLevel.Aal1, false),
            (Reached(first).Status, Reached(first).AssuranceLevel, Reached(first).PhishingResistant));
        Assert.Empty(Reached(first).Required);
        Assert.Equal(
            ErrorCodes.StepUpRequired,
            (await Guard.PassedAsync(subject, held, StepUpAction.AccountSuspend, TestContext.Current.CancellationToken))?.Code);
        Assert.Equal(ErrorCodes.FactorRejected, Refused(again));

        ValueTask<Result<SignInProgress>> SteppedAsync() =>
            Service.StepUpAsync(
                AccessContext.Of(subject),
                held,
                began.Challenge,
                new FactorPresentation(Factor.Password) { Value = Secret },
                Source,
                TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// AUTH-STEP-002 step 2: only a call that presents a factor writes the session, so a
    /// text code asked for at a step-up, which presents none, leaves every instant of
    /// the session as it was.
    /// </summary>
    [Fact]
    public async Task AUTH_STEP_002_ATextCodeAskedForAtAStepUpWritesNothingIntoTheSessionAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);

        SessionId held = Opened(subject);
        Session session = Assert.IsType<Session>(await _live.FindAsync(held, TestContext.Current.CancellationToken));
        (DateTimeOffset, DateTimeOffset?, DateTimeOffset?, DateTimeOffset?, DateTimeOffset?) before =
            (session.DelegatedAt, session.Aal1At, session.Aal2At, session.Aal3At, session.PhishingResistantAt);
        SignInChallenge began = await BeganAsync(Address);

        _clock.Advance(TimeSpan.FromMinutes(1));

        SignInProgress? asked = (await AskedAsync(began.Challenge, subject, held)).Match(
            progress => progress,
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        Assert.Null(asked);
        _ = Assert.Single(_notifications.Texts);
        Assert.Equal(
            before,
            (session.DelegatedAt, session.Aal1At, session.Aal2At, session.Aal3At, session.PhishingResistantAt));
    }

    /// <summary>
    /// AUTH-STEP-002 AC4e: a text code asked for at a step-up and answered 200, under a
    /// session that reached <c>aal2</c> earlier and with no factor yet accepted on the
    /// challenge, reports <c>delegated</c>, not phishing-resistant, and writes nothing
    /// into the session record.
    /// </summary>
    [Fact]
    public async Task AUTH_STEP_002_AC4e_ATextCodeAskWithNoFactorAcceptedReportsDelegatedAndWritesNothingAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Holds(subject, Factor.Totp);
        Answers(PhoneSignal.Risk);

        SessionId held = Opened(subject, AssuranceLevel.Aal2);
        Session session = Assert.IsType<Session>(await _live.FindAsync(held, TestContext.Current.CancellationToken));
        DateTimeOffset?[] before = Instants(session);

        _clock.Advance(TimeSpan.FromHours(2));

        SignInChallenge began = await BeganAsync(Address);
        SignInProgress asked = Assert.IsType<SignInProgress>((await AskedAsync(began.Challenge, subject, held)).Match(
            progress => progress,
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString())));

        Assert.Equal(
            (SignInStatus.FactorRequired, AssuranceLevel.Delegated, false),
            (asked.Status, asked.AssuranceLevel, asked.PhishingResistant));
        Assert.Equal([Factor.Password, Factor.Totp], asked.Required);
        Assert.Equal(AssuranceLevel.Aal2, session.Attained);
        Assert.Equal(before, Instants(session));
        Assert.Empty(_notifications.Texts);
    }

    /// <summary>
    /// AUTH-STEP-002 step 2, chapter 09 <c>POST /auth/step-up</c>: a text code asked
    /// for after a password was accepted on the challenge of the step-up, and answered
    /// 200, reports what the factors accepted reach together, which is <c>aal1</c>,
    /// under a session that reached <c>aal2</c> earlier.
    /// </summary>
    [Fact]
    public async Task AUTH_STEP_002_ATextCodeAskAfterAPasswordReportsWhatTheFactorsAcceptedReachAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Holds(subject, Factor.Totp);
        Answers(PhoneSignal.Risk);

        SessionId held = Opened(subject, AssuranceLevel.Aal2);

        _clock.Advance(TimeSpan.FromHours(2));

        SignInChallenge began = await BeganAsync(Address);
        Result<SignInProgress> presented = await Service.StepUpAsync(
            AccessContext.Of(subject),
            held,
            began.Challenge,
            new FactorPresentation(Factor.Password) { Value = Secret },
            Source,
            TestContext.Current.CancellationToken);
        SignInProgress asked = Assert.IsType<SignInProgress>((await AskedAsync(began.Challenge, subject, held)).Match(
            progress => progress,
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString())));

        Assert.Null(Refused(presented));
        Assert.Equal(
            (SignInStatus.FactorRequired, AssuranceLevel.Aal1, false),
            (asked.Status, asked.AssuranceLevel, asked.PhishingResistant));
    }

    /// <summary>
    /// AUTH-STEP-002 step 2: a second factor presented alone at a step-up reaches no
    /// level, so it writes no instant into the session record, that of
    /// <c>delegated</c> included, and its 200 reports <c>delegated</c>, not
    /// phishing-resistant, whatever the session reached earlier.
    /// </summary>
    [Fact]
    public async Task AUTH_STEP_002_ASecondFactorAloneAtAStepUpWritesNoInstantAndReportsDelegatedAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Declares(new Gate(GateLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(5)));

        SessionId held = Opened(subject, AssuranceLevel.Aal2);
        Session session = Assert.IsType<Session>(await _live.FindAsync(held, TestContext.Current.CancellationToken));
        DateTimeOffset?[] before = Instants(session);

        _clock.Advance(TimeSpan.FromMinutes(6));

        SignInChallenge began = await BeganAsync(Address);
        SignInProgress answered = Reached(await Service.StepUpAsync(
            AccessContext.Of(subject),
            held,
            began.Challenge,
            new FactorPresentation(Factor.Totp)
            {
                Value = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
                    .ComputeTotp(_clock.GetUtcNow().UtcDateTime),
            },
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            (SignInStatus.FactorRequired, AssuranceLevel.Delegated, false),
            (answered.Status, answered.AssuranceLevel, answered.PhishingResistant));
        Assert.Equal([Factor.Password], answered.Required);
        Assert.Equal(before, Instants(session));
        Assert.Equal(
            ErrorCodes.StepUpRequired,
            (await Guard.PassedAsync(subject, held, StepUpAction.AccountSuspend, TestContext.Current.CancellationToken))?.Code);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10, AUTH-FACT-004: a wrong try at a sign-in code is decided in
    /// its caller's unit of work, which it leaves open with the count written on the
    /// code's record for the caller to commit.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_AWrongSignInCodeIsCountedInItsCallersUnitOfWorkAsync()
    {
        SubjectId subject = await AccountAsync();
        PendingSignIn held = await CodeSentAsync(subject);

        _work.Reset();

        Result wrong = await Links.SpendCodeAsync(held, Other(Code()), TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.CodeInvalid, wrong.Match(() => (ErrorCode?)null, error => error.Code));
        Assert.Equal((0, 0, 0), (_work.Opened, _work.Committed, _work.RolledBack));
        Assert.Equal(
            1,
            (await _pending.FindAsync(subject, Factor.EmailCode, TestContext.Current.CancellationToken))?.WrongAttempts);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10: a sign-in code found gone under its lock is refused as
    /// expired having written nothing, and the refusal neither begins nor ends a unit of
    /// work, so its caller may still commit the kept writes it makes for it.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_ASignInCodeGoneUnderItsLockWritesNothingAsync()
    {
        SubjectId subject = await AccountAsync();
        PendingSignIn held = await CodeSentAsync(subject);

        await _pending.RemoveAsync(held.Fingerprint, TestContext.Current.CancellationToken);
        _work.Reset();

        Result gone = await Links.SpendCodeAsync(held, Code(), TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.CodeExpired, gone.Match(() => (ErrorCode?)null, error => error.Code));
        Assert.Equal((0, 0, 0), (_work.Opened, _work.Committed, _work.RolledBack));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10, AUTH-FACT-004: a wrong sign-in code commits its count on the
    /// code's record, the failed authentication's record and the failure's counts in one
    /// unit of work, and the try that reaches the cap commits the code's removal the
    /// same way.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_AWrongSignInCodeCommitsItsKeptWritesTogetherAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailCode);
        Remembered(subject);
        AuthenticationCodeKeys();

        SignInChallenge began = await BeganAsync(Address);

        _ = await Service.SendCodeAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        string right = Code();

        _work.Reset();

        Assert.Equal(ErrorCodes.CodeInvalid, Refused(await PresentAsync(began.Challenge, Factor.EmailCode, Other(right))));
        Assert.False(_work.Open);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.Equal(
            1,
            (await _pending.FindAsync(subject, Factor.EmailCode, TestContext.Current.CancellationToken))?.WrongAttempts);
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.EmailCode)], _audit.Failed);
        Assert.Contains(_throttle.Counted, counter => counter.Scope is ThrottleScope.Source);

        _work.Reset();

        Assert.Equal(ErrorCodes.CodeInvalid, Refused(await PresentAsync(began.Challenge, Factor.EmailCode, Other(right))));
        Assert.False(_work.Open);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.Null(await _pending.FindAsync(subject, Factor.EmailCode, TestContext.Current.CancellationToken));
        Assert.Equal(2, _audit.Failed.Count);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10, AUTH-FACT-004: a sign-in code presented past its lifetime
    /// changes nothing on the code's record and commits the failed authentication's
    /// record and the failure's counts alone.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_ASignInCodePastItsLifetimeCommitsOnlyItsRecordAndItsCountsAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailCode);
        Remembered(subject);
        AuthenticationCodeKeys();

        SignInChallenge began = await BeganAsync(Address);

        _ = await Service.SendCodeAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        string right = Code();

        _clock.Advance(TimeSpan.FromMinutes(4));
        _work.Reset();

        Assert.Equal(ErrorCodes.CodeExpired, Refused(await PresentAsync(began.Challenge, Factor.EmailCode, right)));
        Assert.False(_work.Open);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.Equal(
            0,
            (await _pending.FindAsync(subject, Factor.EmailCode, TestContext.Current.CancellationToken))?.WrongAttempts);
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.EmailCode)], _audit.Failed);
        Assert.Contains(_throttle.Counted, counter => counter.Scope is ThrottleScope.Source);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10, AUTH-FACT-004: a right sign-in code whose sign-in a domain
    /// lock then refuses is spent, and its spend is committed alone in the one unit of
    /// work with the refusal, which counts no failure and records no failed
    /// authentication.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_ARightSignInCodeADomainLockRefusesCommitsItsSpendAloneAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailCode);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _ = await Service.SendCodeAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        string right = Code();

        LockedElsewhere(subject);
        _work.Reset();

        Result<SignInProgress> refused = await PresentAsync(began.Challenge, Factor.EmailCode, right);

        Assert.Equal(ErrorCodes.IdentifierDomainNotAllowed, Refused(refused));
        Assert.False(_work.Open);
        Assert.Equal((1, 1, 0), (_work.Opened, _work.OutermostCommitted, _work.RolledBack));
        Assert.Null(await _pending.FindAsync(subject, Factor.EmailCode, TestContext.Current.CancellationToken));
        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
        Assert.Empty(await _live.LiveOfAsync(subject, _clock.GetUtcNow(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// REG-DOM-001 AC14, AUTH-FACT-004: at a step-up a right email code sent to an address the
    /// domain lock now refuses is answered <c>identity.identifier.domainnotallowed</c>.
    /// The lock is judged after the code against the address the code was sent to, the
    /// spend is committed alone, no failure is counted, no refused step-up factor is
    /// recorded, and the session is not raised.
    /// </summary>
    [Fact]
    public async Task REG_DOM_001_AC14_ARightEmailCodeAtAStepUpToAnAddressTheLockRefusesIsSpentAndCountsNoFailureAsync()
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.EmailCode);
        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);
        _ = await Service.SendCodeAsync(Address, Language, Source, TestContext.Current.CancellationToken);
        string right = Code();
        LockedElsewhere(subject);
        _work.Reset();

        ErrorCode? refused = Refused(await Service.StepUpAsync(
            AccessContext.Of(subject),
            session,
            began.Challenge,
            new FactorPresentation(Factor.EmailCode) { Value = right },
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.IdentifierDomainNotAllowed, refused);
        Assert.False(_work.Open);
        Assert.Equal((1, 1, 0), (_work.Opened, _work.OutermostCommitted, _work.RolledBack));
        Assert.Null(await _pending.FindAsync(subject, Factor.EmailCode, TestContext.Current.CancellationToken));
        Assert.Empty(_audit.StepUpsFailed);
        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
        Assert.Null((await _live.FindAsync(session, TestContext.Current.CancellationToken))?.EndedAt);
    }

    /// <summary>
    /// REG-DOM-001, AUTH-FACT-004: at a step-up the lock is judged after the code, so a
    /// wrong email code presented under a lock is refused as a wrong code is anywhere, a
    /// refused step-up factor recorded, and learns nothing of the lock.
    /// </summary>
    [Fact]
    public async Task REG_DOM_001_AWrongEmailCodeAtAStepUpUnderADomainLockLearnsNothingOfTheLockAsync()
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.EmailCode);
        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);
        _ = await Service.SendCodeAsync(Address, Language, Source, TestContext.Current.CancellationToken);
        string right = Code();
        LockedElsewhere(subject);

        ErrorCode? refused = Refused(await Service.StepUpAsync(
            AccessContext.Of(subject),
            session,
            began.Challenge,
            new FactorPresentation(Factor.EmailCode) { Value = Other(right) },
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.CodeInvalid, refused);
        Assert.Equal(
            1,
            (await _pending.FindAsync(subject, Factor.EmailCode, TestContext.Current.CancellationToken))?.WrongAttempts);
        Assert.Equal([Factor.EmailCode], _audit.StepUpsFailed.Select(failed => failed.Presented));
    }

    /// <summary>
    /// AUTH-FACT-004: the domain lock is judged after the code, never before it, so a
    /// wrong code presented under a lock is told what a wrong code is told anywhere and
    /// learns nothing of the lock.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AWrongSignInCodeUnderADomainLockLearnsNothingOfTheLockAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailCode);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _ = await Service.SendCodeAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        string right = Code();

        LockedElsewhere(subject);

        Result<SignInProgress> refused = await PresentAsync(began.Challenge, Factor.EmailCode, Other(right));

        Assert.Equal(ErrorCodes.CodeInvalid, Refused(refused));
        Assert.Equal(
            1,
            (await _pending.FindAsync(subject, Factor.EmailCode, TestContext.Current.CancellationToken))?.WrongAttempts);
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.EmailCode)], _audit.Failed);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10, REG-IDENT-006 AC6: a right sign-in code sent to an address
    /// the account has given up since is spent and refused as a wrong factor is, the
    /// spend, the failed authentication's record and the failure's counts committed in
    /// one unit of work.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_ARightSignInCodeToAnAddressGivenUpCommitsItsSpendWithTheRefusalAsync()
    {
        SubjectId subject = await AccountAsync();
        IdentifierId second = _identifiers.Verified(subject, IdentifierKind.Email, Second);

        Enables(Factor.EmailCode);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _ = await Service.SendCodeAsync(Second, Language, Source, TestContext.Current.CancellationToken);

        string right = Code();

        await RemovedAsync(subject, second);
        _work.Reset();

        Result<SignInProgress> refused = await PresentAsync(began.Challenge, Factor.EmailCode, right);

        Assert.Equal(ErrorCodes.FactorRejected, Refused(refused));
        Assert.False(_work.Open);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.Null(await _pending.FindAsync(subject, Factor.EmailCode, TestContext.Current.CancellationToken));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.EmailCode)], _audit.Failed);
        Assert.Contains(_throttle.Counted, counter => counter.Scope is ThrottleScope.Source);
    }

    /// <summary>
    /// AUTH-ABUSE-001, CONV-LOG-005: a fault of the library's own inside a sign-in (a
    /// setting that does not read once the password has verified) is no failed attempt:
    /// the failure comes back as it is, nothing is counted and nothing is recorded,
    /// while a wrong password under the same fault is refused, recorded and counted.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AFaultInsideASignInIsNeitherCountedNorRecordedAsync()
    {
        SubjectId subject = await AccountAsync();

        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _configuration.Unread = Settings.PasswordArgon2Parallelism.Key;
        _work.Reset();

        Result<SignInProgress> faulted = await PresentAsync(began.Challenge, Factor.Password, Secret);

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, Refused(faulted));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);

        Result<SignInProgress> wrong = await PresentAsync(began.Challenge, Factor.Password, "not the password at all");

        Assert.Equal(ErrorCodes.FactorRejected, Refused(wrong));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.Password)], _audit.Failed);
        Assert.NotEmpty(_throttle.Counted);
    }

    /// <summary>
    /// AUTH-ABUSE-001, CONV-LOG-005, CONV-DESIGN-003 AC10: a fault of the library's own
    /// inside a step-up's presentation is no refused step-up factor: nothing is counted
    /// and nothing is recorded.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AFaultInsideAStepUpIsNeitherCountedNorRecordedAsync()
    {
        SubjectId subject = await AccountAsync();

        Remembered(subject);

        SignInProgress signedIn = await SignedInAsync(subject, Factor.Password, Secret);
        SignInChallenge began = await BeganAsync(Address);

        _configuration.Unread = Settings.PasswordArgon2Parallelism.Key;

        Result<SignInProgress> faulted = await Service.StepUpAsync(
            AccessContext.Of(subject),
            Assert.IsType<SessionId>(signedIn.Session),
            began.Challenge,
            new FactorPresentation(Factor.Password) { Value = Secret },
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, Refused(faulted));
        Assert.False(_work.Open);
        Assert.Empty(_audit.StepUpsFailed);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC2: at a sign-in that still asks a second step once its first
    /// factor is accepted, a suspended generator is judged as an active one would be.
    /// The code it gives is refused <c>auth.credential.suspended</c>, a code it does not
    /// give is refused as any wrong code is, and each is a failed attempt, recorded and
    /// counted.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_007_AC2_ASuspendedGeneratorsCodeIsRefusedSuspendedAndCountedAtASignInAsync()
    {
        SubjectId subject = await AccountAsync();
        Holds(subject, Factor.Totp);
        Holds(subject, Factor.SecurityKey);
        Suspends(subject, Factor.Totp);
        Remembered(subject);
        string generated = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);
        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        ErrorCode? wrong = Refused(await PresentAsync(began.Challenge, Factor.Totp, Other(generated)));
        ErrorCode? right = Refused(await PresentAsync(began.Challenge, Factor.Totp, generated));

        Assert.Equal(ErrorCodes.CodeInvalid, wrong);
        Assert.Equal(ErrorCodes.CredentialSuspended, right);
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.Totp), (subject, Factor.Totp)], _audit.Failed);
        Assert.Equal(
            2,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC2: at a gate a suspended generator's own code is refused
    /// <c>auth.credential.suspended</c>, a refused step-up factor recorded and counted,
    /// and the session is not raised.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_007_AC2_ASuspendedGeneratorsCodeIsRefusedSuspendedAndCountedAtAStepUpAsync()
    {
        SubjectId subject = await AccountAsync();
        Holds(subject, Factor.Totp);
        Suspends(subject, Factor.Totp);
        SessionId session = Opened(subject);
        string generated = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);
        SignInChallenge began = await BeganAsync(Address);

        ErrorCode? refused = Refused(await Service.StepUpAsync(
            AccessContext.Of(subject),
            session,
            began.Challenge,
            new FactorPresentation(Factor.Totp) { Value = generated },
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.CredentialSuspended, refused);
        Assert.Equal([Factor.Totp], _audit.StepUpsFailed.Select(failed => failed.Presented));
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Equal(
            AssuranceLevel.Aal1,
            (await _live.FindAsync(session, TestContext.Current.CancellationToken))?.Attained);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC2: a suspended passkey's assertion is refused
    /// <c>auth.credential.suspended</c> only where its signature verifies, writing
    /// neither its counter nor its use; an assertion another key signed is refused as any
    /// wrong presentation is. Each is a failed attempt.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_007_AC2_ASuspendedPasskeysAssertionIsRefusedSuspendedOnlyWhereItVerifiesAsync()
    {
        SubjectId subject = await AccountAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var forger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        AuthenticatorId held = Keyed(subject, key, counter: 9);
        Suspends(subject, Factor.Passkey);
        Remembered(subject);
        SignInChallenge began = await BeganAsync(Address);

        ErrorCode? forged = Refused(await Service.PresentAsync(
            began.Challenge,
            new FactorPresentation(Factor.Passkey) { Assertion = Asserted(forger, began.WebAuthn.Challenge, counter: 10) },
            Browser,
            Source,
            TestContext.Current.CancellationToken));
        ErrorCode? verified = Refused(await Service.PresentAsync(
            began.Challenge,
            new FactorPresentation(Factor.Passkey) { Assertion = Asserted(key, began.WebAuthn.Challenge, counter: 10) },
            Browser,
            Source,
            TestContext.Current.CancellationToken));

        Authenticator? after = await _authenticators.FindAsync(held, TestContext.Current.CancellationToken);
        Assert.Equal(ErrorCodes.FactorRejected, forged);
        Assert.Equal(ErrorCodes.CredentialSuspended, verified);
        Assert.Equal((9u, null), (after?.WebAuthn?.Counter, after?.LastUsedAt));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.Passkey), (subject, Factor.Passkey)], _audit.Failed);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC9, AUTH-FACT-014 AC3: at a sign-in a suspended passkey whose
    /// assertion verifies and whose counter equals the stored one is refused
    /// <c>auth.webauthn.countermismatch</c>, not as suspended: the refusal's record, the
    /// failed authentication's record and the failure's count commit together, and the
    /// stored counter is left as it stood.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_007_AC9_ASuspendedPasskeyWhoseCounterDidNotAdvanceIsRefusedACounterMismatchAsync()
    {
        SubjectId subject = await AccountAsync();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        AuthenticatorId held = Keyed(subject, key, counter: 9);
        Suspends(subject, Factor.Passkey);
        Remembered(subject);
        SignInChallenge began = await BeganAsync(Address);
        _work.Reset();

        ErrorCode? refused = Refused(await Service.PresentAsync(
            began.Challenge,
            new FactorPresentation(Factor.Passkey) { Assertion = Asserted(key, began.WebAuthn.Challenge, counter: 9) },
            Browser,
            Source,
            TestContext.Current.CancellationToken));

        (AuditAction action, SubjectId audited, AuthenticatorId named) = Assert.Single(_credentials.Records);
        Authenticator? after = await _authenticators.FindAsync(held, TestContext.Current.CancellationToken);
        Assert.Equal(ErrorCodes.WebAuthnCounterMismatch, refused);
        Assert.Equal(("auth.credential.countermismatch", subject, held), (action.ToString(), audited, named));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.Passkey)], _audit.Failed);
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Equal((9u, null), (after?.WebAuthn?.Counter, after?.LastUsedAt));
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC2: a suspended passkey of another account, answering this
    /// sign-in with an assertion that verifies, is refused as any wrong credential is, so
    /// its state is told to nobody but the account that holds it.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_007_AC2_ASuspendedPasskeyOfAnotherAccountIsRefusedAsAnyWrongCredentialIsAsync()
    {
        SubjectId subject = await AccountAsync();
        var other = new SubjectId(Guid.NewGuid());
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        _ = Keyed(other, key, counter: 9);
        Suspends(other, Factor.Passkey);
        Remembered(subject);
        SignInChallenge began = await BeganAsync(Address);

        ErrorCode? refused = Refused(await Service.PresentAsync(
            began.Challenge,
            new FactorPresentation(Factor.Passkey) { Assertion = Asserted(key, began.WebAuthn.Challenge, counter: 10) },
            Browser,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.FactorRejected, refused);
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.Passkey)], _audit.Failed);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC2, AUTH-FACT-004: a text code sent to a number suspended since is
    /// judged as an active one's would be. The wrong code is refused as any wrong code
    /// is; the right one is spent and refused <c>auth.credential.suspended</c>, the spend
    /// kept with the refused step-up factor's record and count.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_007_AC2_ATextCodeOfANumberSuspendedSinceIsSpentAndRefusedSuspendedAsync()
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);
        _ = await AskedAsync(began.Challenge, subject, session);
        string code = Assert.Single(_notifications.Texts).Values["code"];
        Suspends(subject, Factor.PhoneCode);
        _work.Reset();

        ErrorCode? wrong = Refused(await SteppedAsync(Other(code)));
        ErrorCode? right = Refused(await SteppedAsync(code));

        Assert.Equal(ErrorCodes.CodeInvalid, wrong);
        Assert.Equal(ErrorCodes.CredentialSuspended, right);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([Factor.PhoneCode, Factor.PhoneCode], _audit.StepUpsFailed.Select(failed => failed.Presented));
        Assert.Equal(
            2,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.False(_work.Open);

        ValueTask<Result<SignInProgress>> SteppedAsync(string value) =>
            Service.StepUpAsync(
                AccessContext.Of(subject),
                session,
                began.Challenge,
                new FactorPresentation(Factor.PhoneCode) { Value = value },
                Source,
                TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// AUTH-FACT-004 AC7: a text code sent for a credential removed or invalidated since
    /// is judged as any code is. The wrong code is refused as any wrong code is; the
    /// right one is spent and refused <c>auth.factor.rejected</c>, the spend kept with
    /// the refused step-up factor's record and count, and the session is not raised.
    /// </summary>
    /// <param name="removed">Whether the credential was removed, or invalidated.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AUTH_FACT_004_AC7_ARightTextCodeWhoseCredentialIsGoneIsSpentRefusedAndCountedAtAStepUpAsync(
        bool removed)
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);
        _ = await AskedAsync(began.Challenge, subject, session);
        string code = Assert.Single(_notifications.Texts).Values["code"];
        await GoneAsync(subject, Factor.PhoneCode, removed);
        _work.Reset();

        ErrorCode? wrong = Refused(await SteppedAsync(Other(code)));
        ErrorCode? right = Refused(await SteppedAsync(code));

        Assert.Equal(ErrorCodes.CodeInvalid, wrong);
        Assert.Equal(ErrorCodes.FactorRejected, right);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([Factor.PhoneCode, Factor.PhoneCode], _audit.StepUpsFailed.Select(failed => failed.Presented));
        Assert.Equal(
            2,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Equal(
            AssuranceLevel.Aal1,
            (await _live.FindAsync(session, TestContext.Current.CancellationToken))?.Attained);
        Assert.Equal((2, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);

        ValueTask<Result<SignInProgress>> SteppedAsync(string value) =>
            Service.StepUpAsync(
                AccessContext.Of(subject),
                session,
                began.Challenge,
                new FactorPresentation(Factor.PhoneCode) { Value = value },
                Source,
                TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// AUTH-FACT-004 AC7, AUTH-RECOV-007 AC2 (D-197): at a step-up, a right text code
    /// whose credential's number left the account since it was sent is refused
    /// <c>auth.factor.rejected</c>, as one sent to an address given up since is, and
    /// not as a suspended credential's is, though the credential stands suspended. It
    /// is spent, the refused factor is recorded and counted, and the session is not
    /// raised.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC7_ARightTextCodeWhoseCredentialsNumberLeftIsSpentRejectedAndCountedAtAStepUpAsync()
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);
        _ = await AskedAsync(began.Challenge, subject, session);
        string code = Assert.Single(_notifications.Texts).Values["code"];
        await UnboundAsync(subject);
        _work.Reset();

        ErrorCode? right = Refused(await Service.StepUpAsync(
            AccessContext.Of(subject),
            session,
            began.Challenge,
            new FactorPresentation(Factor.PhoneCode) { Value = code },
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.FactorRejected, right);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([Factor.PhoneCode], _audit.StepUpsFailed.Select(failed => failed.Presented));
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Equal(
            AssuranceLevel.Aal1,
            (await _live.FindAsync(session, TestContext.Current.CancellationToken))?.Attained);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC2, AUTH-FACT-004 AC7 (D-197): at a sign-in's second step, a
    /// right text code whose credential's number left the account since it was sent is
    /// spent and refused <c>auth.factor.rejected</c>, a failed authentication recorded
    /// and counted, and nobody is signed in.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_007_AC2_ARightTextCodeWhoseCredentialsNumberLeftIsSpentRejectedAndCountedAtASignInAsync()
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Remembered(subject);
        SignInChallenge began = await BeganAsync(Address);
        _ = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));
        _ = await AskedAsync(began.Challenge, stepping: null);
        string code = Assert.Single(_notifications.Texts).Values["code"];
        await UnboundAsync(subject);
        _work.Reset();

        ErrorCode? refused = Refused(await PresentAsync(began.Challenge, Factor.PhoneCode, code));

        Assert.Equal(ErrorCodes.FactorRejected, refused);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.PhoneCode)], _audit.Failed);
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-004 AC7: at a sign-in's second step the right text code of a credential
    /// removed since it was sent is spent and refused <c>auth.factor.rejected</c>, a
    /// failed authentication recorded and counted, and nobody is signed in.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC7_ARightTextCodeWhoseCredentialWasRemovedIsSpentRefusedAndCountedAtASignInAsync()
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Remembered(subject);
        SignInChallenge began = await BeganAsync(Address);
        _ = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));
        _ = await AskedAsync(began.Challenge, stepping: null);
        string code = Assert.Single(_notifications.Texts).Values["code"];
        await GoneAsync(subject, Factor.PhoneCode, removed: true);
        _work.Reset();

        ErrorCode? refused = Refused(await PresentAsync(began.Challenge, Factor.PhoneCode, code));

        Assert.Equal(ErrorCodes.FactorRejected, refused);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.PhoneCode)], _audit.Failed);
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-004 AC7: a text code is judged against the credential it was sent for,
    /// so one enrolled after the code went out does not answer for the one removed: the
    /// right code is spent and refused <c>auth.factor.rejected</c>.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC7_ACredentialEnrolledAfterATextCodeWasSentDoesNotAnswerForTheOneRemovedAsync()
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);
        _ = await AskedAsync(began.Challenge, subject, session);
        string code = Assert.Single(_notifications.Texts).Values["code"];
        await GoneAsync(subject, Factor.PhoneCode, removed: true);
        _clock.Advance(TimeSpan.FromSeconds(1));
        Holds(subject, Factor.PhoneCode);

        ErrorCode? refused = Refused(await Service.StepUpAsync(
            AccessContext.Of(subject),
            session,
            began.Challenge,
            new FactorPresentation(Factor.PhoneCode) { Value = code },
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.FactorRejected, refused);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([Factor.PhoneCode], _audit.StepUpsFailed.Select(failed => failed.Presented));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-004 AC7: a text code is judged against the credential its record names,
    /// whatever other credential of the factor the account holds. Another one, held from
    /// before the code was sent and suspended then, so that the code was issued for the
    /// first alone, is active again when the code is presented and does not answer for
    /// the first, removed or invalidated since: the wrong code is refused as any wrong
    /// code is, and the right one is spent and refused <c>auth.factor.rejected</c>, the
    /// refused step-up factor recorded and counted, and the session is not raised.
    /// </summary>
    /// <param name="removed">Whether the credential was removed, or invalidated.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AUTH_FACT_004_AC7_AnotherCredentialHeldWhenATextCodeWasSentDoesNotAnswerForItsOwnAtAStepUpAsync(
        bool removed)
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.PhoneCode);
        Authenticator named = Held(subject, Factor.PhoneCode);
        Authenticator other = Held(subject, Factor.PhoneCode);
        other.Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);
        _ = await AskedAsync(began.Challenge, subject, session);
        string code = Assert.Single(_notifications.Texts).Values["code"];
        await GoneAsync(named, removed);
        other.Restore();
        _work.Reset();

        ErrorCode? wrong = Refused(await SteppedAsync(Other(code)));
        ErrorCode? right = Refused(await SteppedAsync(code));

        Assert.True(other.IsUsable);
        Assert.Equal(ErrorCodes.CodeInvalid, wrong);
        Assert.Equal(ErrorCodes.FactorRejected, right);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([Factor.PhoneCode, Factor.PhoneCode], _audit.StepUpsFailed.Select(failed => failed.Presented));
        Assert.Equal(
            2,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Equal(
            AssuranceLevel.Aal1,
            (await _live.FindAsync(session, TestContext.Current.CancellationToken))?.Attained);
        Assert.Equal((2, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);

        ValueTask<Result<SignInProgress>> SteppedAsync(string value) =>
            Service.StepUpAsync(
                AccessContext.Of(subject),
                session,
                began.Challenge,
                new FactorPresentation(Factor.PhoneCode) { Value = value },
                Source,
                TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// AUTH-FACT-004 AC7: where the account holds two active credentials of the factor
    /// when a text code is asked for, the code's record names one of them, and the code
    /// is judged against that one alone. Once it is removed or invalidated the right
    /// code is spent and refused <c>auth.factor.rejected</c>, the refused step-up factor
    /// recorded and counted, whatever the other still is.
    /// </summary>
    /// <param name="removed">Whether the credential was removed, or invalidated.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AUTH_FACT_004_AC7_ARightTextCodeIsRefusedOnceTheCredentialItsRecordNamesIsGoneWhateverOtherIsActiveAsync(
        bool removed)
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.PhoneCode);
        Authenticator[] held = [Held(subject, Factor.PhoneCode), Held(subject, Factor.PhoneCode)];
        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);
        _ = await AskedAsync(began.Challenge, subject, session);
        string code = Assert.Single(_notifications.Texts).Values["code"];
        AuthenticatorId? named =
            (await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken))?.Credential;
        Authenticator issuedFor = Assert.Single(held, credential => credential.Id == named);
        await GoneAsync(issuedFor, removed);
        _work.Reset();

        ErrorCode? refused = Refused(await Service.StepUpAsync(
            AccessContext.Of(subject),
            session,
            began.Challenge,
            new FactorPresentation(Factor.PhoneCode) { Value = code },
            Source,
            TestContext.Current.CancellationToken));

        Assert.True(Assert.Single(held, credential => credential.Id != named).IsUsable);
        Assert.Equal(ErrorCodes.FactorRejected, refused);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([Factor.PhoneCode], _audit.StepUpsFailed.Select(failed => failed.Presented));
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Equal(
            AssuranceLevel.Aal1,
            (await _live.FindAsync(session, TestContext.Current.CancellationToken))?.Attained);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-004 AC7: a text code is judged against the credential its record names
    /// and no other, so the removal of another credential of the factor the account
    /// held does not refuse it: the right code is accepted and the session is raised.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC7_ARightTextCodeIsAcceptedWhileTheCredentialItsRecordNamesStandsWhateverOtherIsGoneAsync()
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.PhoneCode);
        Authenticator[] held = [Held(subject, Factor.PhoneCode), Held(subject, Factor.PhoneCode)];
        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);
        _ = await AskedAsync(began.Challenge, subject, session);
        string code = Assert.Single(_notifications.Texts).Values["code"];
        AuthenticatorId? named =
            (await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken))?.Credential;
        await GoneAsync(Assert.Single(held, credential => credential.Id != named), removed: true);

        Result<SignInProgress> stepped = await Service.StepUpAsync(
            AccessContext.Of(subject),
            session,
            began.Challenge,
            new FactorPresentation(Factor.PhoneCode) { Value = code },
            Source,
            TestContext.Current.CancellationToken);

        Assert.Null(Refused(stepped));
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Empty(_audit.StepUpsFailed);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-004 AC7: at a sign-in's second step the right text code whose own
    /// credential was removed or invalidated since it was sent is spent and refused
    /// <c>auth.factor.rejected</c>, a failed authentication recorded and counted, and
    /// nobody is signed in, although another credential of the factor, held from before
    /// the code was sent, is active when it is presented.
    /// </summary>
    /// <param name="removed">Whether the credential was removed, or invalidated.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AUTH_FACT_004_AC7_AnotherCredentialHeldWhenATextCodeWasSentDoesNotAnswerForItsOwnAtASignInAsync(
        bool removed)
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.PhoneCode);
        Authenticator named = Held(subject, Factor.PhoneCode);
        Authenticator other = Held(subject, Factor.PhoneCode);
        other.Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        Remembered(subject);
        SignInChallenge began = await BeganAsync(Address);
        _ = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));
        _ = await AskedAsync(began.Challenge, stepping: null);
        string code = Assert.Single(_notifications.Texts).Values["code"];
        await GoneAsync(named, removed);
        other.Restore();
        _work.Reset();

        ErrorCode? refused = Refused(await PresentAsync(began.Challenge, Factor.PhoneCode, code));

        Assert.True(other.IsUsable);
        Assert.Equal(ErrorCodes.FactorRejected, refused);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.PhoneCode)], _audit.Failed);
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC2, AUTH-FACT-004: the right text code whose own credential is
    /// suspended since it was sent is spent and refused
    /// <c>auth.credential.suspended</c> at a step-up, the refused factor recorded and
    /// counted, although another credential of the factor, held from before the code was
    /// sent, is active when it is presented.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_007_AC2_ATextCodeWhoseOwnCredentialIsSuspendedIsRefusedSuspendedAtAStepUpWhateverElseIsHeldAsync()
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.PhoneCode);
        Authenticator named = Held(subject, Factor.PhoneCode);
        Authenticator other = Held(subject, Factor.PhoneCode);
        other.Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);
        _ = await AskedAsync(began.Challenge, subject, session);
        string code = Assert.Single(_notifications.Texts).Values["code"];
        named.Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        other.Restore();
        _work.Reset();

        ErrorCode? refused = Refused(await Service.StepUpAsync(
            AccessContext.Of(subject),
            session,
            began.Challenge,
            new FactorPresentation(Factor.PhoneCode) { Value = code },
            Source,
            TestContext.Current.CancellationToken));

        Assert.True(other.IsUsable);
        Assert.Equal(ErrorCodes.CredentialSuspended, refused);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([Factor.PhoneCode], _audit.StepUpsFailed.Select(failed => failed.Presented));
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Equal(
            AssuranceLevel.Aal1,
            (await _live.FindAsync(session, TestContext.Current.CancellationToken))?.Attained);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC2, AUTH-FACT-004: at a sign-in's second step the right text code
    /// whose own credential is suspended since it was sent is spent and refused
    /// <c>auth.credential.suspended</c>, a failed authentication recorded and counted,
    /// although another credential of the factor, held from before the code was sent, is
    /// active when it is presented.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_007_AC2_ATextCodeWhoseOwnCredentialIsSuspendedIsRefusedSuspendedAtASignInWhateverElseIsHeldAsync()
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.PhoneCode);
        Authenticator named = Held(subject, Factor.PhoneCode);
        Authenticator other = Held(subject, Factor.PhoneCode);
        other.Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        Remembered(subject);
        SignInChallenge began = await BeganAsync(Address);
        _ = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));
        _ = await AskedAsync(began.Challenge, stepping: null);
        string code = Assert.Single(_notifications.Texts).Values["code"];
        named.Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        other.Restore();
        _work.Reset();

        ErrorCode? refused = Refused(await PresentAsync(began.Challenge, Factor.PhoneCode, code));

        Assert.True(other.IsUsable);
        Assert.Equal(ErrorCodes.CredentialSuspended, refused);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.PhoneCode)], _audit.Failed);
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC8: a text code asked for after a first factor, naming a
    /// suspended number, sends nothing and is refused
    /// <c>auth.credential.suspended</c>. The ask presents no factor, so nothing is
    /// counted and no failed authentication is recorded.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_007_AC8_AnAskAfterAFirstFactorNamingASuspendedNumberSendsNothingAndCountsNothingAsync()
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Holds(subject, Factor.Totp);
        Suspends(subject, Factor.PhoneCode);
        Remembered(subject);
        SignInChallenge began = await BeganAsync(Address);
        SignInProgress reached = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));

        Error refused = Refusal(await AskedAsync(began.Challenge, stepping: null));

        Assert.Equal([Factor.Totp], reached.Required);
        Assert.Equal(ErrorCodes.CredentialSuspended, refused.Code);
        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC8: a text code asked for under a session, naming a suspended
    /// number, sends nothing and is refused <c>auth.credential.suspended</c>, counting
    /// nothing and recording no refused step-up factor.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_007_AC8_AnAskUnderASessionNamingASuspendedNumberSendsNothingAndCountsNothingAsync()
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Suspends(subject, Factor.PhoneCode);
        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);

        Error refused = Refusal(await AskedAsync(began.Challenge, subject, session));

        Assert.Equal(ErrorCodes.CredentialSuspended, refused.Code);
        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Empty(_audit.StepUpsFailed);
        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC8: a text code asked for before a first factor is answered as
    /// every ask is and sends nothing, whatever the account holds, a suspended number
    /// included.
    /// </summary>
    [Fact]
    public async Task AUTH_RECOV_007_AC8_AnAskBeforeAFirstFactorIsAnsweredAsEveryAskIsWhateverTheAccountHoldsAsync()
    {
        SubjectId subject = await AccountAsync();
        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Suspends(subject, Factor.PhoneCode);
        SignInChallenge began = await BeganAsync(Address);

        Result<SignInProgress?> asked = await AskedAsync(began.Challenge, stepping: null);

        Assert.True(asked.Match(offered => offered is null, _ => false));
        Assert.Empty(_notifications.Texts);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// IDN-ATTR-008 AC4, AUTH-RECOV-007: a preferred second-step method that is not
    /// active is not offered at a sign-in's second step, and the challenge offers the
    /// other active ones.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_008_AC4_APreferredMethodThatIsNotActiveIsNotOfferedAtASignInAndTheOthersAreAsync()
    {
        SubjectId subject = await AccountAsync();
        Holds(subject, Factor.Totp);
        Holds(subject, Factor.SecurityKey, isPreferred: true);
        Suspends(subject, Factor.SecurityKey);

        SignInProgress reached = await SignedInAsync(subject, Factor.Password, Secret);

        Assert.Equal(SignInStatus.FactorRequired, reached.Status);
        Assert.Equal([Factor.Totp], reached.Required);
    }

    /// <summary>
    /// IDN-ATTR-008 AC4, AUTH-RECOV-007: a preferred second-step method that is not
    /// active is not offered at a gate, and the step-up goes on asking for the other
    /// active ones.
    /// </summary>
    [Fact]
    public async Task IDN_ATTR_008_AC4_APreferredMethodThatIsNotActiveIsNotOfferedAtAGateAndTheOthersAreAsync()
    {
        SubjectId subject = await AccountAsync();
        Holds(subject, Factor.Totp);
        Holds(subject, Factor.SecurityKey, isPreferred: true);
        Suspends(subject, Factor.SecurityKey);
        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);

        SignInProgress reached = Reached(await Service.StepUpAsync(
            AccessContext.Of(subject),
            session,
            began.Challenge,
            new FactorPresentation(Factor.Password) { Value = Secret },
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(SignInStatus.FactorRequired, reached.Status);
        Assert.Equal([Factor.Totp], reached.Required);
    }

    /// <summary>
    /// AUTH-ABUSE-001: a failed attempt of a presented factor is one refused with a code
    /// the item lists, and with no other code of the catalogue: the domain lock's
    /// refusal, which follows a factor that succeeded, and a fault of the library's own
    /// are none.
    /// </summary>
    [Fact]
    public void AUTH_ABUSE_001_AFailedAttemptIsAFactorRefusedWithAListedCode()
    {
        ErrorCode[] listed =
        [
            ErrorCodes.FactorRejected,
            ErrorCodes.FactorNotPermitted,
            ErrorCodes.CodeInvalid,
            ErrorCodes.CodeExpired,
            ErrorCodes.CodeReplayed,
            ErrorCodes.CredentialSuspended,
            ErrorCodes.WebAuthnAlgorithmNotAllowed,
            ErrorCodes.WebAuthnCounterMismatch,
            ErrorCodes.WebAuthnRelyingPartyChanged,
            ErrorCodes.WebAuthnUserVerificationRequired,
        ];

        ErrorCode[] counted = [.. typeof(ErrorCodes)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(ErrorCode))
            .Select(property => (ErrorCode)property.GetValue(obj: null)!)
            .Where(code => AuthenticationService.Refuses(Error.From(code)))];

        Assert.Equal(
            listed.Select(code => code.ToString()).Order(StringComparer.Ordinal),
            counted.Select(code => code.ToString()).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(ErrorCodes.IdentifierDomainNotAllowed, counted);
        Assert.DoesNotContain(ErrorCodes.SystemFault, counted);
    }

    /// <summary>
    /// AUTH-ABUSE-001, CONV-LOG-005, `09` `POST /auth/step-up`: a step-up answers each
    /// refusal a factor gives what is presented to it, and each is a failed attempt,
    /// recorded as a refused step-up factor and counted against the source.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AStepUpCountsEveryRefusalAFactorAnswersAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Holds(subject, Factor.Totp);
        _configuration.Set(Settings.AbuseThrottleThreshold, 100);

        SessionId session = Opened(subject);
        string generated = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var refused = new List<ErrorCode?>();

        refused.Add(await SteppedAsync(_ => new FactorPresentation(Factor.Password) { Value = "not the password" }));
        refused.Add(await SteppedAsync(_ => new FactorPresentation(Factor.EmailCode) { Value = "000000" }));
        refused.Add(await SteppedAsync(_ => new FactorPresentation(Factor.PhoneCode) { Value = "000000" }));

        SignInChallenge texted = await BeganAsync(Address);

        Assert.True((await AskedAsync(texted.Challenge, subject, session)).Match(offered => offered is null, _ => false));

        refused.Add(await SteppedAsync(
            _ => new FactorPresentation(Factor.PhoneCode) { Value = Other(_notifications.Texts[^1].Values["code"]) },
            texted));

        Assert.Null(await SteppedAsync(_ => new FactorPresentation(Factor.Totp) { Value = generated }));

        refused.Add(await SteppedAsync(_ => new FactorPresentation(Factor.Totp) { Value = generated }));
        refused.Add(await KeyedAsync(counter: 8, "example.test", algorithm: -7, flags: 0x05));
        refused.Add(await KeyedAsync(counter: 10, "elsewhere.test", algorithm: -7, flags: 0x05));
        refused.Add(await KeyedAsync(counter: 10, "example.test", algorithm: -7, flags: 0x01));
        refused.Add(await KeyedAsync(counter: 10, "example.test", algorithm: -8, flags: 0x05));

        Assert.Equal<ErrorCode?>(
            [
                ErrorCodes.FactorRejected,
                ErrorCodes.FactorNotPermitted,
                ErrorCodes.CodeExpired,
                ErrorCodes.CodeInvalid,
                ErrorCodes.CodeReplayed,
                ErrorCodes.WebAuthnCounterMismatch,
                ErrorCodes.WebAuthnRelyingPartyChanged,
                ErrorCodes.WebAuthnUserVerificationRequired,
                ErrorCodes.WebAuthnAlgorithmNotAllowed,
            ],
            refused);
        Assert.Equal(
            [
                Factor.Password,
                Factor.EmailCode,
                Factor.PhoneCode,
                Factor.PhoneCode,
                Factor.Totp,
                Factor.Passkey,
                Factor.Passkey,
                Factor.Passkey,
                Factor.Passkey,
            ],
            _audit.StepUpsFailed.Select(failed => failed.Presented));
        Assert.All(_audit.StepUpsFailed, failed => Assert.Equal((session, subject), (failed.Session, failed.Subject)));
        Assert.Equal(
            9,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.False(_work.Open);

        async ValueTask<ErrorCode?> SteppedAsync(
            Func<SignInChallenge, FactorPresentation> presented,
            SignInChallenge? opened = null)
        {
            SignInChallenge began = opened ?? await BeganAsync(Address);

            return Refused(await Service.StepUpAsync(
                AccessContext.Of(subject),
                session,
                began.Challenge,
                presented(began),
                Source,
                TestContext.Current.CancellationToken));
        }

        async ValueTask<ErrorCode?> KeyedAsync(uint counter, string relyingParty, int algorithm, byte flags)
        {
            AuthenticatorId held = Keyed(subject, key, counter: 9, relyingParty, algorithm);

            ErrorCode? answered = await SteppedAsync(began => new FactorPresentation(Factor.Passkey)
            {
                Assertion = Asserted(key, began.WebAuthn.Challenge, counter, relyingParty, flags),
            });

            await _authenticators.RemoveAsync(held, TestContext.Current.CancellationToken);

            return answered;
        }
    }

    /// <summary>
    /// AUTH-ABUSE-001, CONV-DESIGN-003 AC10: a fault of the library's own on a sign-in
    /// code's path (the attempt cap does not read) leaves the unit of work rolled back,
    /// the code as it stood, nothing counted and no failed authentication recorded.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AFaultOnASignInCodesPathIsRolledBackAndCountedNowhereAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailCode);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _ = await Service.SendCodeAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        string right = Code();

        _configuration.Unread = Settings.CodeSigninAttempts.Key;
        _work.Reset();

        Result<SignInProgress> faulted = await PresentAsync(began.Challenge, Factor.EmailCode, right);

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, Refused(faulted));
        Assert.False(_work.Open);
        Assert.Equal((1, 0, 1), (_work.Opened, _work.Committed, _work.RolledBack));
        Assert.Equal(
            0,
            (await _pending.FindAsync(subject, Factor.EmailCode, TestContext.Current.CancellationToken))?.WrongAttempts);
        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// AUTH-ABUSE-001, CONV-DESIGN-003 AC10: a fault of the library's own judging the
    /// domain lock after a right sign-in code leaves the unit of work rolled back, the
    /// code unspent, nothing counted and no failed authentication recorded.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AFaultJudgingTheLockAfterARightSignInCodeIsRolledBackAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailCode);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _ = await Service.SendCodeAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        string right = Code();

        _memberships.Place(subject, Locked);
        _configuration.Unread = Settings.OrganizationPolicy.For(Locked.ToString());
        _work.Reset();

        Result<SignInProgress> faulted = await PresentAsync(began.Challenge, Factor.EmailCode, right);

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, Refused(faulted));
        Assert.False(_work.Open);
        Assert.Equal((0, 1), (_work.Committed, _work.RolledBack));
        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// AUTH-ABUSE-001, CONV-LOG-005, CONV-DESIGN-003 AC10: a fault of the library's own
    /// on the new-device check's path (the attempt cap does not read) leaves the unit of
    /// work rolled back, the code as it stood, nothing counted and no refused
    /// verification recorded.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AFaultOnADeviceChecksPathIsRolledBackAndCountedNowhereAsync()
    {
        await AccountAsync();

        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        string right = Code();

        _configuration.Unread = Settings.CodeVerificationAttempts.Key;
        _work.Reset();

        Result<SignInProgress> faulted = await Service.VerifyDeviceAsync(
            began.Challenge,
            right,
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, Refused(faulted));
        Assert.False(_work.Open);
        Assert.Equal((1, 0, 1), (_work.Opened, _work.Committed, _work.RolledBack));
        Assert.Equal(0, Assert.Single(_codes.All).Attempts);
        Assert.Empty(_audit.DeviceVerificationsFailed);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10, AUTH-FACT-004: a wrong code of the new-device check commits
    /// its count on the code's record, the refusal's record and the failure's counts in
    /// one unit of work.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_AWrongDeviceCodeCommitsItsKeptWritesTogetherAsync()
    {
        SubjectId subject = await AccountAsync();
        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        string right = Code();

        _work.Reset();

        Result<SignInProgress> wrong = await Service.VerifyDeviceAsync(
            began.Challenge,
            Other(right),
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.CodeInvalid, Refused(wrong));
        Assert.False(_work.Open);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.Equal(1, Assert.Single(_codes.All).Attempts);
        Assert.Equal([subject], _audit.DeviceVerificationsFailed);
        Assert.Contains(_throttle.Counted, counter => counter.Scope is ThrottleScope.Source);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10, AUTH-FACT-004: a new-device code presented past its
    /// lifetime leaves the code's record as it stood, the lapsed record being the
    /// sweep's, and commits the refusal's record and the failure's counts alone.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_ADeviceCodePastItsLifetimeCommitsOnlyItsRecordAndItsCountsAsync()
    {
        SubjectId subject = await AccountAsync();

        SignInChallenge began = await BeganAsync(Address);

        // The sign-in lives as long as the key held when it began, the code as long as
        // the key holds when it is sent, so the code lapses first.
        _configuration.Set(Settings.CodeVerificationLifetime, TimeSpan.FromMinutes(1));

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        string right = Code();

        _clock.Advance(TimeSpan.FromMinutes(1));
        _work.Reset();

        Result<SignInProgress> lapsed = await Service.VerifyDeviceAsync(
            began.Challenge,
            right,
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.CodeExpired, Refused(lapsed));
        Assert.False(_work.Open);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.Equal(0, Assert.Single(_codes.All).Attempts);
        Assert.Equal([subject], _audit.DeviceVerificationsFailed);
    }

    /// <summary>
    /// AUTH-FACT-014 AC3, CONV-DESIGN-003 AC10: a signature counter that did not advance
    /// is refused, and the refusal's record, the failed authentication's record and the
    /// failure's counts are committed together, the stored counter left as it stood.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_014_AC3_ACounterThatDidNotAdvanceCommitsItsThreeWritesTogetherAsync()
    {
        SubjectId subject = await AccountAsync();

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        AuthenticatorId credential = Keyed(subject, key, counter: 9);
        SignInChallenge began = await BeganAsync(Address);

        _work.Reset();

        Result<SignInProgress> refused = await Service.PresentAsync(
            began.Challenge,
            new FactorPresentation(Factor.Passkey) { Assertion = Asserted(key, began.WebAuthn.Challenge, counter: 8) },
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.WebAuthnCounterMismatch, Refused(refused));
        Assert.False(_work.Open);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));

        (AuditAction action, SubjectId audited, AuthenticatorId named) = Assert.Single(_credentials.Records);

        Assert.Equal(("auth.credential.countermismatch", subject, credential), (action.ToString(), audited, named));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.Passkey)], _audit.Failed);
        Assert.Contains(_throttle.Counted, counter => counter.Scope is ThrottleScope.Source);
        Assert.Equal(
            9u,
            (await _authenticators.FindAsync(credential, TestContext.Current.CancellationToken))!.WebAuthn!.Counter);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10: an assertion the credential did not sign keeps no write of
    /// its own, so its unit of work is rolled back and the failed authentication's
    /// record and the failure's counts are committed in theirs.
    /// </summary>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_AnAssertionRefusedOtherwiseIsRolledBackAndCountedAsync()
    {
        SubjectId subject = await AccountAsync();

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        AuthenticatorId credential = Keyed(subject, key, counter: 9);
        SignInChallenge began = await BeganAsync(Address);

        _work.Reset();

        Result<SignInProgress> refused = await Service.PresentAsync(
            began.Challenge,
            new FactorPresentation(Factor.Passkey) { Assertion = Asserted(other, began.WebAuthn.Challenge, counter: 10) },
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.FactorRejected, Refused(refused));
        Assert.False(_work.Open);
        Assert.Equal((1, 1), (_work.OutermostCommitted, _work.RolledBack));
        Assert.Empty(_credentials.Records);
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.Passkey)], _audit.Failed);
        Assert.Equal(
            9u,
            (await _authenticators.FindAsync(credential, TestContext.Current.CancellationToken))!.WebAuthn!.Counter);
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC9, AUTH-ABUSE-003: failures against an identifier no account
    /// holds are counted against the identifier as failures against one an account
    /// holds are, so from a source that has failed nothing both are held alike and a
    /// third identifier is not held at all.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AC9_AnIdentifierNoAccountHoldsIsHeldFromAFreshSourceAsOneAnAccountHoldsAsync()
    {
        await AccountAsync();

        await FailedAsync(Elsewhere, "198.51.100.21", times: 3);
        await FailedAsync(Address, "198.51.100.22", times: 3);

        Result<SignInChallenge> nobodys = await Service.BeginAsync(
            Elsewhere,
            Fresh,
            remembered: null,
            trusted: null,
            browser: null,
            TestContext.Current.CancellationToken);
        Result<SignInChallenge> held = await Service.BeginAsync(
            Address,
            Fresh,
            remembered: null,
            trusted: null,
            browser: null,
            TestContext.Current.CancellationToken);
        Result<SignInChallenge> other = await Service.BeginAsync(
            "somebody@example.test",
            Fresh,
            remembered: null,
            trusted: null,
            browser: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.Throttled, Delayed(nobodys)?.Code);
        Assert.Equal(Shape(Delayed(held)), Shape(Delayed(nobodys)));
        Assert.Null(Delayed(other));
    }

    /// <summary>
    /// AUTH-ABUSE-003 AC2: the tokens a browser carries are looked up alike whether the
    /// identifier resolves to an account or to none, both of them whatever the first
    /// answered, so the work a sign-in opens with says nothing about the identifier.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_003_AC2_ACarriedTokenIsLookedUpAlikeForAHeldAndAnUnheldIdentifierAsync()
    {
        SubjectId subject = await AccountAsync();
        string remembered = await KnownAsync(subject, trusted: false);
        string trusted = await KnownAsync(subject, trusted: true);

        int before = _devices.LookedUp;

        _ = await Service.BeginAsync(Address, Fresh, remembered, trusted, browser: null, TestContext.Current.CancellationToken);

        int held = _devices.LookedUp - before;

        _ = await Service.BeginAsync(Elsewhere, Fresh, remembered, trusted, browser: null, TestContext.Current.CancellationToken);

        int unheld = _devices.LookedUp - before - held;

        Assert.Equal(2, held);
        Assert.Equal(held, unheld);
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC5: a browser is spared the delay the account and the identifier
    /// have earned only by a token that stands for this account; a forged token, another
    /// account's, one of the wrong kind or none at all is held as any browser is, both
    /// where the sign-in opens and where a factor is presented.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AC5_OnlyATokenOfTheAccountsOwnSparesItsBrowserTheDelayAsync()
    {
        SubjectId subject = await AccountAsync();

        Remembered(subject);

        string remembered = await KnownAsync(subject, trusted: false);
        string trusted = await KnownAsync(subject, trusted: true);
        string foreign = await KnownAsync(new SubjectId(Guid.NewGuid()), trusted: false);
        string forged = OpaqueToken.Draw(_randomness).Value;

        await FailedAsync(Address, "198.51.100.22", times: 3);

        Assert.Equal(ErrorCodes.Throttled, Delayed(await BeganFromAsync(null, null))?.Code);
        Assert.Equal(ErrorCodes.Throttled, Delayed(await BeganFromAsync(forged, forged))?.Code);
        Assert.Equal(ErrorCodes.Throttled, Delayed(await BeganFromAsync(foreign, null))?.Code);
        Assert.Equal(ErrorCodes.Throttled, Delayed(await BeganFromAsync(null, remembered))?.Code);
        Assert.Null(Delayed(await BeganFromAsync(null, trusted)));

        SignInChallenge began = (await BeganFromAsync(remembered, null)).Match(
            challenge => challenge,
            error => throw new InvalidOperationException(error.Code.ToString()));

        Result<SignInOutcome> spoofed = await PresentedFromAsync(began.Challenge, foreign);
        Result<SignInOutcome> recognised = await PresentedFromAsync(began.Challenge, remembered);

        Assert.Equal(ErrorCodes.Throttled, spoofed.Match(_ => (ErrorCode?)null, error => error.Code));
        Assert.Equal(
            SignInStatus.Complete,
            recognised.Match(
                outcome => outcome.Progress.Status,
                error => throw new InvalidOperationException(error.Code.ToString())));
    }

    /// <summary>
    /// AUTH-ABUSE-002 AC3, AUTH-ABUSE-003: an ask that sends no link, because the
    /// policy has not enabled the channel, because the account is not active, because
    /// a domain lock refuses the address, or because the window has already told an
    /// address no account holds, counts against the sending restrictions as the link
    /// would have, and nothing but the one notice reaches an address no account holds.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_002_AC3_AnAskNoLinkAnswersCountsAsTheLinkWouldAsync()
    {
        SubjectId subject = await AccountAsync();

        Assert.True(await AskedLinkAsync(Address));

        Enables(Factor.EmailLink);
        _accounts.Stands(subject, AccountState.Suspended);

        Assert.True(await AskedLinkAsync(Address));

        _accounts.Stands(subject, AccountState.Active);
        _memberships.Place(subject, Locked);
        _configuration.Set(
            Settings.OrganizationPolicy,
            Locked.ToString(),
            PolicyOverride.None with { EmailDomains = ["elsewhere.test"] });

        Assert.True(await AskedLinkAsync(Address));
        Assert.True(await AskedLinkAsync(Elsewhere));
        Assert.True(await AskedLinkAsync(Elsewhere));

        Assert.Equal(
            [Address, Address, Address, Elsewhere],
            _restrictions.Drawn.Select(drawn => drawn.Destination.Canonical));
        Assert.All(_restrictions.Drawn, drawn => Assert.Equal(MessageKind.SignInLink, drawn.Message));
        Assert.All(_restrictions.Drawn, drawn => Assert.Equal(RestrictionPurpose.SignIn, drawn.Purpose));

        OutboundMessage told = Assert.Single(_notifications.Sent);

        Assert.Equal(Elsewhere, told.Destination.Canonical);
        Assert.Equal(MessageKind.NoAccount, told.Message);
        Assert.Empty(told.Values);
    }

    /// <summary>
    /// AUTH-ABUSE-002 AC3, BFF-ABUSE-002 AC1: where the restrictions refuse the send an
    /// ask stands for, an account the ask cannot reach and an address no account holds
    /// are refused with the one refusal.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_002_AC3_ARefusalAnswersAnAskNoLinkAnswersAlikeAsync()
    {
        await AccountAsync();

        Assert.True(await AskedLinkAsync(Elsewhere));

        var refusal = Error.From(ErrorCodes.RestrictionExceeded);
        _restrictions.Refusal = refusal;

        Result<RequestedLink> held = await Service.SendLinkAsync(Address, Language, Source, browser: null, TestContext.Current.CancellationToken);
        Result<RequestedLink> nobodys = await Service.SendLinkAsync(Elsewhere, Language, Source, browser: null, TestContext.Current.CancellationToken);

        Assert.Same(refusal, held.Match(_ => (Error?)null, error => error));
        Assert.Same(refusal, nobodys.Match(_ => (Error?)null, error => error));
    }

    /// <summary>
    /// CONV-LOG-005 AC1: a link pressed in the browser that asked for it and landing
    /// on no sign-in of its account, its sign-in having completed since, is a refused
    /// link against that account, and a pressed token that resolves to nothing is one
    /// against no account; an open that is not a press and a press in another browser
    /// are no attempt and are not recorded.
    /// </summary>
    [Fact]
    public async Task CONV_LOG_005_AC1_ALinkThatDoesNotLandIsRecordedAgainstItsAccountAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailLink);
        Remembered(subject);

        (string challenge, string token, string browser) = await AskedAsync(subject);

        _ = await LandedAsync(browser, token, press: false);
        _ = await LandedAsync(browser: null, token, press: true);

        Assert.Empty(_audit.Failed);

        Result<SignInLanding> unknown = await Service.LandAsync(
            browser,
            "a-token-nothing-issued",
            Factor.EmailLink,
            press: true,
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(SignInStatus.Complete, Reached(await PresentAsync(challenge, Factor.Password, Secret)).Status);

        Result<SignInLanding> astray = await Service.LandAsync(
            browser,
            token,
            Factor.EmailLink,
            press: true,
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.CodeExpired, unknown.Match(_ => (ErrorCode?)null, error => error.Code));
        Assert.Equal(ErrorCodes.FactorRejected, astray.Match(_ => (ErrorCode?)null, error => error.Code));
        Assert.Equal<(SubjectId?, Factor)>([(null, Factor.EmailLink), (subject, Factor.EmailLink)], _audit.Failed);
    }

    /// <summary>
    /// CONV-LOG-005 AC1 and AUTH-ABUSE-001: a pressed link token that opens nothing is a
    /// refused factor against no account under the factor the request named, counted
    /// against its source; once the source has earned a delay the press is refused
    /// throttled and nothing more is recorded.
    /// </summary>
    [Fact]
    public async Task CONV_LOG_005_AC1_APressedLinkThatIsGoneIsRecordedBehindTheSourceDelayAsync()
    {
        var answers = new List<ErrorCode?>();

        for (int attempt = 0; attempt < Settings.AbuseThrottleThreshold.Default + 1; attempt++)
        {
            answers.Add(Code(await Service.LandAsync(
                browser: null,
                "a-token-nothing-issued",
                Factor.PhoneLink,
                press: true,
                Browser,
                Source,
                TestContext.Current.CancellationToken)));
        }

        Assert.Equal(
            [ErrorCodes.CodeExpired, ErrorCodes.CodeExpired, ErrorCodes.CodeExpired, ErrorCodes.Throttled],
            answers);
        Assert.Equal<(SubjectId?, Factor)>(
            [(null, Factor.PhoneLink), (null, Factor.PhoneLink), (null, Factor.PhoneLink)],
            _audit.Failed);
    }

    /// <summary>
    /// CONV-LOG-005 AC1: a link token that opens nothing and is not pressed is no
    /// attempt: it is answered as a gone link, recorded nowhere and counted against
    /// nothing, however often it is opened.
    /// </summary>
    [Fact]
    public async Task CONV_LOG_005_AC1_AnUnpressedLinkThatIsGoneWritesNothingAsync()
    {
        for (int attempt = 0; attempt < Settings.AbuseThrottleThreshold.Default + 1; attempt++)
        {
            Assert.Equal(
                ErrorCodes.CodeExpired,
                Code(await Service.LandAsync(
                    browser: null,
                    "a-token-nothing-issued",
                    Factor.EmailLink,
                    press: false,
                    Browser,
                    Source,
                    TestContext.Current.CancellationToken)));
        }

        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// CONV-LOG-005 AC1: a delegated sign-in refused because the identity is linked to
    /// no account is recorded against none, and one refused because the account it is
    /// linked to is not active is recorded against that account.
    /// </summary>
    [Fact]
    public async Task CONV_LOG_005_AC1_ARefusedDelegatedSignInIsRecordedAsync()
    {
        SubjectId subject = await AccountAsync();

        await _authenticators.LinkAsync(
            Authenticator.Linked(
                AuthenticatorId.New(_clock),
                subject,
                Factor.Google,
                Label(Factor.Google),
                _clock.GetUtcNow()),
            "linked-at-the-provider",
            TestContext.Current.CancellationToken);

        _accounts.Stands(subject, AccountState.Suspended);

        Result<SignInOutcome> unlinked = await Service.DelegatedAsync(
            Factor.Google,
            "linked-to-nothing",
            new SessionOrigin(Source, Browser),
            TestContext.Current.CancellationToken);

        Result<SignInOutcome> inactive = await Service.DelegatedAsync(
            Factor.Google,
            "linked-at-the-provider",
            new SessionOrigin(Source, Browser),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.FactorRejected, unlinked.Match(_ => (ErrorCode?)null, error => error.Code));
        Assert.Equal(ErrorCodes.FactorRejected, inactive.Match(_ => (ErrorCode?)null, error => error.Code));
        Assert.Equal<(SubjectId?, Factor)>(
            [(null, Factor.Google), (subject, Factor.Google)],
            _audit.Failed);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC9: a social credential that is suspended, on a window or held
    /// after its provider's security event, is refused
    /// <c>auth.credential.suspended</c> once its provider vouches, a failed attempt
    /// recorded against its account and counted, and nobody is signed in.
    /// </summary>
    /// <param name="held">Whether a provider's event holds it, or a window suspends it.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUTH_RECOV_007_AC9_ASuspendedSocialCredentialIsRefusedSuspendedOnceItsProviderVouchesAsync(
        bool held)
    {
        SubjectId subject = await AccountAsync();
        Authenticator linked = await LinkedAsync(subject, held);
        _work.Reset();

        Result<SignInOutcome> refused = await Service.DelegatedAsync(
            Factor.Google,
            "linked-at-the-provider",
            new SessionOrigin(Source, Browser),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.CredentialSuspended, refused.Match(_ => (ErrorCode?)null, error => error.Code));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.Google)], _audit.Failed);
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Equal(AuthenticatorState.Suspended, linked.State);
        Assert.Empty(_live.All);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-007: a suspended social credential is judged first as an active one
    /// would be, so on an account that is not active it is refused as an active one is,
    /// <c>auth.factor.rejected</c>, and nothing is told of its state.
    /// </summary>
    /// <param name="held">Whether a provider's event holds it, or a window suspends it.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUTH_RECOV_007_ASuspendedSocialCredentialOfAnAccountNotActiveIsRefusedAsAnActiveOneIsAsync(
        bool held)
    {
        SubjectId subject = await AccountAsync();
        _ = await LinkedAsync(subject, held);
        _accounts.Stands(subject, AccountState.Suspended);

        Result<SignInOutcome> refused = await Service.DelegatedAsync(
            Factor.Google,
            "linked-at-the-provider",
            new SessionOrigin(Source, Browser),
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.FactorRejected, refused.Match(_ => (ErrorCode?)null, error => error.Code));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.Google)], _audit.Failed);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// CONV-LOG-005 AC1: a provider's round trip whose identity does not hold up is a
    /// refused factor naming no account, recorded and counted against its source, and
    /// while the delay it earned stands nothing more is recorded (AUTH-ABUSE-001).
    /// </summary>
    [Fact]
    public async Task CONV_LOG_005_AC1_AProviderIdentityThatDoesNotHoldUpIsRecordedBehindTheDelayAsync()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Assert.Equal(
                ErrorCodes.FactorRejected,
                (await Service.ProviderRefusedAsync(Factor.Apple, Source, TestContext.Current.CancellationToken)).Code);
        }

        Assert.Equal(
            ErrorCodes.Throttled,
            (await Service.ProviderRefusedAsync(Factor.Apple, Source, TestContext.Current.CancellationToken)).Code);
        Assert.Equal<(SubjectId?, Factor)>(
            [(null, Factor.Apple), (null, Factor.Apple), (null, Factor.Apple)],
            _audit.Failed);
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
    /// AUTH-FACT-002b AC7: at a sign-in a second step presented while no first factor
    /// has been accepted on the challenge is refused <c>auth.factor.notpermitted</c>
    /// before it is verified and before anything of the account is read: the answer is
    /// the same for an identifier whose account holds the factor, the right value
    /// presented, for one whose account does not, and for one no account holds, and
    /// neither the account's state, its credentials, its password, a browser's token
    /// nor a delay's counter is read for it.
    /// </summary>
    /// <param name="second">The second step presented.</param>
    [Theory]
    [InlineData(Factor.Totp)]
    [InlineData(Factor.RecoveryCodes)]
    [InlineData(Factor.SecurityKey)]
    [InlineData(Factor.PhoneCode)]
    public async Task AUTH_FACT_002b_AC7_ASecondStepBeforeAFirstFactorIsRefusedBeforeAnythingOfTheAccountIsReadAsync(
        Factor second)
    {
        SubjectId holding = await AccountAsync();
        var bare = new SubjectId(Guid.NewGuid());

        _accounts.Stands(bare, AccountState.Active);
        _ = _identifiers.Verified(bare, IdentifierKind.Email, Second);

        Enables(Factor.PhoneCode);
        Holds(holding, Factor.Totp);
        Holds(holding, Factor.SecurityKey);
        Holds(holding, Factor.PhoneCode);

        string known = await KnownAsync(holding, trusted: true);
        string value = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);

        SignInChallenge held = await BeganAsync(Address);
        SignInChallenge lacking = await BeganAsync(Second);
        SignInChallenge nobody = await BeganAsync(Elsewhere);

        (int States, int Credentials, int Passwords, int Browsers, int Delays, int Opened) before =
            (_accounts.StateReads, _authenticators.Reads, _passwords.Reads, _devices.LookedUp, _throttle.Reads, _work.Opened);

        string[] answers =
        [
            await RefusedFromAsync(held.Challenge),
            await RefusedFromAsync(lacking.Challenge),
            await RefusedFromAsync(nobody.Challenge),
        ];

        Assert.All(answers, answer => Assert.Equal(Shape(Error.From(ErrorCodes.FactorNotPermitted)), answer));
        Assert.Equal(
            before,
            (_accounts.StateReads, _authenticators.Reads, _passwords.Reads, _devices.LookedUp, _throttle.Reads, _work.Opened));
        Assert.False(_work.Open);

        async ValueTask<string> RefusedFromAsync(string challenge) =>
            Shape((await Service.PresentAsync(
                    challenge,
                    new FactorPresentation(second) { Value = value },
                    new SessionOrigin(Source, Browser),
                    remembered: null,
                    known,
                    TestContext.Current.CancellationToken))
                .Match(_ => (Error?)null, error => error));
    }

    /// <summary>
    /// AUTH-ABUSE-001, AUTH-FACT-002b AC7: a second step refused at a sign-in before a
    /// first factor is no failed attempt. No counter moves, under the source, the
    /// identifier or the account, no failed authentication is recorded and no event is
    /// published, whether the value presented is right or wrong; a wrong password
    /// presented after it is counted and recorded as it always is.
    /// </summary>
    /// <param name="right">Whether the generated code presented is the right one.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AUTH_ABUSE_001_ASecondStepRefusedBeforeAFirstFactorCountsAndRecordsNothingAsync(bool right)
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);

        string generated = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);

        SignInChallenge began = await BeganAsync(Address);
        int published = _events.Published.Count;

        ErrorCode? second = Refused(
            await PresentAsync(began.Challenge, Factor.Totp, right ? generated : Other(generated)));

        Assert.Equal(ErrorCodes.FactorNotPermitted, second);
        Assert.Empty(_throttle.Counted);
        Assert.Empty(_throttle.Failures);
        Assert.Empty(_audit.Failed);
        Assert.Equal(published, _events.Published.Count);

        ErrorCode? first = Refused(await PresentAsync(began.Challenge, Factor.Password, "wrong" + Secret));

        Assert.Equal(ErrorCodes.FactorRejected, first);
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.Password)], _audit.Failed);
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-002b AC7: the refusal of a second step presented before a first factor
    /// leaves the challenge as it was: nothing is accepted on it, and the same second
    /// step, presented once a first factor has been accepted, completes the sign-in.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC7_ASecondStepIsAcceptedOnlyOnceAFirstFactorHasBeenAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);
        string generated = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);

        ErrorCode? before = Refused(await PresentAsync(began.Challenge, Factor.Totp, generated));
        SignInProgress first = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));
        SignInProgress second = Reached(await PresentAsync(began.Challenge, Factor.Totp, generated));

        Assert.Equal(ErrorCodes.FactorNotPermitted, before);
        Assert.Equal(SignInStatus.FactorRequired, first.Status);
        Assert.Equal(AssuranceLevel.Aal1, first.AssuranceLevel);
        Assert.Equal([Factor.Totp], first.Required);
        Assert.Equal(SignInStatus.Complete, second.Status);
        Assert.Equal(AssuranceLevel.Aal2, second.AssuranceLevel);
    }

    /// <summary>
    /// AUTH-FACT-002b AC9: at a sign-in before a first factor, an assertion presented
    /// as <c>passkey</c> whose credential is a second-factor security key is refused
    /// <c>auth.factor.notpermitted</c> once it verifies: for an active key, for a
    /// suspended one, under a policy whose <c>loginFactors</c> omits <c>passkey</c> and
    /// under a domain lock on the sign-in's address alike. Nothing is accepted on the
    /// challenge, nothing is counted, no failed authentication is recorded, nothing is
    /// committed, and the stored counter, <c>backupState</c> and last use stand as they
    /// were.
    /// </summary>
    /// <param name="standing">What stands against the key or the sign-in.</param>
    [Theory]
    [InlineData("active")]
    [InlineData("suspended")]
    [InlineData("policy")]
    [InlineData("lock")]
    public async Task AUTH_FACT_002b_AC9_ASecurityKeysAssertionPresentedAsPasskeyBeforeAFirstFactorIsRefusedOnceItVerifiesAsync(
        string standing)
    {
        SubjectId subject = await AccountAsync();
        var key = SoftwareAuthenticator.Holding();
        Authenticator held = Counting(subject, Factor.SecurityKey, key, counter: 9);

        Remembered(subject);

        switch (standing)
        {
            case "suspended":
                held.Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
                break;
            case "policy":
                _configuration.Set(
                    Settings.PolicyDefault,
                    Janus.Core.Policies.SystemDefault with
                    {
                        LoginFactors = new HashSet<Factor>(
                            Janus.Core.Policies.SystemDefault.LoginFactors.Where(factor => factor != Factor.Passkey)),
                    });
                break;
            case "lock":
                LockedElsewhere(subject);
                break;
            default:
                break;
        }

        SignInChallenge began = await BeganAsync(Address);
        int published = _events.Published.Count;

        _work.Reset();

        Result<SignInProgress> refused = await Service.PresentAsync(
            began.Challenge,
            new FactorPresentation(Factor.Passkey) { Assertion = Synced(key, began.WebAuthn.Challenge, counter: 10) },
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Challenge? open = await _challenges.FindAsync(
            OpaqueToken.Of(began.Challenge).Fingerprint(),
            TestContext.Current.CancellationToken);
        Authenticator? after = await _authenticators.FindAsync(held.Id, TestContext.Current.CancellationToken);

        Assert.Equal(
            Shape(Error.From(ErrorCodes.FactorNotPermitted)),
            Shape(refused.Match(_ => (Error?)null, error => error)));
        Assert.Empty(Assert.IsType<Challenge>(open).Presented);
        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
        Assert.Empty(_throttle.Failures);
        Assert.Empty(_credentials.Records);
        Assert.Equal(published, _events.Published.Count);
        Assert.Equal(
            (9u, false, null),
            (after?.WebAuthn?.Counter, after?.WebAuthn?.BackupState, after?.LastUsedAt));
        Assert.Equal(0, _work.Committed);
        Assert.False(_work.Open);
        Assert.Empty(await _live.LiveOfAsync(subject, _clock.GetUtcNow(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-002b AC9: the refusal of a security key's assertion presented as
    /// <c>passkey</c> before a first factor leaves the challenge as it was, so the
    /// password and then the key under its own name complete the sign-in.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC9_TheRefusalAcceptsNothingAndTheKeyCompletesTheSignInOnceAFirstFactorIsAcceptedAsync()
    {
        SubjectId subject = await AccountAsync();
        var key = SoftwareAuthenticator.Holding();

        _ = Counting(subject, Factor.SecurityKey, key, counter: 9);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        ErrorCode? before = Refused(await Service.PresentAsync(
            began.Challenge,
            new FactorPresentation(Factor.Passkey) { Assertion = Synced(key, began.WebAuthn.Challenge, counter: 10) },
            Browser,
            Source,
            TestContext.Current.CancellationToken));
        SignInProgress first = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));
        SignInProgress second = Reached(await Service.PresentAsync(
            began.Challenge,
            new FactorPresentation(Factor.SecurityKey)
            {
                Assertion = Synced(key, began.WebAuthn.Challenge, counter: 10),
            },
            Browser,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.FactorNotPermitted, before);
        Assert.Equal((SignInStatus.FactorRequired, AssuranceLevel.Aal1), (first.Status, first.AssuranceLevel));
        Assert.Equal([Factor.SecurityKey], first.Required);
        Assert.Equal((SignInStatus.Complete, AssuranceLevel.Aal2), (second.Status, second.AssuranceLevel));
        Assert.Empty(_audit.Failed);
    }

    /// <summary>
    /// AUTH-FACT-002b AC9: an assertion presented as <c>passkey</c> before a first
    /// factor that does not verify is refused as a wrong passkey assertion is, the same
    /// answer whether its identifier names a second-factor security key, a passkey or
    /// nothing: <c>auth.factor.rejected</c>, a failed authentication recorded under
    /// <c>passkey</c> and counted, and nothing of the credential written.
    /// </summary>
    /// <param name="names">What the assertion's credential identifier names.</param>
    [Theory]
    [InlineData("securityKey")]
    [InlineData("passkey")]
    [InlineData("nothing")]
    public async Task AUTH_FACT_002b_AC9_AnAssertionThatDoesNotVerifyIsRefusedAsAWrongPasskeyAssertionIsWhateverItsIdentifierNamesAsync(
        string names)
    {
        SubjectId subject = await AccountAsync();
        var key = SoftwareAuthenticator.Holding();
        var forger = SoftwareAuthenticator.Holding(credentialId: key.CredentialId);
        Authenticator? held = names switch
        {
            "securityKey" => Counting(subject, Factor.SecurityKey, key, counter: 9),
            "passkey" => Counting(subject, Factor.Passkey, key, counter: 9),
            _ => null,
        };

        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        Result<SignInProgress> refused = await Service.PresentAsync(
            began.Challenge,
            new FactorPresentation(Factor.Passkey) { Assertion = Synced(forger, began.WebAuthn.Challenge, counter: 10) },
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            Shape(Error.From(ErrorCodes.FactorRejected)),
            Shape(refused.Match(_ => (Error?)null, error => error)));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.Passkey)], _audit.Failed);
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Equal((held is null ? (uint?)null : 9u, null), (held?.WebAuthn?.Counter, held?.LastUsedAt));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-002b AC9, AUTH-FACT-014 AC3: the counter is among the checks an
    /// assertion verifies by, so a security key's assertion presented as
    /// <c>passkey</c> before a first factor whose counter did not advance is refused
    /// <c>auth.webauthn.countermismatch</c> as a passkey's is, its record, the failed
    /// authentication's and the failure's count committed together.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC9_ASecurityKeysAssertionWhoseCounterDidNotAdvanceIsRefusedAsAPasskeysIsAsync()
    {
        SubjectId subject = await AccountAsync();
        var key = SoftwareAuthenticator.Holding();
        Authenticator held = Counting(subject, Factor.SecurityKey, key, counter: 9);

        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _work.Reset();

        ErrorCode? refused = Refused(await Service.PresentAsync(
            began.Challenge,
            new FactorPresentation(Factor.Passkey) { Assertion = Synced(key, began.WebAuthn.Challenge, counter: 9) },
            Browser,
            Source,
            TestContext.Current.CancellationToken));

        (AuditAction action, SubjectId audited, AuthenticatorId named) = Assert.Single(_credentials.Records);

        Assert.Equal(ErrorCodes.WebAuthnCounterMismatch, refused);
        Assert.Equal(("auth.credential.countermismatch", subject, held.Id), (action.ToString(), audited, named));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.Passkey)], _audit.Failed);
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Equal((9u, false, null), (held.WebAuthn?.Counter, held.WebAuthn?.BackupState, held.LastUsedAt));
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-002b AC10: at a sign-in before a first factor, an assertion presented
    /// as <c>passkey</c> whose credential is a second-factor security key the account
    /// the sign-in is for does not hold (another account's, or any account's where the
    /// sign-in's identifier names none) is refused <c>auth.factor.rejected</c>, counted
    /// under the identifier and the source and recorded as a failed authentication
    /// under <c>passkey</c>, whether or not it would verify, and nothing of the
    /// credential is written.
    /// </summary>
    /// <param name="named">Whether the sign-in's identifier names an account.</param>
    /// <param name="verifies">Whether the assertion is the key's own.</param>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task AUTH_FACT_002b_AC10_ASecurityKeyTheSignInsAccountDoesNotHoldIsRefusedAsAWrongCredentialIsAsync(
        bool named,
        bool verifies)
    {
        SubjectId subject = await AccountAsync();
        var another = new SubjectId(Guid.NewGuid());
        var key = SoftwareAuthenticator.Holding();
        SoftwareAuthenticator presenting = verifies
            ? key
            : SoftwareAuthenticator.Holding(credentialId: key.CredentialId);
        Authenticator held = Counting(named ? another : subject, Factor.SecurityKey, key, counter: 9);

        Remembered(subject);

        SignInChallenge began = await BeganAsync(named ? Address : Elsewhere);

        _work.Reset();

        Result<SignInProgress> refused = await Service.PresentAsync(
            began.Challenge,
            new FactorPresentation(Factor.Passkey)
            {
                Assertion = Synced(presenting, began.WebAuthn.Challenge, counter: 10),
            },
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Challenge? open = await _challenges.FindAsync(
            OpaqueToken.Of(began.Challenge).Fingerprint(),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            Shape(Error.From(ErrorCodes.FactorRejected)),
            Shape(refused.Match(_ => (Error?)null, error => error)));
        Assert.Empty(Assert.IsType<Challenge>(open).Presented);
        Assert.Equal<(SubjectId?, Factor)>([(named ? subject : null, Factor.Passkey)], _audit.Failed);
        Assert.Contains(_throttle.Counted, counter => counter.Scope is ThrottleScope.Identifier);
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Empty(_credentials.Records);

        // Where the sign-in names an account, the ceremony is judged in the sign-in's
        // unit of work, which is rolled back whole before the failure's record and
        // counts commit, so nothing the judgement wrote of another account's key is
        // kept. Where it names none, no credential is read at all.
        Assert.Equal((1, named ? 1 : 0), (_work.OutermostCommitted, _work.RolledBack));

        if (!(named && verifies))
        {
            Assert.Equal(
                (9u, false, null),
                (held.WebAuthn?.Counter, held.WebAuthn?.BackupState, held.LastUsedAt));
        }

        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-014 AC9, AUTH-FACT-002b AC10: at a sign-in opened for an account, an
    /// assertion presented as <c>passkey</c> whose credential another account holds,
    /// a passkey or a second-factor security key, is refused
    /// <c>auth.factor.rejected</c> before it is verified, whatever else it would fail:
    /// its user verification flag clear, another relying party, an algorithm outside
    /// the allow-list, a backup flag differing from the stored one, a counter that did
    /// not advance, a signature that does not verify, or none of these. The refusal is
    /// counted under the identifier and the source and recorded as a failed
    /// authentication, nothing of the credential is written, no
    /// <c>auth.credential.countermismatch</c> is recorded, and the answer is the one a
    /// sign-in whose identifier names no account gives.
    /// </summary>
    /// <param name="factor">The kind of the credential the other account holds.</param>
    /// <param name="failing">What the assertion would fail at its verification.</param>
    [Theory]
    [InlineData("passkey", "userVerification")]
    [InlineData("passkey", "relyingParty")]
    [InlineData("passkey", "algorithm")]
    [InlineData("passkey", "backupFlag")]
    [InlineData("passkey", "counter")]
    [InlineData("passkey", "signature")]
    [InlineData("passkey", "nothing")]
    [InlineData("securityKey", "userVerification")]
    [InlineData("securityKey", "relyingParty")]
    [InlineData("securityKey", "algorithm")]
    [InlineData("securityKey", "backupFlag")]
    [InlineData("securityKey", "counter")]
    [InlineData("securityKey", "signature")]
    [InlineData("securityKey", "nothing")]
    public async Task AUTH_FACT_014_AC9_ACredentialTheSignInsAccountDoesNotHoldIsRefusedBeforeItIsVerifiedAsync(
        string factor,
        string failing)
    {
        SubjectId subject = await AccountAsync();
        var another = new SubjectId(Guid.NewGuid());
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var forger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string relyingParty = failing is "relyingParty" ? "elsewhere.test" : "example.test";
        AuthenticatorId held = Keyed(
            another,
            key,
            counter: 9,
            relyingParty,
            algorithm: failing is "algorithm" ? SoftwareAuthenticator.EdDsa : SoftwareAuthenticator.Es256,
            factor is "securityKey" ? Factor.SecurityKey : Factor.Passkey);

        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _work.Reset();

        Result<SignInProgress> refused = await Service.PresentAsync(
            began.Challenge,
            new FactorPresentation(Factor.Passkey) { Assertion = Failing(began) },
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Challenge? open = await _challenges.FindAsync(
            OpaqueToken.Of(began.Challenge).Fingerprint(),
            TestContext.Current.CancellationToken);
        Authenticator? after = await _authenticators.FindAsync(held, TestContext.Current.CancellationToken);

        Assert.Equal(
            Shape(Error.From(ErrorCodes.FactorRejected)),
            Shape(refused.Match(_ => (Error?)null, error => error)));
        Assert.Empty(Assert.IsType<Challenge>(open).Presented);
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.Passkey)], _audit.Failed);
        Assert.Contains(_throttle.Counted, counter => counter.Scope is ThrottleScope.Identifier);
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Empty(_credentials.Records);
        Assert.Equal(
            (9u, false, null),
            (after?.WebAuthn?.Counter, after?.WebAuthn?.BackupState, after?.LastUsedAt));
        Assert.Equal(1, _work.OutermostCommitted);
        Assert.False(_work.Open);

        SignInChallenge nobodys = await BeganAsync(Elsewhere);

        Result<SignInProgress> unnamed = await Service.PresentAsync(
            nobodys.Challenge,
            new FactorPresentation(Factor.Passkey) { Assertion = Failing(nobodys) },
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            Shape(unnamed.Match(_ => (Error?)null, error => error)),
            Shape(refused.Match(_ => (Error?)null, error => error)));

        AuthenticatorAssertion Failing(SignInChallenge opened) =>
            Asserted(
                failing is "signature" ? forger : key,
                opened.WebAuthn.Challenge,
                counter: failing is "counter" ? 9u : 10u,
                relyingParty,
                flags: failing switch
                {
                    "userVerification" => SoftwareAuthenticator.UserPresent,
                    "backupFlag" => SoftwareAuthenticator.UserPresent
                        | SoftwareAuthenticator.UserVerified
                        | SoftwareAuthenticator.BackupEligible,
                    _ => SoftwareAuthenticator.UserPresent | SoftwareAuthenticator.UserVerified,
                });
    }

    /// <summary>
    /// AUTH-FACT-014 AC9: at a step-up, an assertion whose credential another account
    /// holds, its user verification flag clear, is refused <c>auth.factor.rejected</c>
    /// before it is verified, recorded as a refused step-up factor and counted, and
    /// nothing of the credential is written.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_014_AC9_AtAStepUpACredentialTheAccountDoesNotHoldIsRefusedBeforeItIsVerifiedAsync()
    {
        SubjectId subject = await AccountAsync();
        var another = new SubjectId(Guid.NewGuid());
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        AuthenticatorId held = Keyed(another, key, counter: 9);
        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);

        ErrorCode? refused = Refused(await Service.StepUpAsync(
            AccessContext.Of(subject),
            session,
            began.Challenge,
            new FactorPresentation(Factor.Passkey)
            {
                Assertion = Asserted(
                    key,
                    began.WebAuthn.Challenge,
                    counter: 10,
                    flags: SoftwareAuthenticator.UserPresent),
            },
            Source,
            TestContext.Current.CancellationToken));

        Authenticator? after = await _authenticators.FindAsync(held, TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.FactorRejected, refused);
        Assert.Equal(
            [(session, subject, Factor.Passkey)],
            _audit.StepUpsFailed.Select(failed => (failed.Session, failed.Subject, failed.Presented)));
        Assert.Equal(
            1,
            (await _throttle.FindAsync(ThrottleScope.Source, Source, TestContext.Current.CancellationToken))?.Failures);
        Assert.Empty(_credentials.Records);
        Assert.Equal(
            (9u, false, null),
            (after?.WebAuthn?.Counter, after?.WebAuthn?.BackupState, after?.LastUsedAt));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-002b AC8: at a sign-in, once a password is accepted, the answer that
    /// offers the security key carries the request options for the challenge: user
    /// verification required, the value the sign-in was opened with, and the account's
    /// active second-factor security keys by credential identifier and transports. A
    /// suspended one, another account's and a passkey are not named. The security
    /// key's assertion made from those options completes the sign-in.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC8_AtASignInTheAnswerOfferingTheSecurityKeyCarriesItsRequestOptionsAsync()
    {
        SubjectId subject = await AccountAsync();
        var another = new SubjectId(Guid.NewGuid());
        var key = SoftwareAuthenticator.Holding();
        var spare = SoftwareAuthenticator.Holding();

        _ = Carries(subject, Factor.SecurityKey, key, "Key", "usb", "nfc");
        _ = Carries(subject, Factor.SecurityKey, spare, "Spare");
        Carries(subject, Factor.SecurityKey, SoftwareAuthenticator.Holding(), "Lost", "usb")
            .Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        _ = Carries(subject, Factor.Passkey, SoftwareAuthenticator.Holding(), "Phone", "internal");
        _ = Carries(another, Factor.SecurityKey, SoftwareAuthenticator.Holding(), "Theirs", "usb");
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);
        SignInProgress first = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));
        WebAuthnRequestOptions options = Assert.IsType<WebAuthnRequestOptions>(first.WebAuthn);

        SignInProgress second = Reached(await Service.PresentAsync(
            began.Challenge,
            new FactorPresentation(Factor.SecurityKey)
            {
                Assertion = key.Asserted(options.Challenge, "https://example.test", options.RelyingPartyId),
            },
            Browser,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(SignInStatus.FactorRequired, first.Status);
        Assert.Equal([Factor.SecurityKey], first.Required);
        Assert.Equal("required", options.UserVerification);
        Assert.Equal("example.test", options.RelyingPartyId);
        Assert.Equal(began.WebAuthn.Challenge, options.Challenge);
        Assert.Equal(
            [
                (Base64Url.EncodeToString(key.CredentialId), "usb,nfc"),
                (Base64Url.EncodeToString(spare.CredentialId), string.Empty),
            ],
            Described(options));
        Assert.Equal(
            (SignInStatus.Complete, AssuranceLevel.Aal2, true),
            (second.Status, second.AssuranceLevel, second.PhishingResistant));
        Assert.Null(second.WebAuthn);
    }

    /// <summary>
    /// AUTH-FACT-002b AC8: an answer that does not offer the security key carries no
    /// request options: one that offers another second step, on an account whose only
    /// security key is suspended, and the one that completes the sign-in.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC8_AnAnswerThatDoesNotOfferTheSecurityKeyCarriesNoRequestOptionsAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Carries(subject, Factor.SecurityKey, SoftwareAuthenticator.Holding(), "Lost", "usb")
            .Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);
        string generated = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);

        SignInProgress first = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));
        SignInProgress second = Reached(await PresentAsync(began.Challenge, Factor.Totp, generated));

        Assert.Equal(SignInStatus.FactorRequired, first.Status);
        Assert.Equal([Factor.Totp], first.Required);
        Assert.Null(first.WebAuthn);
        Assert.Equal(SignInStatus.Complete, second.Status);
        Assert.Null(second.WebAuthn);
    }

    /// <summary>
    /// AUTH-FACT-002b AC8, AUTH-ABUSE-003: the challenge a sign-in opens with names no
    /// credential. For an account holding a security key and a passkey it carries the
    /// relying party and the value to sign and nothing else, as it does for an
    /// identifier no account holds, and no credential is read for it.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC8_TheChallengeASignInOpensWithNamesNoCredentialAsync()
    {
        SubjectId subject = await AccountAsync();

        _ = Carries(subject, Factor.SecurityKey, SoftwareAuthenticator.Holding(), "Key", "usb");
        _ = Carries(subject, Factor.Passkey, SoftwareAuthenticator.Holding(), "Phone", "internal");

        int read = _authenticators.Reads;

        SignInChallenge held = await BeganAsync(Address);
        SignInChallenge nobody = await BeganAsync(Elsewhere);

        Assert.Equal(read, _authenticators.Reads);
        Assert.Equal(new WebAuthnChallenge("example.test", held.WebAuthn.Challenge), held.WebAuthn);
        Assert.Equal(new WebAuthnChallenge("example.test", nobody.WebAuthn.Challenge), nobody.WebAuthn);
        Assert.Equal(
            ["Challenge", "RelyingPartyId"],
            typeof(WebAuthnChallenge).GetProperties().Select(member => member.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AUTH-FACT-002b AC8: at a step-up no answer carries the request options until a
    /// factor has been presented on its challenge; once the password has been, the
    /// answer that offers the security key carries them, and since it offers the
    /// passkey too it names the account's active passkeys beside its security keys, so
    /// that a passkey answers from those options. A suspended passkey is not named.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC8_AtAStepUpTheRequestOptionsFollowAFactorAndNameThePasskeysOfferedAsync()
    {
        SubjectId subject = await AccountAsync();
        var key = SoftwareAuthenticator.Holding();
        var passkey = SoftwareAuthenticator.Holding();
        var gate = new Gate(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5));

        _ = Carries(subject, Factor.SecurityKey, key, "Key", "usb");
        _ = Carries(subject, Factor.Passkey, passkey, "Phone", "internal", "hybrid");
        Carries(subject, Factor.Passkey, SoftwareAuthenticator.Holding(), "Lost", "internal")
            .Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        Holds(subject, Factor.PhoneCode);
        Declares(gate, gate, Factor.PhoneCode);
        Answers(PhoneSignal.Risk);

        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);

        SignInProgress before = await LeftAsync();
        SignInProgress first = Reached(await SteppedAsync(new FactorPresentation(Factor.Password) { Value = Secret }));
        SignInProgress after = await LeftAsync();
        WebAuthnRequestOptions options = Assert.IsType<WebAuthnRequestOptions>(first.WebAuthn);

        SignInProgress second = Reached(await SteppedAsync(new FactorPresentation(Factor.Passkey)
        {
            Assertion = passkey.Asserted(
                options.Challenge,
                "https://example.test",
                options.RelyingPartyId,
                userHandle: WebAuthnService.Handle(subject)),
        }));

        Assert.Equal(SignInStatus.FactorRequired, before.Status);
        Assert.Contains(Factor.SecurityKey, before.Required);
        Assert.Null(before.WebAuthn);
        Assert.Equal(SignInStatus.FactorRequired, first.Status);
        Assert.Equal([Factor.Passkey, Factor.SecurityKey], first.Required.Order());
        Assert.Equal("required", options.UserVerification);
        Assert.Equal("example.test", options.RelyingPartyId);
        Assert.Equal(began.WebAuthn.Challenge, options.Challenge);
        Assert.Equal(
            new[]
            {
                (Base64Url.EncodeToString(key.CredentialId), "usb"),
                (Base64Url.EncodeToString(passkey.CredentialId), "internal,hybrid"),
            }.Order(),
            Described(options).Order());
        Assert.Equal(first.Required, after.Required);
        Assert.Equal(options.Challenge, Assert.IsType<WebAuthnRequestOptions>(after.WebAuthn).Challenge);
        Assert.Equal(Described(options), Described(after.WebAuthn!));
        Assert.Equal(
            (SignInStatus.Complete, AssuranceLevel.Aal2, true),
            (second.Status, second.AssuranceLevel, second.PhishingResistant));
        Assert.Null(second.WebAuthn);

        ValueTask<Result<SignInProgress>> SteppedAsync(FactorPresentation presented) =>
            Service.StepUpAsync(
                AccessContext.Of(subject),
                session,
                began.Challenge,
                presented,
                Source,
                TestContext.Current.CancellationToken);

        async ValueTask<SignInProgress> LeftAsync() =>
            Assert.IsType<SignInProgress>((await AskedAsync(began.Challenge, subject, session)).Match(
                progress => progress,
                error => throw new Xunit.Sdk.XunitException(error.Code.ToString())));
    }

    /// <summary>
    /// AUTH-FACT-002b AC8: at a step-up that does not offer the passkey, the account's
    /// only one being suspended, the request options name the account's active
    /// security keys and no passkey; the security key, presented after the password of
    /// its combination, answers from those options and the two reach <c>aal2</c>,
    /// phishing-resistant.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC8_AtAStepUpThatOffersNoPasskeyTheRequestOptionsNameTheSecurityKeysAloneAsync()
    {
        SubjectId subject = await AccountAsync();
        var key = SoftwareAuthenticator.Holding();
        var gate = new Gate(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5));

        _ = Carries(subject, Factor.SecurityKey, key, "Key", "usb", "nfc");
        Carries(subject, Factor.SecurityKey, SoftwareAuthenticator.Holding(), "Spare", "usb")
            .Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        Carries(subject, Factor.Passkey, SoftwareAuthenticator.Holding(), "Phone", "internal")
            .Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        Declares(gate, gate);

        SessionId session = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);

        SignInProgress first = Reached(await SteppedAsync(new FactorPresentation(Factor.Password) { Value = Secret }));
        WebAuthnRequestOptions options = Assert.IsType<WebAuthnRequestOptions>(first.WebAuthn);

        SignInProgress second = Reached(await SteppedAsync(new FactorPresentation(Factor.SecurityKey)
        {
            Assertion = key.Asserted(options.Challenge, "https://example.test", options.RelyingPartyId),
        }));

        Assert.Equal(SignInStatus.FactorRequired, first.Status);
        Assert.Equal([Factor.SecurityKey], first.Required);
        Assert.Equal("required", options.UserVerification);
        Assert.Equal(began.WebAuthn.Challenge, options.Challenge);
        Assert.Equal([(Base64Url.EncodeToString(key.CredentialId), "usb,nfc")], Described(options));
        Assert.Equal(
            (SignInStatus.Complete, AssuranceLevel.Aal2, true),
            (second.Status, second.AssuranceLevel, second.PhishingResistant));
        Assert.Null(second.WebAuthn);

        ValueTask<Result<SignInProgress>> SteppedAsync(FactorPresentation presented) =>
            Service.StepUpAsync(
                AccessContext.Of(subject),
                session,
                began.Challenge,
                presented,
                Source,
                TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Chapter 09 <c>POST /auth/factor</c>: in the usual order a first factor is
    /// accepted and what is left to present is the second steps the challenge offers,
    /// the factor accepted never among them; one of them, presented, then completes
    /// the sign-in.
    /// </summary>
    [Fact]
    public async Task PresentAsync_AFirstFactorAccepted_RequiresTheSecondStepsLeftAndNoFactorAcceptedAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.SecurityKey);
        Holds(subject, Factor.Totp, isPreferred: true);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);
        string generated = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);

        SignInProgress first = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));
        SignInProgress second = Reached(await PresentAsync(began.Challenge, first.Required[0], generated));

        Assert.Equal(SignInStatus.FactorRequired, first.Status);
        Assert.Equal(AssuranceLevel.Aal1, first.AssuranceLevel);
        Assert.Equal([Factor.Totp, Factor.SecurityKey], first.Required);
        Assert.DoesNotContain(Factor.Password, first.Required);
        Assert.Empty(first.Required.Intersect(began.Available));
        Assert.Equal(SignInStatus.Complete, second.Status);
        Assert.Equal(AssuranceLevel.Aal2, second.AssuranceLevel);
        Assert.Empty(second.Required);
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

        SignInLanding opened = await LandedAsync(browser, token, press: false);

        Assert.Null(opened.SignedIn);
        Assert.True(opened.SameBrowser);
        Assert.Null(opened.Code);

        SignInLanding away = await LandedAsync(browser: null, token, press: true);

        Assert.Null(away.SignedIn);
        Assert.False(away.SameBrowser);
        Assert.NotNull(away.Code);

        SignInLanding pressed = await LandedAsync(browser, token, press: true);

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

        SignInLanding away = await LandedAsync(browser: null, token, press: true);

        Assert.NotNull(away.Code);

        Result<SignInProgress> typed = await PresentAsync(challenge, Factor.EmailLink, away.Code);

        Assert.Equal(SignInStatus.Complete, Reached(typed).Status);
    }

    /// <summary>
    /// AUTH-FACT-003 AC4 (D-226): the link is issued on the asking browser's latest
    /// sign-in opened with the identifier, so the code it shows completes that one, and
    /// is no code of an earlier one or of another browser's.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_003_AC4_ALinkIsIssuedOnTheAskingBrowsersLatestSignInAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailLink);
        Remembered(subject);

        var browser = OpaqueToken.Draw(_randomness);
        SignInChallenge earlier = await BeganAsync(Address, browser.Value);

        _clock.Advance(TimeSpan.FromSeconds(1));

        SignInChallenge latest = await BeganAsync(Address, browser.Value);

        _clock.Advance(TimeSpan.FromSeconds(1));

        SignInChallenge elsewhere = await BeganAsync(Address, OpaqueToken.Draw(_randomness).Value);

        _ = await Service.SendLinkAsync(Address, Language, Source, browser.Value, TestContext.Current.CancellationToken);

        string shown = Assert.IsType<string>((await LandedAsync(browser: null, Token(), press: true)).Code);

        Assert.Equal(ErrorCodes.CodeExpired, Refused(await PresentAsync(earlier.Challenge, Factor.EmailLink, shown)));
        Assert.Equal(ErrorCodes.CodeExpired, Refused(await PresentAsync(elsewhere.Challenge, Factor.EmailLink, shown)));
        Assert.Equal(SignInStatus.Complete, Reached(await PresentAsync(latest.Challenge, Factor.EmailLink, shown)).Status);
    }

    /// <summary>
    /// AUTH-FACT-003 AC4 (D-226): a sign-in on which a factor has been accepted is not
    /// one a link is issued on, so the link opens a sign-in of its own for the browser,
    /// which its press completes in that browser.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_003_AC4_ASignInAFactorAnsweredIsNotOneALinkIsIssuedOnAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);
        Enables(Factor.EmailLink);
        Remembered(subject);

        var browser = OpaqueToken.Draw(_randomness);
        SignInChallenge began = await BeganAsync(Address, browser.Value);

        Assert.Equal(SignInStatus.FactorRequired, Reached(await PresentAsync(began.Challenge, Factor.Password, Secret)).Status);

        _ = await Service.SendLinkAsync(Address, Language, Source, browser.Value, TestContext.Current.CancellationToken);

        string token = Token();
        string shown = Assert.IsType<string>((await LandedAsync(browser: null, token, press: false)).Code);

        Assert.Equal(ErrorCodes.CodeExpired, Refused(await PresentAsync(began.Challenge, Factor.EmailLink, shown)));
        Assert.NotNull((await LandedAsync(browser.Value, token, press: true)).SignedIn);
    }

    /// <summary>
    /// AUTH-FACT-003 AC4 (D-226, D-230): a link asked for from a browser holding no
    /// sign-in opens one bound to that browser, which its press completes there and
    /// nowhere else; a link asked for from a browser carrying no pre-authentication
    /// session is issued on a sign-in bound to none, which no press completes, and the
    /// code its opening shows, typed on the sign-in the answer names, completes it.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_003_AC4_ALinkAskedWithoutASignInOpensOneBoundToTheBrowserAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailLink);
        Remembered(subject);

        var browser = OpaqueToken.Draw(_randomness);

        string? opened = await LinkedAsync(Address, browser: null);
        SignInLanding unbound = await LandedAsync(browser: null, Token(), press: true);
        Result<SignInProgress> typed = await PresentAsync(opened!, Factor.EmailLink, unbound.Code);

        _ = await LinkedAsync(Address, browser.Value);

        string token = Token();
        SignInLanding away = await LandedAsync(OpaqueToken.Draw(_randomness).Value, token, press: true);
        SignInLanding pressed = await LandedAsync(browser.Value, token, press: true);

        Assert.Equal((null, false), (unbound.SignedIn, unbound.SameBrowser));
        Assert.NotNull(opened);
        Assert.NotNull(unbound.Code);
        Assert.Equal(SignInStatus.Complete, typed.Match(progress => progress.Status, error => throw new InvalidOperationException(error.Code.ToString())));
        Assert.Equal((null, false), (away.SignedIn, away.SameBrowser));
        Assert.Equal(SignInStatus.Complete, pressed.SignedIn?.Status);
    }

    /// <summary>
    /// AUTH-FACT-003 AC6 (D-230): a link asked for from a browser holding no sign-in
    /// opened with the identifier is answered with the sign-in its request opened, and
    /// the code the link shows in another browser, typed on it, completes it; asked again
    /// from that browser, or from one that began a sign-in with the identifier, it is
    /// issued on the sign-in the browser holds and the answer names none.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_003_AC6_ABrowserHoldingNoSignInIsAnsweredWithTheOneItsRequestOpenedAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailLink);
        Remembered(subject);

        var browser = OpaqueToken.Draw(_randomness);
        var began = OpaqueToken.Draw(_randomness);

        string? first = await LinkedAsync(Address, browser.Value);
        string? again = await LinkedAsync(Address, browser.Value);
        SignInLanding away = await LandedAsync(OpaqueToken.Draw(_randomness).Value, Token(), press: true);
        Result<SignInProgress> typed = await PresentAsync(first!, Factor.EmailLink, away.Code);

        _ = await BeganAsync(Address, began.Value);

        string? beside = await LinkedAsync(Address, began.Value);

        Assert.NotNull(first);
        Assert.Null(again);
        Assert.Equal(SignInStatus.Complete, typed.Match(progress => progress.Status, error => throw new InvalidOperationException(error.Code.ToString())));
        Assert.Null(beside);
    }

    /// <summary>
    /// AUTH-FACT-003 AC6, AUTH-ABUSE-002 (D-230): whether the answer names a sign-in turns
    /// on the browser's sign-ins alone, never on the account: a browser holding none is
    /// answered with the one its request opened, and one that began a sign-in with the
    /// identifier with none, for an identifier no account holds, one that reads as
    /// neither an address nor a number, a channel the policy has not enabled, an address
    /// a domain lock refuses and a number reported as at risk alike, as for one the link
    /// goes to.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_003_AC6_WhetherTheAnswerNamesASignInNeverTurnsOnTheAccountAsync()
    {
        SubjectId subject = await AccountAsync();

        Remembered(subject);

        (string? Opened, string? Held) disabled = await AnsweredAsync(Address);
        (string? Opened, string? Held) nobodys = await AnsweredAsync(Elsewhere);
        (string? Opened, string? Held) unread = await AnsweredAsync("neither-an-address-nor-a-number");

        Enables(Factor.EmailLink);

        (string? Opened, string? Held) sent = await AnsweredAsync(Address);

        LockedElsewhere(subject);

        (string? Opened, string? Held) locked = await AnsweredAsync(Address);

        Enables(Factor.PhoneLink);
        Answers(PhoneSignal.Risk);

        (string? Opened, string? Held) risk = await AnsweredAsync(Number);

        Assert.All(
            [disabled, nobodys, unread, sent, locked, risk],
            answered =>
            {
                Assert.NotNull(answered.Opened);
                Assert.Null(answered.Held);
            });
    }

    /// <summary>
    /// AUTH-FACT-003 AC4 (D-226): the sign-in a link is issued on answers at least as
    /// long as the link, so a press after the sign-in's own lifetime and inside the
    /// link's completes it.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_003_AC4_TheSignInALinkIsIssuedOnLivesAsLongAsTheLinkAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailLink);
        Remembered(subject);
        _configuration.Set(Settings.CodeVerificationLifetime, TimeSpan.FromMinutes(5));
        _configuration.Set(Settings.LinkMagicLifetime, TimeSpan.FromMinutes(15));

        (_, string token, string browser) = await AskedAsync(subject);

        _clock.Advance(TimeSpan.FromMinutes(14));

        Assert.Equal(SignInStatus.Complete, (await LandedAsync(browser, token, press: true)).SignedIn?.Status);
    }

    /// <summary>
    /// AUTH-FACT-003 AC5: asking for a link on a channel whose factor the policy has
    /// not enabled answers as an identifier no account holds does, and sends nothing.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_003_AC5_ALinkOnADisabledChannelSendsNothingAndSaysSoAsync()
    {
        SubjectId subject = await AccountAsync();

        Result<RequestedLink> asked = await Service.SendLinkAsync(
            Address,
            Language,
            Source,
            browser: null,
            TestContext.Current.CancellationToken);

        Assert.True(asked.Match(_ => true, _ => false));
        Assert.Null(await _pending.FindAsync(subject, Factor.EmailLink, TestContext.Current.CancellationToken));
        Assert.Empty(_notifications.Mail);
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

        string carried = _notifications.Texts[^1].Token();

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
    /// CONV-DESIGN-003 (D-207): a sign-in link, which may go by text and so ask the
    /// carrier's signal of the number, asked inside a unit of work another opened is a
    /// fault of the calling code, for a number an account holds and for one none holds:
    /// it ends the level it joined, so nothing of the outer unit of work commits and
    /// nothing is sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_ALinkAskedInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneLink);
        _work.Reset();

        foreach (string number in new[] { Number, "+441632960099" })
        {
            _ = await _work.BeginAsync(TestContext.Current.CancellationToken);

            _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await Service.SendLinkAsync(number, Language, Source, browser: null, TestContext.Current.CancellationToken));
            _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _work.CommitAsync(TestContext.Current.CancellationToken));

            Assert.False(_work.Open);
        }

        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneLink, TestContext.Current.CancellationToken));
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
            .Match(_ => true, _ => false));

        Assert.Empty(_notifications.Texts);
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

        Assert.NotEmpty(_notifications.Texts);
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
            new SessionOrigin(Source, Browser),
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
        Assert.Single(_notifications.Mail);
    }

    /// <summary>
    /// AUTH-ABUSE-004 and chapter 10 section 5.14: the new-device check code is asked for
    /// by the request that presented the password, so it is sent under that request's
    /// source and never under the address it goes to (D-166, 342).
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_TheCheckCodeIsSentUnderTheSourceThatAskedAsync()
    {
        await AccountAsync();

        SignInChallenge began = await BeganAsync(Address);

        _ = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));

        Assert.Equal(Source, Assert.Single(_notifications.Mail).Source);
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
            Source,
            TestContext.Current.CancellationToken);

        SignInProgress reached = Reached(completed);

        Assert.Equal(SignInStatus.Complete, reached.Status);
        Assert.Equal(AssuranceLevel.Aal1, reached.AssuranceLevel);
        Assert.NotNull(reached.Session);
    }

    /// <summary>
    /// AUTH-FACT-016 AC8 (D-224): an email code sent to the primary email, presented
    /// from a browser the account has not been seen on, completes without the check,
    /// remembers the browser and raises <c>DeviceVerified</c>; no check code is sent.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_016_AC8_AnEmailCodeToThePrimaryEmailCompletesWithoutTheCheckAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailCode);

        SignInChallenge began = await BeganAsync(Address);

        _ = await Service.SendCodeAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        int sent = _notifications.Mail.Count;
        SignInProgress reached = Reached(await PresentAsync(began.Challenge, Factor.EmailCode, Code()));

        Assert.Equal(SignInStatus.Complete, reached.Status);
        Assert.NotNull(reached.Session);
        Assert.Equal(sent, _notifications.Mail.Count);
        await RememberedOnceAsync(subject);
    }

    /// <summary>
    /// AUTH-FACT-016 AC8 (D-224): a sign-in link sent to the primary email, pressed in
    /// the browser that asked for it, completes without the check and remembers the
    /// browser.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_016_AC8_ALinkToThePrimaryEmailCompletesWithoutTheCheckAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailLink);

        (string challenge, string token, string browser) = await AskedAsync(subject);

        SignInLanding pressed = await LandedAsync(browser, token, press: true);

        Assert.Equal(SignInStatus.Complete, pressed.SignedIn!.Status);
        Assert.NotNull(pressed.SignedIn.Session);
        await RememberedOnceAsync(subject);
    }

    /// <summary>
    /// AUTH-FACT-016 AC8 (D-224): the code a sign-in link's page showed, for a link sent
    /// to the primary email and typed where the sign-in began, completes without the
    /// check and remembers the browser.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_016_AC8_TheCodeALinksPageShowedCompletesWithoutTheCheckAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailLink);

        (string challenge, string token, _) = await AskedAsync(subject);

        SignInLanding away = await LandedAsync(browser: null, token, press: true);
        SignInProgress reached = Reached(await PresentAsync(challenge, Factor.EmailLink, away.Code!));

        Assert.Equal(SignInStatus.Complete, reached.Status);
        await RememberedOnceAsync(subject);
    }

    /// <summary>
    /// AUTH-FACT-016 AC8 (D-224): an email code sent to another verified email proved
    /// no primary mailbox, so the sign-in is held and its check's code goes to the
    /// primary email.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_016_AC8_AnEmailCodeToAnotherEmailIsHeldWithItsCodeToThePrimaryAsync()
    {
        SubjectId subject = await AccountAsync();
        _ = _identifiers.Verified(subject, IdentifierKind.Email, Second);

        Enables(Factor.EmailCode);

        SignInChallenge began = await BeganAsync(Second);

        _ = await Service.SendCodeAsync(Second, Language, Source, TestContext.Current.CancellationToken);

        SignInProgress reached = Reached(await PresentAsync(began.Challenge, Factor.EmailCode, Code()));

        Assert.Equal(SignInStatus.DeviceVerificationRequired, reached.Status);
        Assert.Null(reached.Session);
        Assert.Equal(Address, _notifications.Mail[^1].Destination.Canonical);
        Assert.Equal(MessageKind.VerificationCode, _notifications.Mail[^1].Message);
        Assert.Empty(_events.Published.OfType<DeviceVerified>());
    }

    /// <summary>
    /// AUTH-FACT-016 AC8 (D-224): a sign-in link sent to a phone proved no mailbox, so
    /// the sign-in is held and its check's code goes to the primary email.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_016_AC8_APhoneLinkIsHeldWithItsCodeToThePrimaryAsync()
    {
        _ = await AccountAsync();

        Enables(Factor.PhoneLink);

        SignInChallenge began = await BeganAsync(Number);
        var browser = OpaqueToken.Draw(_randomness);

        _ = await Service.SendLinkAsync(Number, Language, Source, browser.Value, TestContext.Current.CancellationToken);

        SignInLanding pressed = (await Service.LandAsync(
                browser.Value,
                _notifications.Texts[^1].Token(),
                Factor.PhoneLink,
                press: true,
                Browser,
                Source,
                TestContext.Current.CancellationToken))
            .Match(landing => landing, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(SignInStatus.DeviceVerificationRequired, pressed.SignedIn!.Status);
        Assert.Equal(Address, _notifications.Mail[^1].Destination.Canonical);
        Assert.Empty(_events.Published.OfType<DeviceVerified>());
    }

    /// <summary>
    /// AUTH-FACT-016 AC9 (D-224): where the email destination restriction refuses the
    /// check's code, the factor stays accepted and the sign-in held at the check; a
    /// resend while the restriction still refuses is refused as it was, and one after
    /// sends a code; the code before a resend no longer verifies, and the last one
    /// completes the sign-in.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_016_AC9_ARefusedCheckCodeIsSentByAResendOnceTheRestrictionAllowsItAsync()
    {
        await AccountAsync();

        var browser = OpaqueToken.Draw(_randomness);
        SignInChallenge began = await BeganAsync(Address, browser.Value);

        _notifications.Refusal = Error.From(ErrorCodes.RestrictionExceeded);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refused(await PresentAsync(began.Challenge, Factor.Password, Secret)));
        Assert.Empty(_notifications.Mail);
        Assert.Equal(ErrorCodes.RestrictionExceeded, Code(await ResentAsync(began.Challenge, browser.Value)));

        _notifications.Refusal = null;

        Assert.Null(Code(await ResentAsync(began.Challenge, browser.Value)));

        string first = Code();

        Assert.Null(Code(await ResentAsync(began.Challenge, browser.Value)));

        string last = Code();

        Assert.Equal(2, _notifications.Mail.Count);
        Assert.All(_notifications.Mail, sent => Assert.Equal(MessageKind.VerificationCode, sent.Message));
        Assert.All(_notifications.Mail, sent => Assert.Equal(Address, sent.Destination.Canonical));

        if (!string.Equals(first, last, StringComparison.Ordinal))
        {
            Assert.NotNull(Refused(await VerifiedAsync(began.Challenge, first)));
        }

        SignInProgress completed = Reached(await VerifiedAsync(began.Challenge, last));

        Assert.Equal(SignInStatus.Complete, completed.Status);
        Assert.Equal(AssuranceLevel.Aal1, completed.AssuranceLevel);
    }

    /// <summary>
    /// AUTH-FACT-016 AC9 (D-224, D-226): a resend is answered <c>auth.code.expired</c>,
    /// one answer for every cause, and sends nothing, for a handle that opens nothing,
    /// a sign-in never held at the check, one held but asked from another browser or
    /// from none, one bound to no browser, and one completed since.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_016_AC9_AResendIsRefusedWhereNoSignInIsHeldAtTheCheckAsync()
    {
        await AccountAsync();

        var browser = OpaqueToken.Draw(_randomness);
        SignInChallenge unheld = await BeganAsync(Address, browser.Value);
        SignInChallenge held = await BeganAsync(Address, browser.Value);
        SignInChallenge unbound = await BeganAsync(Address);

        _ = Reached(await PresentAsync(held.Challenge, Factor.Password, Secret));

        string code = Code();

        _ = Reached(await PresentAsync(unbound.Challenge, Factor.Password, Secret));

        _clock.Advance(TimeSpan.FromMinutes(2));

        int sent = _notifications.Mail.Count;

        ErrorCode?[] answers =
        [
            Code(await ResentAsync("a-handle-nothing-opened", browser.Value)),
            Code(await ResentAsync(unheld.Challenge, browser.Value)),
            Code(await ResentAsync(held.Challenge, OpaqueToken.Draw(_randomness).Value)),
            Code(await ResentAsync(held.Challenge, browser: null)),
            Code(await ResentAsync(unbound.Challenge, browser: null)),
        ];

        Assert.Equal(SignInStatus.Complete, Reached(await VerifiedAsync(held.Challenge, code)).Status);

        ErrorCode? completed = Code(await ResentAsync(held.Challenge, browser.Value));

        Assert.All([.. answers, completed], answer => Assert.Equal(ErrorCodes.CodeExpired, answer));
        Assert.Equal(sent, _notifications.Mail.Count);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// AUTH-FACT-016 AC9 and AUTH-ABUSE-001: a resend waits out the delay the sign-in's
    /// identifier has earned, answered <c>auth.throttled</c>, and sends nothing.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_016_AC9_AResendWaitsOutTheDelayAsync()
    {
        await AccountAsync();

        var browser = OpaqueToken.Draw(_randomness);
        SignInChallenge held = await BeganAsync(Address, browser.Value);

        _ = Reached(await PresentAsync(held.Challenge, Factor.Password, Secret));

        int sent = _notifications.Mail.Count;

        await FailedAsync(Address, Fresh, Settings.AbuseThrottleThreshold.Default);

        Assert.Equal(ErrorCodes.Throttled, Code(await ResentAsync(held.Challenge, browser.Value)));
        Assert.Equal(sent, _notifications.Mail.Count);
    }

    /// <summary>
    /// AUTH-SESS-005 AC4 (D-224 item 13): the restore of a lapsed session begins with
    /// each of the passkey and the password the account holds and the policy permits,
    /// and request options asking user verification and naming the account's active
    /// passkeys, a suspended one and a second-factor security key not among them; the
    /// password alone is named where the account holds no passkey, and neither where
    /// the policy permits neither.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005_AC4_ARestoreBeginsWithTheFactorsTheAccountHoldsAndThePolicyPermitsAsync()
    {
        SubjectId subject = await AccountAsync();
        SubjectId other = await AccountAsync();
        var passkey = SoftwareAuthenticator.Holding();

        _ = Carries(subject, Factor.Passkey, passkey, "Phone", "internal");
        _ = Carries(subject, Factor.SecurityKey, SoftwareAuthenticator.Holding(), "Key", "usb");
        Carries(subject, Factor.Passkey, SoftwareAuthenticator.Holding(), "Lost", "internal")
            .Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));

        OpaqueToken lapsed = Lapsed(subject);
        OpaqueToken unkeyed = Lapsed(other);

        RestoreChallenge began = Begun(await RestoreBegunAsync(lapsed));
        RestoreChallenge passwordOnly = Begun(await RestoreBegunAsync(unkeyed));

        _configuration.Set(
            Settings.PolicyDefault,
            Janus.Core.Policies.SystemDefault with
            {
                RequiredAssurance = AssuranceLevel.Aal2,
                LoginFactors = new HashSet<Factor>([Factor.Totp, Factor.Google]),
            });

        RestoreChallenge unpermitted = Begun(await RestoreBegunAsync(lapsed));

        Assert.Equal([Factor.Passkey, Factor.Password], began.Factors);
        Assert.Equal("required", began.WebAuthn.UserVerification);
        Assert.Equal("example.test", began.WebAuthn.RelyingPartyId);
        Assert.Equal([(Base64Url.EncodeToString(passkey.CredentialId), "internal")], Described(began.WebAuthn));
        Assert.Equal([Factor.Password], passwordOnly.Factors);
        Assert.Empty(passwordOnly.WebAuthn.AllowCredentials);
        Assert.Empty(unpermitted.Factors);
        Assert.NotEqual(began.Challenge, unpermitted.Challenge);
    }

    /// <summary>
    /// AUTH-SESS-005 AC4 (D-224 item 13): the password restores the lapsed session in
    /// place: the same session, a new secret, complete at what the password proves,
    /// and the challenge ends with it.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005_AC4_ThePasswordRestoresTheLapsedSessionInPlaceAsync()
    {
        SubjectId subject = await AccountAsync();
        OpaqueToken lapsed = Lapsed(subject);
        Session before = (await _live.FindByFingerprintAsync(lapsed.Fingerprint(), TestContext.Current.CancellationToken))!;

        RestoreChallenge began = Begun(await RestoreBegunAsync(lapsed));
        SignInOutcome restored = Restored(await RestoredAsync(
            lapsed,
            began.Challenge,
            new FactorPresentation(Factor.Password) { Value = Secret }));

        IssuedSession issued = Assert.IsType<IssuedSession>(restored.Session);

        Assert.Equal(
            (SignInStatus.Complete, AssuranceLevel.Aal1, false, before.Id),
            (restored.Progress.Status, restored.Progress.AssuranceLevel, restored.Progress.PhishingResistant, issued.Id));
        Assert.Empty(restored.Progress.Required);
        Assert.Null(await _live.FindByFingerprintAsync(lapsed.Fingerprint(), TestContext.Current.CancellationToken));
        Assert.Equal(before.Id, (await _live.FindByFingerprintAsync(issued.Secret.Fingerprint(), TestContext.Current.CancellationToken))?.Id);
        Assert.Equal(
            ErrorCodes.SessionExpired,
            RefusedRestore(await RestoredAsync(
                issued.Secret,
                began.Challenge,
                new FactorPresentation(Factor.Password) { Value = Secret })));
    }

    /// <summary>
    /// AUTH-SESS-005 AC4 (D-224 item 13): a passkey the begin named restores the
    /// session, its assertion naming the session's account in its user handle; one
    /// that names no account there, a passkey added after the begin, and a passkey of
    /// another account are refused <c>auth.factor.rejected</c>, each counted.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005_AC4_APasskeyTheBeginNamedRestoresTheSessionAsync()
    {
        SubjectId subject = await AccountAsync();
        SubjectId other = await AccountAsync();
        var passkey = SoftwareAuthenticator.Holding();
        var theirs = SoftwareAuthenticator.Holding();
        var later = SoftwareAuthenticator.Holding();

        _ = Carries(subject, Factor.Passkey, passkey, "Phone", "internal");
        _ = Carries(other, Factor.Passkey, theirs, "Theirs", "internal");
        _configuration.Set(Settings.AbuseThrottleThreshold, 100);

        OpaqueToken lapsed = Lapsed(subject);
        RestoreChallenge began = Begun(await RestoreBegunAsync(lapsed));

        _clock.Advance(TimeSpan.FromSeconds(1));
        _ = Carries(subject, Factor.Passkey, later, "Later", "internal");

        string challenge = began.WebAuthn.Challenge;
        string relyingParty = began.WebAuthn.RelyingPartyId;

        ErrorCode?[] refused =
        [
            RefusedRestore(await RestoredAsync(lapsed, began.Challenge, new FactorPresentation(Factor.Passkey)
            {
                Assertion = passkey.Asserted(challenge, "https://example.test", relyingParty),
            })),
            RefusedRestore(await RestoredAsync(lapsed, began.Challenge, new FactorPresentation(Factor.Passkey)
            {
                Assertion = later.Asserted(
                    challenge,
                    "https://example.test",
                    relyingParty,
                    userHandle: WebAuthnService.Handle(subject)),
            })),
            RefusedRestore(await RestoredAsync(lapsed, began.Challenge, new FactorPresentation(Factor.Passkey)
            {
                Assertion = theirs.Asserted(
                    challenge,
                    "https://example.test",
                    relyingParty,
                    userHandle: WebAuthnService.Handle(other)),
            })),
        ];

        SignInOutcome restored = Restored(await RestoredAsync(lapsed, began.Challenge, new FactorPresentation(Factor.Passkey)
        {
            Assertion = passkey.Asserted(
                challenge,
                "https://example.test",
                relyingParty,
                userHandle: WebAuthnService.Handle(subject)),
        }));

        Assert.All(refused, code => Assert.Equal(ErrorCodes.FactorRejected, code));
        Assert.Equal(3, _audit.Failed.Count(failed => failed == (subject, Factor.Passkey)));
        Assert.Equal(
            (SignInStatus.Complete, AssuranceLevel.Aal2, true),
            (restored.Progress.Status, restored.Progress.AssuranceLevel, restored.Progress.PhishingResistant));
    }

    /// <summary>
    /// AUTH-SESS-005 AC2a (D-224 item 13): a factor other than the passkey or the
    /// password, or one the begin did not name, is refused
    /// <c>auth.factor.notpermitted</c> before it is verified and counts nothing; a
    /// wrong password is refused and counted as at sign-in; neither restores the
    /// session.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005_AC2a_ARestoreTakesOnlyAFactorTheBeginNamedAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.Totp);

        OpaqueToken lapsed = Lapsed(subject);
        RestoreChallenge began = Begun(await RestoreBegunAsync(lapsed));

        ErrorCode? totp = RefusedRestore(await RestoredAsync(
            lapsed,
            began.Challenge,
            new FactorPresentation(Factor.Totp) { Value = "123456" }));
        ErrorCode? passkey = RefusedRestore(await RestoredAsync(
            lapsed,
            began.Challenge,
            new FactorPresentation(Factor.Passkey)));

        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);

        ErrorCode? wrong = RefusedRestore(await RestoredAsync(
            lapsed,
            began.Challenge,
            new FactorPresentation(Factor.Password) { Value = "notthepassword" }));

        Assert.Equal(
            (ErrorCodes.FactorNotPermitted, ErrorCodes.FactorNotPermitted, ErrorCodes.FactorRejected),
            (totp, passkey, wrong));
        Assert.Equal((subject, Factor.Password), Assert.Single(_audit.Failed));
        Assert.NotEmpty(_throttle.Counted);
        Assert.NotNull(await _live.FindByFingerprintAsync(lapsed.Fingerprint(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-SESS-005 AC4 (D-224 item 13): a challenge of another account's restore, or
    /// one nothing opened, restores nothing and is refused as a wrong factor is.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005_AC4_AChallengeOfAnotherRestoreRestoresNothingAsync()
    {
        SubjectId subject = await AccountAsync();
        SubjectId other = await AccountAsync();
        OpaqueToken lapsed = Lapsed(subject);
        RestoreChallenge theirs = Begun(await RestoreBegunAsync(Lapsed(other)));

        ErrorCode?[] refused =
        [
            RefusedRestore(await RestoredAsync(lapsed, theirs.Challenge, new FactorPresentation(Factor.Password) { Value = Secret })),
            RefusedRestore(await RestoredAsync(lapsed, "a-handle-nothing-opened", new FactorPresentation(Factor.Password) { Value = Secret })),
        ];

        Assert.All(refused, code => Assert.Equal(ErrorCodes.FactorRejected, code));
        Assert.Equal(2, _audit.Failed.Count);
    }

    /// <summary>
    /// AUTH-SESS-005 AC3a and AC4, OPS-BOOT-002 (D-224 item 13): where the session may
    /// not be restored, both steps answer <c>auth.session.expired</c> asking for a full
    /// authentication: no cookie, a cookie naming nothing, a customer's idle expiry, an
    /// expiry past the absolute window, and a break-glass session.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005_AC4_ASessionTheRestoreDoesNotAdmitAsksForAFullAuthenticationAsync()
    {
        SubjectId subject = await AccountAsync();
        OpaqueToken late = Lapsed(subject);
        OpaqueToken breakGlass = Lapsed(subject, breakGlassReason: "Locked out of every factor");
        RestoreChallenge began = Begun(await RestoreBegunAsync(Lapsed(subject)));

        _clock.Advance(TimeSpan.FromHours(23));

        OpaqueToken customer = Lapsed(subject);

        _configuration.Set(Settings.PolicyDefault, Janus.Core.Policies.SystemDefault);

        OpaqueToken?[] refused = [null, OpaqueToken.Draw(_randomness), customer, late, breakGlass];

        foreach (OpaqueToken? secret in refused)
        {
            Assert.Equal("full", Asked(await RestoreBegunAsync(secret)));
            Assert.Equal("full", Asked(await RestoredAsync(
                secret,
                began.Challenge,
                new FactorPresentation(Factor.Password) { Value = Secret })));
        }

        Assert.Empty(_audit.Failed);
    }

    /// <summary>
    /// AUTH-SESS-005 AC4 and AUTH-ABUSE-001 (D-224 item 13): both steps wait out the
    /// delay the account has earned, answered <c>auth.throttled</c>.
    /// </summary>
    [Fact]
    public async Task AUTH_SESS_005_AC4_ARestoreWaitsOutTheDelayAsync()
    {
        SubjectId subject = await AccountAsync();
        OpaqueToken lapsed = Lapsed(subject);
        RestoreChallenge began = Begun(await RestoreBegunAsync(lapsed));

        await FailedAsync(Address, Fresh, Settings.AbuseThrottleThreshold.Default);

        Assert.Equal(
            (ErrorCodes.Throttled, ErrorCodes.Throttled),
            (
                RefusedRestore(await RestoreBegunAsync(lapsed)),
                RefusedRestore(await RestoredAsync(
                    lapsed,
                    began.Challenge,
                    new FactorPresentation(Factor.Password) { Value = Secret }))));
    }

    /// <summary>
    /// REG-IDENT-006 AC6 and CONV-LOG-005 AC1: a sign-in link sent to an address the
    /// account has removed since does not sign in; the press is refused
    /// <c>auth.factor.rejected</c>, recorded against the account and counted.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_006_ALinkSentBeforeTheAddressWasRemovedDoesNotSignInAsync()
    {
        SubjectId subject = await AccountAsync();
        IdentifierId second = _identifiers.Verified(subject, IdentifierKind.Email, Second);

        Enables(Factor.EmailLink);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);
        var browser = OpaqueToken.Draw(_randomness);

        _ = await Service.SendLinkAsync(Second, Language, Source, browser.Value, TestContext.Current.CancellationToken);

        string token = Token();

        await RemovedAsync(subject, second);

        Result<SignInLanding> pressed = await Service.LandAsync(
            browser.Value,
            token,
            Factor.EmailLink,
            press: true,
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.FactorRejected, Code(pressed));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.EmailLink)], _audit.Failed);
        Assert.NotEmpty(_throttle.Counted);
    }

    /// <summary>
    /// REG-IDENT-006 AC6 and CONV-LOG-005 AC1: an email code sent to an address the
    /// account has removed since does not sign in, and is refused and recorded as a
    /// wrong factor is.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_006_AC6_ACodeSentBeforeTheAddressWasRemovedDoesNotSignInAsync()
    {
        SubjectId subject = await AccountAsync();
        IdentifierId second = _identifiers.Verified(subject, IdentifierKind.Email, Second);

        Enables(Factor.EmailCode);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _ = await Service.SendCodeAsync(Second, Language, Source, TestContext.Current.CancellationToken);

        await RemovedAsync(subject, second);

        Assert.Equal(ErrorCodes.FactorRejected, Refused(await PresentAsync(began.Challenge, Factor.EmailCode, Code())));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.EmailCode)], _audit.Failed);
    }

    /// <summary>
    /// REG-IDENT-006 AC6 and CONV-LOG-005 AC1: a sign-in opened with an address the
    /// account has removed since does not sign in, whatever factor is then presented;
    /// the factor is refused <c>auth.factor.rejected</c>, recorded and counted.
    /// </summary>
    [Fact]
    public async Task REG_IDENT_006_AC6_ASignInOpenedWithAnAddressSinceRemovedDoesNotSignInAsync()
    {
        SubjectId subject = await AccountAsync();
        IdentifierId second = _identifiers.Verified(subject, IdentifierKind.Email, Second);

        Remembered(subject);

        SignInChallenge began = await BeganAsync(Second);

        await RemovedAsync(subject, second);

        Assert.Equal(ErrorCodes.FactorRejected, Refused(await PresentAsync(began.Challenge, Factor.Password, Secret)));
        Assert.Equal<(SubjectId?, Factor)>([(subject, Factor.Password)], _audit.Failed);
        Assert.NotEmpty(_throttle.Counted);
    }

    /// <summary>
    /// AUTH-FACT-004 AC6: an email sign-in code is an authentication code, sent as
    /// <c>sign-in-code</c>, living <c>code.signin.lifetime</c> and spent after
    /// <c>code.signin.attempts</c> wrong tries, whatever the verification code's keys hold.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC6_AnEmailSignInCodeIsHeldToItsOwnKeysAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailCode);
        Remembered(subject);
        AuthenticationCodeKeys();

        SignInChallenge began = await BeganAsync(Address);

        Assert.True((await Service.SendCodeAsync(Address, Language, Source, TestContext.Current.CancellationToken))
            .Match(() => true, _ => false));
        Assert.Equal(MessageKind.SignInCode, _notifications.Mail[^1].Message);
        Assert.Equal(
            Noon + TimeSpan.FromMinutes(4),
            (await _pending.FindAsync(subject, Factor.EmailCode, TestContext.Current.CancellationToken))?.ExpiresAt);

        string right = Code();

        Assert.Equal(ErrorCodes.CodeInvalid, Refused(await PresentAsync(began.Challenge, Factor.EmailCode, Other(right))));
        Assert.Equal(ErrorCodes.CodeInvalid, Refused(await PresentAsync(began.Challenge, Factor.EmailCode, Other(right))));
        Assert.Equal(ErrorCodes.CodeExpired, Refused(await PresentAsync(began.Challenge, Factor.EmailCode, right)));

        _ = await Service.SendCodeAsync(Address, Language, Source, TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromMinutes(4));

        Assert.Equal(ErrorCodes.CodeExpired, Refused(await PresentAsync(began.Challenge, Factor.EmailCode, Code())));
    }

    /// <summary>
    /// AUTH-FACT-004 AC6: the code a sign-in link shows elsewhere lives as long as its
    /// link, <c>link.magic.lifetime</c>, and is spent after <c>code.signin.attempts</c>
    /// wrong tries, whatever the verification code's keys hold.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC6_TheCodeASignInLinkShowsIsHeldToTheLinkAndTheSignInCapAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailLink);
        Remembered(subject);
        AuthenticationCodeKeys();
        _configuration.Set(Settings.LinkMagicLifetime, TimeSpan.FromMinutes(20));

        (string challenge, string token, _) = await AskedAsync(subject);

        Assert.Equal(
            Noon + TimeSpan.FromMinutes(20),
            (await _pending.FindAsync(subject, Factor.EmailLink, TestContext.Current.CancellationToken))?.ExpiresAt);

        string shown = Assert.IsType<string>((await LandedAsync(browser: null, token, press: false)).Code);

        Assert.Equal(ErrorCodes.CodeInvalid, Refused(await PresentAsync(challenge, Factor.EmailLink, Other(shown))));
        Assert.Equal(ErrorCodes.CodeInvalid, Refused(await PresentAsync(challenge, Factor.EmailLink, Other(shown))));
        Assert.Equal(ErrorCodes.CodeExpired, Refused(await PresentAsync(challenge, Factor.EmailLink, shown)));
    }

    /// <summary>
    /// AUTH-FACT-004 AC3: a wrong new-device code does not complete the sign-in, and
    /// after <c>code.verification.attempts</c> of them the right one is refused too.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_004_AC3_WrongCodesInvalidateTheHeldSignInAsync()
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
                Source,
                TestContext.Current.CancellationToken);

            Assert.NotNull(Refused(wrong));
        }

        Result<SignInProgress> spent = await Service.VerifyDeviceAsync(
            began.Challenge,
            right,
            Browser,
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

        _work.Reset();

        Result<SignInProgress> stopped = await PresentAsync(began.Challenge, Factor.Password, Secret);

        Assert.Equal(ErrorCodes.PolicyGraceExpired, Refused(stopped));
        Assert.False(_work.Open);
        Assert.Equal((_work.Opened - 1, 1), (_work.Committed, _work.RolledBack));
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

    /// <summary>
    /// AUTH-FACT-002 AC6 and AC9: the text code goes to the number its credential is
    /// bound to, which is the one it was confirmed on, and never to the account's
    /// primary number where that is another.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC6_ATextCodeGoesToTheNumberItsCredentialIsBoundToAndNotThePrimaryAsync()
    {
        const string bound = "+441632960029";

        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        _ = _identifiers.Verified(subject, IdentifierKind.Phone, bound);
        _ = Held(subject, Factor.PhoneCode, number: bound);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        Assert.Equal([Factor.PhoneCode], Reached(await PresentAsync(began.Challenge, Factor.Password, Secret)).Required);
        Assert.True((await AskedAsync(began.Challenge, stepping: null)).Match(offered => offered is null, _ => false));

        OutboundMessage texted = Assert.Single(_notifications.Texts);

        Assert.Equal(Number, (await _identifiers.HeldAsync(subject, TestContext.Current.CancellationToken)).Texted()?.Canonical);
        Assert.Equal(bound, texted.Destination.Canonical);
        Assert.Equal(
            SignInStatus.Complete,
            Reached(await PresentAsync(began.Challenge, Factor.PhoneCode, texted.Values["code"])).Status);
    }

    /// <summary>
    /// AUTH-FACT-002b AC6 and AUTH-FACT-002 AC9: the signal a text code's second step
    /// is considered by is the one of the number its credential is bound to, at the
    /// challenge that offers it and at the ask alike.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_TheSignalOfATextCodeIsAskedOfTheNumberItsCredentialIsBoundToAsync()
    {
        const string bound = "+441632960029";

        SubjectId subject = await AccountAsync();
        var asked = new List<string>();

        Enables(Factor.PhoneCode);
        _ = _identifiers.Verified(subject, IdentifierKind.Phone, bound);
        _ = Held(subject, Factor.PhoneCode, number: bound);
        Remembered(subject);
        _provider = new PhoneSignalProvider((number, _) =>
        {
            asked.Add(number);

            return ValueTask.FromResult(PhoneSignal.Clear);
        });

        SignInChallenge began = await BeganAsync(Address);

        Assert.Equal([Factor.PhoneCode], Reached(await PresentAsync(began.Challenge, Factor.Password, Secret)).Required);
        Assert.True((await AskedAsync(began.Challenge, stepping: null)).Match(offered => offered is null, _ => false));

        Assert.NotEmpty(asked);
        Assert.All(asked, number => Assert.Equal(bound, number));
    }

    /// <summary>
    /// AUTH-FACT-002 AC4 and AC6: the text code asked for at the second step goes to the
    /// account's number as <c>secondstep-code</c> under the purpose <c>secondfactor</c>,
    /// and a password beside it, presented, completes the sign-in at AAL2 and not
    /// phishing-resistant.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC4_APasswordAndASentTextCodeCompleteAtAal2Async()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        Assert.Equal([Factor.PhoneCode], Reached(await PresentAsync(began.Challenge, Factor.Password, Secret)).Required);
        Assert.True((await AskedAsync(began.Challenge, stepping: null)).Match(offered => offered is null, _ => false));

        OutboundMessage texted = Assert.Single(_notifications.Texts);
        SignInProgress reached = Reached(await PresentAsync(began.Challenge, Factor.PhoneCode, texted.Values["code"]));

        Assert.Equal(MessageKind.SecondStepCode, texted.Message);
        Assert.Equal(RestrictionPurpose.SecondFactor, texted.Purpose);
        Assert.Equal(Number, texted.Destination.Canonical);
        Assert.Equal(SignInStatus.Complete, reached.Status);
        Assert.Equal(AssuranceLevel.Aal2, reached.AssuranceLevel);
        Assert.False(reached.PhishingResistant);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC20: the text that carries a second step's code is named as its
    /// send's answer, a wrong code releases nothing, and the code presented right
    /// releases what that send counted once.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC20_ASecondStepCodePresentedRightAnswersItsSendAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);
        _ = await AskedAsync(began.Challenge, stepping: null);

        string right = Assert.Single(_notifications.Texts).Values["code"];
        byte[] named = Assert.Single(_restrictions.Carried).Answer;

        Assert.Equal(ErrorCodes.CodeInvalid, Refused(await PresentAsync(began.Challenge, Factor.PhoneCode, Other(right))));
        Assert.Empty(_restrictions.Answered);

        _ = Reached(await PresentAsync(began.Challenge, Factor.PhoneCode, right));

        Assert.Equal(named, Assert.Single(_restrictions.Answered));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC20, AC14: the message that carries a sign-in code is named as its
    /// send's answer and an ask that sends nothing names none; a wrong code releases
    /// nothing, and the code presented right releases what that send counted once.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC20_ASignInCodePresentedRightAnswersItsSendAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailCode);
        Remembered(subject);
        AuthenticationCodeKeys();

        SignInChallenge began = await BeganAsync(Address);

        _ = await Service.SendCodeAsync(Elsewhere, Language, Source, TestContext.Current.CancellationToken);

        Assert.Empty(_restrictions.Carried);

        _ = await Service.SendCodeAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        string right = Code();
        byte[] named = Assert.Single(_restrictions.Carried).Answer;

        Assert.Equal(ErrorCodes.CodeInvalid, Refused(await PresentAsync(began.Challenge, Factor.EmailCode, Other(right))));
        Assert.Empty(_restrictions.Answered);

        _ = Reached(await PresentAsync(began.Challenge, Factor.EmailCode, right));

        Assert.Equal(named, Assert.Single(_restrictions.Answered));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC20: a sign-in link is named as its send's answer; opened, or
    /// pressed in another browser, it releases nothing, and the press that signs in
    /// releases what that send counted once.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC20_ASignInLinkPressedAnswersItsSendAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailLink);
        Remembered(subject);

        (string challenge, string token, string browser) = await AskedAsync(subject);
        byte[] named = OpaqueToken.Of(token).Fingerprint();

        Assert.Equal(named, Assert.Single(_restrictions.Carried).Answer);

        _ = await LandedAsync(browser, token, press: false);
        _ = await LandedAsync(browser: null, token, press: true);

        Assert.Empty(_restrictions.Answered);

        Assert.NotNull((await LandedAsync(browser, token, press: true)).SignedIn);
        Assert.Equal(named, Assert.Single(_restrictions.Answered));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC20: the message that carries the code of the new-device check is
    /// named as its send's answer, a wrong code releases nothing, and the code presented
    /// right releases what that send counted once.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_004_AC20_ADeviceCodePresentedRightAnswersItsSendAsync()
    {
        _ = await AccountAsync();

        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        string right = Code();
        byte[] named = Assert.Single(_restrictions.Carried).Answer;

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.VerifyDeviceAsync(
                began.Challenge,
                Other(right),
                Browser,
                Source,
                TestContext.Current.CancellationToken)));
        Assert.Empty(_restrictions.Answered);

        _ = Reached(await Service.VerifyDeviceAsync(
            began.Challenge,
            right,
            Browser,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(named, Assert.Single(_restrictions.Answered));
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC20, AUTH-FACT-004: a second step's text code presented right
    /// releases what its send counted once though a refusal follows it, at a sign-in
    /// and at a step-up: where the credential its record names was removed, invalidated
    /// or suspended since, or its number left the account, the release is committed
    /// with the code's spend and the refusal, in one unit of work.
    /// </summary>
    /// <param name="since">What became of the code's credential since it was sent.</param>
    /// <param name="stepUp">Whether the code is presented at a step-up.</param>
    [Theory]
    [InlineData("removed", false)]
    [InlineData("invalidated", false)]
    [InlineData("suspended", false)]
    [InlineData("unbound", false)]
    [InlineData("removed", true)]
    [InlineData("invalidated", true)]
    [InlineData("suspended", true)]
    [InlineData("unbound", true)]
    public async Task AUTH_ABUSE_004_AC20_ARightTextCodeARefusalFollowsReleasesItsSendWithItsSpendAsync(
        string since,
        bool stepUp)
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Remembered(subject);

        SessionId? session = stepUp ? Opened(subject) : null;
        SignInChallenge began = await BeganAsync(Address);

        if (!stepUp)
        {
            _ = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));
        }

        _ = await AskedAsync(began.Challenge, stepUp ? subject : null, session);

        string right = Assert.Single(_notifications.Texts).Values["code"];
        byte[] named = Assert.Single(_restrictions.Carried).Answer;

        switch (since)
        {
            case "suspended":
                Suspends(subject, Factor.PhoneCode);
                break;
            case "unbound":
                await UnboundAsync(subject);
                break;
            default:
                await GoneAsync(subject, Factor.PhoneCode, removed: since == "removed");
                break;
        }

        _work.Reset();

        ErrorCode? refused = Refused(session is SessionId raising
            ? await Service.StepUpAsync(
                AccessContext.Of(subject),
                raising,
                began.Challenge,
                new FactorPresentation(Factor.PhoneCode) { Value = right },
                Source,
                TestContext.Current.CancellationToken)
            : await PresentAsync(began.Challenge, Factor.PhoneCode, right));

        Assert.Equal(since == "suspended" ? ErrorCodes.CredentialSuspended : ErrorCodes.FactorRejected, refused);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal(named, Assert.Single(_restrictions.Answered));
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC20, AUTH-FACT-004: a sign-in code presented right releases what
    /// its send counted once though a refusal follows it, at a sign-in and at a
    /// step-up: where a domain lock refuses the address it was sent to, or the account
    /// has given that address up since, the release is committed with the code's spend
    /// and the refusal, in one unit of work.
    /// </summary>
    /// <param name="since">What refuses the address the code was sent to.</param>
    /// <param name="stepUp">Whether the code is presented at a step-up.</param>
    [Theory]
    [InlineData("locked", false)]
    [InlineData("given up", false)]
    [InlineData("locked", true)]
    [InlineData("given up", true)]
    public async Task AUTH_ABUSE_004_AC20_ARightSignInCodeARefusalFollowsReleasesItsSendWithItsSpendAsync(
        string since,
        bool stepUp)
    {
        SubjectId subject = await AccountAsync();
        IdentifierId second = _identifiers.Verified(subject, IdentifierKind.Email, Second);

        Enables(Factor.EmailCode);
        Remembered(subject);

        SessionId? session = stepUp ? Opened(subject) : null;
        SignInChallenge began = await BeganAsync(Address);

        _ = await Service.SendCodeAsync(Second, Language, Source, TestContext.Current.CancellationToken);

        string right = Code();
        byte[] named = Assert.Single(_restrictions.Carried).Answer;

        if (since == "locked")
        {
            LockedElsewhere(subject);
        }
        else
        {
            await RemovedAsync(subject, second);
        }

        _work.Reset();

        ErrorCode? refused = Refused(session is SessionId raising
            ? await Service.StepUpAsync(
                AccessContext.Of(subject),
                raising,
                began.Challenge,
                new FactorPresentation(Factor.EmailCode) { Value = right },
                Source,
                TestContext.Current.CancellationToken)
            : await PresentAsync(began.Challenge, Factor.EmailCode, right));

        Assert.Equal(
            since == "locked" ? ErrorCodes.IdentifierDomainNotAllowed : ErrorCodes.FactorRejected,
            refused);
        Assert.Null(await _pending.FindAsync(subject, Factor.EmailCode, TestContext.Current.CancellationToken));
        Assert.Equal(named, Assert.Single(_restrictions.Answered));
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC20: a sign-in link releases what its send counted once only
    /// where its press signs in. A press a domain lock refuses, and one that lands on
    /// no sign-in of its account, release nothing.
    /// </summary>
    /// <param name="locked">
    /// Whether a domain lock refuses the press; otherwise it lands on no sign-in, the
    /// one it was issued on having completed by another factor.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AUTH_ABUSE_004_AC20_ASignInLinkWhosePressIsRefusedReleasesNothingAsync(bool locked)
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailLink);
        Remembered(subject);

        (string challenge, string token, string browser) = await AskedAsync(subject);

        if (locked)
        {
            LockedElsewhere(subject);
        }
        else
        {
            Assert.Equal(SignInStatus.Complete, Reached(await PresentAsync(challenge, Factor.Password, Secret)).Status);
        }

        ErrorCode? refused = Code(await Service.LandAsync(
            browser,
            token,
            Factor.EmailLink,
            press: true,
            Browser,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(locked ? ErrorCodes.IdentifierDomainNotAllowed : ErrorCodes.FactorRejected, refused);
        Assert.Single(_restrictions.Carried);
        Assert.Empty(_restrictions.Answered);
        Assert.NotNull(await _pending.FindAsync(subject, Factor.EmailLink, TestContext.Current.CancellationToken));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-002 AC4: a text code accepted as a second step is one issued for that
    /// sign-in; the code issued for another is refused as no code of this one.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC4_ATextCodeIssuedForAnotherSignInIsRefusedAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Remembered(subject);

        SignInChallenge first = await BeganAsync(Address);
        SignInChallenge second = await BeganAsync(Address);

        _ = await PresentAsync(first.Challenge, Factor.Password, Secret);
        _ = await PresentAsync(second.Challenge, Factor.Password, Secret);
        _ = await AskedAsync(first.Challenge, stepping: null);

        Assert.Equal(
            ErrorCodes.CodeExpired,
            Refused(await PresentAsync(second.Challenge, Factor.PhoneCode, _notifications.Texts[^1].Values["code"])));
    }

    /// <summary>
    /// AUTH-FACT-002 AC6: a text code goes out only for a sign-in a first factor has been
    /// accepted for; an ask on a sign-in only opened, or on a handle that opens nothing,
    /// is answered as every ask is and sends nothing.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC6_AnAskBeforeAFirstFactorSendsNothingAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.PhoneCode);

        SignInChallenge began = await BeganAsync(Address);

        Assert.True((await AskedAsync(began.Challenge, stepping: null)).Match(offered => offered is null, _ => false));
        Assert.True((await AskedAsync("a-handle-nothing-opened", stepping: null)).Match(offered => offered is null, _ => false));
        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-002 AC7, AUTH-FACT-002b AC6: where the carrier reports a recent change
    /// of SIM or of network when the text code is asked for at a sign-in, no code is
    /// issued and nothing goes to the number, nothing is counted, the consideration is
    /// recorded, and a sign-in left with no factor is refused <c>auth.factor.rejected</c>.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC7_AReportedChangeAtTheAskLeavingNoFactorIsRefusedAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        Answers(PhoneSignal.Risk);

        Assert.Equal(
            ErrorCodes.FactorRejected,
            (await AskedAsync(began.Challenge, stepping: null)).Match(_ => (ErrorCode?)null, error => error.Code));
        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([(Factor.PhoneCode, (PhoneSignal?)PhoneSignal.Risk, (SubjectId?)subject)], _considered.Records);
        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// AUTH-FACT-002 AC11: a text code asked for at a sign-in whose credential is taken
    /// out of use, or whose number leaves the account, between the read of the
    /// credential and the issue of its code sends nothing, issues nothing and counts
    /// nothing, and records the consideration as the signal answered. It is answered
    /// <c>auth.credential.suspended</c> where the credential is now suspended, and
    /// otherwise as an ask whose number answers <c>risk</c> is: here, with no factor
    /// left, <c>auth.factor.rejected</c>.
    /// </summary>
    /// <param name="left">What another request committed while the ask waited for the row.</param>
    /// <param name="answer">What the ask is refused with.</param>
    [Theory]
    [InlineData("suspended", "auth.credential.suspended")]
    [InlineData("removed", "auth.factor.rejected")]
    [InlineData("number", "auth.factor.rejected")]
    public async Task AUTH_FACT_002_AC11_AnAskWhoseCredentialOrNumberLeftBeforeItsCodeIsIssuedSendsNothingAndRecordsTheSignalAsync(
        string left,
        string answer)
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Authenticator bound = Held(subject, Factor.PhoneCode);
        Remembered(subject);
        Answers(PhoneSignal.Clear);

        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        await LeavesUnderTheLockAsync(subject, bound, left);
        _work.Reset();

        Assert.Equal(
            answer,
            (await AskedAsync(began.Challenge, stepping: null)).Match(_ => string.Empty, error => error.Code.ToString()));
        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([(Factor.PhoneCode, (PhoneSignal?)PhoneSignal.Clear, (SubjectId?)subject)], _considered.Records);
        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
        Assert.Equal((1, 0), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-002 AC11: where another factor is left, an ask whose credential was
    /// removed before its code was issued is answered with the factors the challenge
    /// still offers, as a <c>risk</c> ask is; and where no signal is declared the
    /// consideration is recorded as <c>unavailable</c>.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC11_AnAskWhoseCredentialWasRemovedIsAnsweredWithTheFactorsLeftAsARiskAskIsAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Authenticator bound = Held(subject, Factor.PhoneCode);
        Holds(subject, Factor.Totp);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        await LeavesUnderTheLockAsync(subject, bound, "removed");

        SignInProgress? offered = (await AskedAsync(began.Challenge, stepping: null))
            .Match(progress => progress, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        Assert.Equal(SignInStatus.FactorRequired, offered?.Status);
        Assert.Equal([Factor.Totp], offered?.Required);
        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([(Factor.PhoneCode, (PhoneSignal?)null, (SubjectId?)subject)], _considered.Records);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// AUTH-FACT-002 AC11, chapter 09 <c>POST /auth/step-up</c>: at a step-up, an ask
    /// whose credential is suspended before its code is issued is answered
    /// <c>auth.credential.suspended</c>, and one whose credential was removed is
    /// answered as a <c>risk</c> ask at a step-up is, with the factors of the
    /// combinations left; neither sends, issues or counts anything, and each records the
    /// consideration as the signal answered.
    /// </summary>
    /// <param name="left">What another request committed while the ask waited for the row.</param>
    [Theory]
    [InlineData("suspended")]
    [InlineData("removed")]
    public async Task AUTH_FACT_002_AC11_AnAskAtAStepUpWhoseCredentialLeftBeforeItsCodeIsIssuedSendsNothingAsync(string left)
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Authenticator bound = Held(subject, Factor.PhoneCode);
        Holds(subject, Factor.Totp);
        Answers(PhoneSignal.Clear);

        SignInChallenge began = await BeganAsync(Address);
        SessionId session = Opened(subject);

        await LeavesUnderTheLockAsync(subject, bound, left);

        Result<SignInProgress?> asked = await AskedAsync(began.Challenge, subject, session);

        if (left == "suspended")
        {
            Assert.Equal(ErrorCodes.CredentialSuspended, asked.Match(_ => (ErrorCode?)null, error => error.Code));
        }
        else
        {
            SignInProgress? offered = asked.Match(
                progress => progress,
                error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

            Assert.Equal(SignInStatus.FactorRequired, offered?.Status);
            Assert.Equal([Factor.Password, Factor.Totp], offered?.Required);
        }

        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([(Factor.PhoneCode, (PhoneSignal?)PhoneSignal.Clear, (SubjectId?)subject)], _considered.Records);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// AUTH-FACT-002 AC11, CONV-DESIGN-003: the unit of work that issues a text code
    /// holds the account's row first, as every writer that takes a number from the
    /// account does, then locks the row of the identifier holding the credential's
    /// number, before it reads the credential under its lock, and issues the code only
    /// after all three.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC11_TheAskLocksTheIdentifiersRowBeforeTheCredentialAndIssuesTheCodeAfterBothAsync()
    {
        SubjectId subject = await AccountAsync();
        var order = new List<string>();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        IdentifierId number = (await _identifiers.HeldAsync(subject, TestContext.Current.CancellationToken)).All
            .First(identifier => identifier.Kind is IdentifierKind.Phone).Id;

        _accounts.Holding = held => order.Add(held == subject ? "account" : "another account");
        _identifiers.LockingRow = async (_, row) =>
        {
            order.Add(row == number ? "identifier" : "another identifier");
            order.Add(
                await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken) is null
                    ? "no code yet"
                    : "code");
        };
        _authenticators.Locking = _ => order.Add("credential");

        Assert.True((await AskedAsync(began.Challenge, stepping: null)).Match(offered => offered is null, _ => false));
        Assert.Equal(["account", "identifier", "no code yet", "credential"], order);
        Assert.NotNull(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Single(_notifications.Texts);
    }

    /// <summary>
    /// AUTH-FACT-002 AC7, AUTH-FACT-002b AC6: a text code asked for at a sign-in whose
    /// number answers <c>risk</c> is answered with the factors the challenge still
    /// offers, the text code no longer among them, and sends nothing.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC7_AReportedChangeAtTheAskIsAnsweredWithTheFactorsLeftAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Holds(subject, Factor.Totp);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        Answers(PhoneSignal.Risk);

        SignInProgress? offered = (await AskedAsync(began.Challenge, stepping: null))
            .Match(progress => progress, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(SignInStatus.FactorRequired, offered?.Status);
        Assert.Equal([Factor.Totp], offered?.Required);
        Assert.Equal(AssuranceLevel.Aal1, offered?.AssuranceLevel);
        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([(Factor.PhoneCode, (PhoneSignal?)PhoneSignal.Risk, (SubjectId?)subject)], _considered.Records);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// AUTH-FACT-002 AC7, chapter 09 <c>POST /auth/step-up</c>: a text code asked for at
    /// a step-up whose number answers <c>risk</c>, after a password was accepted on the
    /// challenge, is answered with the factors of the combinations left, the password
    /// already accepted not among them.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC7_AReportedChangeAtAStepUpLeavesOutAFactorAlreadyAcceptedAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Holds(subject, Factor.Totp);
        Answers(PhoneSignal.Risk);

        SessionId held = Opened(subject);
        SignInChallenge began = await BeganAsync(Address);
        SignInProgress presented = Reached(await Service.StepUpAsync(
            AccessContext.Of(subject),
            held,
            began.Challenge,
            new FactorPresentation(Factor.Password) { Value = Secret },
            Source,
            TestContext.Current.CancellationToken));

        SignInProgress? offered = (await AskedAsync(began.Challenge, subject, held)).Match(
            progress => progress,
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        Assert.Equal(SignInStatus.FactorRequired, presented.Status);
        Assert.Equal(SignInStatus.FactorRequired, offered?.Status);
        Assert.Equal([Factor.Totp], offered?.Required);
        Assert.Empty(_notifications.Texts);
    }

    /// <summary>
    /// AUTH-FACT-002 AC7, AUTH-FACT-002b AC6: at a step-up the text code is withheld
    /// where the carrier reports a recent change for the number: no code is issued or
    /// sent, the consideration is recorded once, and the ask is answered as a sign-in's
    /// is, with the factors of the combinations left without the text code and what
    /// the factors accepted on the challenge reach, which is no level where none was.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_AReportedChangeWithholdsTheTextCodeFromAStepUpAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Holds(subject, Factor.Totp);
        Answers(PhoneSignal.Risk);

        SignInChallenge began = await BeganAsync(Address);

        SignInProgress? offered = (await AskedAsync(began.Challenge, subject, Opened(subject))).Match(
            progress => progress,
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        Assert.Equal(SignInStatus.FactorRequired, offered?.Status);
        Assert.Equal([Factor.Password, Factor.Totp], offered?.Required);
        Assert.Equal((AssuranceLevel.Delegated, false), (offered?.AssuranceLevel, offered?.PhishingResistant));
        Assert.Null(offered?.Session);
        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([(Factor.PhoneCode, (PhoneSignal?)PhoneSignal.Risk, (SubjectId?)subject)], _considered.Records);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// AUTH-FACT-002 AC7, `09` `POST /auth/step-up`: a step-up ask whose number answers
    /// <c>risk</c>, made on a session that already meets the strictest of the policy's
    /// gates as AUTH-STEP-002 step 1 judges it, is answered with nothing required,
    /// reporting what the factors accepted on the challenge reach, none having been:
    /// nothing is issued or sent, and the consideration is recorded once.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC7_AReportedChangeAtAStepUpTheSessionAlreadyMeetsRequiresNothingAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Holds(subject, Factor.Totp);
        Answers(PhoneSignal.Risk);

        SignInChallenge began = await BeganAsync(Address);

        SignInProgress? met = (await AskedAsync(began.Challenge, subject, Opened(subject, AssuranceLevel.Aal2))).Match(
            progress => progress,
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

        Assert.Equal(SignInStatus.Complete, met?.Status);
        Assert.Empty(Assert.IsType<SignInProgress>(met).Required);
        Assert.Equal((AssuranceLevel.Delegated, false), (met?.AssuranceLevel, met?.PhishingResistant));
        Assert.Null(met?.Session);
        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([(Factor.PhoneCode, (PhoneSignal?)PhoneSignal.Risk, (SubjectId?)subject)], _considered.Records);
    }

    /// <summary>
    /// AUTH-FACT-002 AC7: a step-up left with no combination once the text code is
    /// withheld is told to report the loss, the account holding the level and being
    /// unable to present it, with the values of the weakest of the policy's gates
    /// (AUTH-STEP-002 step 3, D-197).
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC7_AReportedChangeAtAStepUpLeavingNoCombinationAsksForALossReportAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Answers(PhoneSignal.Risk);

        SignInChallenge began = await BeganAsync(Address);

        Error refused = Refusal(await AskedAsync(began.Challenge, subject, Opened(subject)));

        Assert.Equal(ErrorCodes.StepUpRequired, refused.Code);
        Assert.Equal("report-loss", refused.Details["outcome"].GetString());

        Gate weakest = await WeakestAsync(subject);

        Assert.Equal(
            ("aal2", weakest.PhishingResistant, (long)weakest.MaximumAge.TotalSeconds),
            (
                refused.Details["required"].GetProperty("level").GetString(),
                refused.Details["required"].GetProperty("phishingResistant").GetBoolean(),
                refused.Details["required"].GetProperty("maxAge").GetInt64()));
        Assert.Equal(JsonValueKind.Null, refused.Details["pendingUntil"].ValueKind);
        Assert.Empty(refused.Details["options"].Deserialize<string[][]>()!);
        Assert.Empty(_notifications.Texts);
        Assert.Equal([(Factor.PhoneCode, (PhoneSignal?)PhoneSignal.Risk, (SubjectId?)subject)], _considered.Records);
    }

    /// <summary>
    /// AUTH-FACT-002 AC7, AUTH-STEP-002: a step-up left with no combination by an account
    /// that never reached what the weakest of the policy's gates asks is offered
    /// enrolment (D-197).
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC7_AReportedChangeAtAStepUpThatNeverReachedTheGateOffersEnrolmentAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Answers(PhoneSignal.Risk);
        Declares(
            new Gate(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5)),
            new Gate(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(15)),
            Factor.PhoneCode);

        SignInChallenge began = await BeganAsync(Address);

        Error refused = Refusal(await AskedAsync(began.Challenge, subject, Opened(subject)));

        Assert.Equal(ErrorCodes.StepUpRequired, refused.Code);
        Assert.Equal("enrol", refused.Details["outcome"].GetString());
        Assert.Equal(900, refused.Details["required"].GetProperty("maxAge").GetInt64());
        Assert.True(refused.Details["required"].GetProperty("phishingResistant").GetBoolean());
        Assert.Empty(refused.Details["options"].Deserialize<string[][]>()!);
    }

    /// <summary>
    /// AUTH-FACT-002 AC7, AUTH-STEP-002: a step-up left with no combination once the text
    /// code is withheld, on an account with a loss report in flight, is refused
    /// <c>auth.stepup.required</c> with the outcome <c>pending</c> and the instant the
    /// report completes. The ask presents no factor, so nothing is counted and no refused
    /// step-up factor is recorded.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC7_AReportedChangeAtAStepUpWithALossReportInFlightIsAnsweredPendingAsync()
    {
        SubjectId subject = await AccountAsync();
        DateTimeOffset completes = _clock.GetUtcNow() + TimeSpan.FromDays(7);

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        _authenticators.Hold(Authenticator.Existing(
            AuthenticatorId.New(_clock),
            subject,
            Factor.Totp,
            Label(Factor.Totp),
            AuthenticatorState.Suspended,
            _clock.GetUtcNow(),
            null,
            completes,
            confirmed: true,
            new TotpMaterial(new byte[20], null),
            null));
        Answers(PhoneSignal.Risk);

        SignInChallenge began = await BeganAsync(Address);

        Error refused = Refusal(await AskedAsync(began.Challenge, subject, Opened(subject)));

        Assert.Equal(ErrorCodes.StepUpRequired, refused.Code);
        Assert.Equal("pending", refused.Details["outcome"].GetString());
        Assert.Equal(completes, refused.Details["pendingUntil"].GetDateTimeOffset());
        Assert.Equal("aal2", refused.Details["required"].GetProperty("level").GetString());
        Assert.Empty(refused.Details["options"].Deserialize<string[][]>()!);
        Assert.Empty(_notifications.Texts);
        Assert.Equal([(Factor.PhoneCode, (PhoneSignal?)PhoneSignal.Risk, (SubjectId?)subject)], _considered.Records);
        Assert.Empty(_audit.StepUpsFailed);
        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// `09` `POST /auth/step-up`: a step-up ask whose number answers <c>risk</c>, made
    /// with no session of the asking account, judges no gate and is refused with no
    /// details.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002_AC7_AReportedChangeAtAStepUpWithNoSessionOfTheAccountJudgesNoGateAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Answers(PhoneSignal.Risk);

        SignInChallenge began = await BeganAsync(Address);

        Error refused = Refusal(await AskedAsync(began.Challenge, subject, Opened(new SubjectId(Guid.NewGuid()))));

        Assert.Equal(ErrorCodes.StepUpRequired, refused.Code);
        Assert.Empty(refused.Details);
        Assert.Empty(_notifications.Texts);
    }

    /// <summary>
    /// AUTH-FACT-002 AC8: a text code asked for at a sign-in on whose challenge a
    /// generated code was presented and refused, no first factor having been accepted
    /// (AUTH-FACT-002b AC7), is an ask before a first
    /// factor. It asks no signal, issues no code, sends nothing and is answered as
    /// every ask is, the same whatever the account holds: a number whose signal is
    /// clear, one whose signal answers <c>risk</c>, a suspended one, or none.
    /// </summary>
    /// <param name="holds">Whether the account holds a number for the text code.</param>
    /// <param name="suspended">Whether that number is suspended.</param>
    /// <param name="answered">What the number's signal would answer.</param>
    [Theory]
    [InlineData(true, false, PhoneSignal.Clear)]
    [InlineData(true, false, PhoneSignal.Risk)]
    [InlineData(true, true, PhoneSignal.Clear)]
    [InlineData(false, false, PhoneSignal.Clear)]
    public async Task AUTH_FACT_002_AC8_AnAskAfterASecondStepRefusedBeforeAnyFirstFactorAsksNoSignalAndSendsNothingAsync(
        bool holds,
        bool suspended,
        PhoneSignal answered)
    {
        SubjectId subject = await AccountAsync();
        int signalled = 0;

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.Totp);
        Remembered(subject);

        if (holds)
        {
            Holds(subject, Factor.PhoneCode);
        }

        if (suspended)
        {
            Suspends(subject, Factor.PhoneCode);
        }

        _provider = new PhoneSignalProvider((_, _) =>
        {
            signalled++;

            return ValueTask.FromResult(answered);
        });

        SignInChallenge began = await BeganAsync(Address);
        string generated = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);

        ErrorCode? presented = Refused(await PresentAsync(began.Challenge, Factor.Totp, generated));
        int counted = _throttle.Counted.Count;

        Result<SignInProgress?> asked = await AskedAsync(began.Challenge, stepping: null);

        Assert.Equal(ErrorCodes.FactorNotPermitted, presented);
        Assert.True(asked.Match(offered => offered is null, _ => false));
        Assert.Equal(0, signalled);
        Assert.Empty(_considered.Records);
        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Empty(_codes.All);
        Assert.Empty(_audit.Failed);
        Assert.Equal(counted, _throttle.Counted.Count);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-002b AC6: a carrier reporting a recent change of SIM or of network
    /// withholds the text code from that sign-in, and the account's other second
    /// steps are offered in its place.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_AReportedChangeWithholdsTheTextCodeAndOffersTheRestAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.PhoneCode);
        Holds(subject, Factor.Totp);
        Answers(PhoneSignal.Risk);

        SignInProgress reached = await SignedInAsync(subject, Factor.Password, Secret);

        Assert.Equal(SignInStatus.FactorRequired, reached.Status);
        Assert.Equal([Factor.Totp], reached.Required);
    }

    /// <summary>
    /// AUTH-FACT-002b AC6: an answer that reports nothing leaves the text code on
    /// offer, so what the signal changes is the one case it speaks to.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_AnAnswerThatReportsNoChangeLeavesTheTextCodeOnOfferAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.PhoneCode);
        Holds(subject, Factor.Totp);
        Answers(PhoneSignal.Clear);

        SignInProgress reached = await SignedInAsync(subject, Factor.Password, Secret);

        Assert.Equal([Factor.PhoneCode, Factor.Totp], reached.Required);
    }

    /// <summary>
    /// AUTH-FACT-002b AC6: withholding the only second step leaves nothing to offer,
    /// and a sign-in with nothing left to present is refused rather than completing
    /// below the level the account's own second step asked for.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_AReportedChangeRefusesASignInWhoseOnlySecondStepIsTheTextCodeAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.PhoneCode);
        Answers(PhoneSignal.Risk);

        SignInChallenge began = await BeganAsync(Address);

        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refused(await PresentAsync(began.Challenge, Factor.Password, Secret)));
    }

    /// <summary>
    /// AUTH-FACT-002b AC6: what the signal refused is recorded with the entry it was
    /// asked about and without the number, which is the trail an operator reads.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_AWithholdingIsRecordedWithTheEntryAndNotTheNumberAsync()
    {
        SubjectId subject = await AccountAsync();

        Holds(subject, Factor.PhoneCode);
        Holds(subject, Factor.Totp);
        Answers(PhoneSignal.Risk);

        _ = await SignedInAsync(subject, Factor.Password, Secret);

        (Factor Factor, PhoneSignal? Signal, SubjectId? Subject) recorded =
            Assert.Single(_considered.Records);

        Assert.Equal(Factor.PhoneCode, recorded.Factor);
        Assert.Equal(PhoneSignal.Risk, recorded.Signal);
        Assert.Equal(subject, recorded.Subject);
    }

    /// <summary>
    /// AUTH-FACT-002b AC6, AUTH-FACT-003: a sign-in link by text is the whole of the
    /// sign-in, so no link goes to a number the carrier reports a change for; the ask
    /// is answered as every ask is, counted against the restrictions as the link would
    /// have been, and the consideration is recorded.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_AReportedChangeSendsNoSignInLinkByTextAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneLink);
        Answers(PhoneSignal.Risk);

        Result<RequestedLink> asked = await Service.SendLinkAsync(
            Number,
            Language,
            Source,
            browser: null,
            TestContext.Current.CancellationToken);

        Assert.True(asked.Match(_ => true, _ => false));
        Assert.Empty(_notifications.Texts);
        Assert.Equal(MessageKind.SignInLink, Assert.Single(_restrictions.Drawn).Message);
        Assert.Equal([(Factor.PhoneLink, (PhoneSignal?)PhoneSignal.Risk, (SubjectId?)subject)], _considered.Records);
    }

    /// <summary>
    /// AUTH-ABUSE-003 AC1: the question is asked of the number and never of the
    /// account, so a number no account holds is answered in the same bytes and nothing
    /// about existence is told either way.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_003_AC1_ANumberNoAccountHoldsIsAnsweredInTheSameBytesAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneLink);
        Answers(PhoneSignal.Risk);

        Result<RequestedLink> held = await Service.SendLinkAsync(
            Number,
            Language,
            Source,
            browser: null,
            TestContext.Current.CancellationToken);

        Result<RequestedLink> nobodys = await Service.SendLinkAsync(
            "+441632960099",
            Language,
            Source,
            browser: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            held.Match(answered => (ErrorCode?)null, error => error.Code),
            nobodys.Match(answered => (ErrorCode?)null, error => error.Code));
        Assert.Equal(
            held.Match(answered => answered.Challenge is null, _ => false),
            nobodys.Match(answered => answered.Challenge is null, _ => false));
        Assert.Empty(_notifications.Texts);
        Assert.NotEqual(default, subject);
    }

    // AUTH-FACT-002 AC6: the text code asked for, in the language of the ask, at a
    // sign-in or under the session a step-up raises.
    private ValueTask<Result<SignInProgress?>> AskedAsync(
        string challenge,
        SubjectId? stepping,
        SessionId? session = null) =>
        Service.AskAsync(
            challenge,
            Factor.PhoneCode,
            stepping,
            session,
            Source,
            Language,
            TestContext.Current.CancellationToken);

    private static Error Refusal(Result<SignInProgress?> outcome) =>
        outcome.Match(
            _ => throw new Xunit.Sdk.XunitException("The ask was not refused."),
            error => error);

    // AUTH-SESS-001: every instant a session record keeps of what it reached.
    private static DateTimeOffset?[] Instants(Session session) =>
    [
        session.DelegatedAt,
        session.Aal1At,
        session.Aal2At,
        session.Aal3At,
        session.PhishingResistantAt,
    ];

    // AUTH-STEP-002 (D-197): what a step-up that names no action offers against and
    // answers a refusal with, which is the weakest of the gates of the policy in force,
    // field by field.
    private async ValueTask<Gate> WeakestAsync(SubjectId subject) =>
        (await Policies.ForAsync(subject, TestContext.Current.CancellationToken)).Match(
            policy => policy.Gates.Values.Aggregate(PolicyStrictness.Weakest),
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    // A session of the account that proved one factor now, which is what a step-up
    // raises, or one that proved more.
    private SessionId Opened(SubjectId subject, AssuranceLevel attained = AssuranceLevel.Aal1)
    {
        var session = Session.Begin(
            SessionId.New(_clock),
            subject,
            new Assurance(attained, PhishingResistant: false),
            new SessionOrigin(Source, Browser),
            _clock.GetUtcNow(),
            TimeSpan.FromDays(1),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        byte[] secret = new byte[32];
        byte[] rotation = new byte[32];

        _randomness.GetBytes(secret);
        _randomness.GetBytes(rotation);
        _live.AddAsync(session, secret, rotation, TestContext.Current.CancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return session.Id;
    }

    // AUTH-STEP-002: the system policy with one action's gate declared, the others as
    // the policy ships them.
    private void Declares(Gate declared) =>
        _configuration.Set(
            Settings.PolicyDefault,
            Janus.Core.Policies.SystemDefault with
            {
                Gates = Janus.Core.Policies.SystemDefault.Gates.ToDictionary(
                    gate => gate.Key,
                    gate => gate.Key is StepUpAction.AccountSuspend ? declared : gate.Value),
            });

    // AUTH-STEP-002: the system policy with one action's gate declared and every other
    // action's declared too, so that the strictest and the weakest of its gates, field
    // by field, are the test's own, and with the entries named permitted beside the
    // policy's.
    private void Declares(Gate declared, Gate others, params Factor[] permitted) =>
        _configuration.Set(
            Settings.PolicyDefault,
            Janus.Core.Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>(
                    [.. Janus.Core.Policies.SystemDefault.LoginFactors, .. permitted]),
                Gates = Janus.Core.Policies.SystemDefault.Gates.ToDictionary(
                    gate => gate.Key,
                    gate => gate.Key is StepUpAction.AccountSuspend ? declared : others),
            });

    // AUTH-STEP-002: what a step-up that names no action asks of the session and
    // offers the account.
    private async ValueTask<StepUpChallenge> OfferedAsync(SubjectId subject, SessionId session) =>
        (await Guard.ChallengeUnnamedAsync(subject, session, TestContext.Current.CancellationToken)).Match(
            challenge => challenge,
            error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    // The combinations a challenge offers, each written as its factors in order.
    private static string[] Named(StepUpChallenge challenge) =>
        [.. challenge.Combinations.Select(combination => Named([.. combination]))];

    private static string Named(params Factor[] combination) => string.Join('+', combination);

    // REG-DOM-001: the account is placed under a lock that admits no address it holds.
    private void LockedElsewhere(SubjectId subject)
    {
        _memberships.Place(subject, Locked);
        _configuration.Set(
            Settings.OrganizationPolicy,
            Locked.ToString(),
            PolicyOverride.None with { EmailDomains = ["elsewhere.test"] });
    }

    // AUTH-FACT-002b: the deployment's own provider, standing for the carrier.
    private void Answers(PhoneSignal signal) =>
        _provider = new PhoneSignalProvider((_, _) => ValueTask.FromResult(signal));

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
            actor: null,
            TestContext.Current.CancellationToken);

        await _work.CommitAsync(TestContext.Current.CancellationToken);

        return subject;
    }

    // The authentication codes' keys set apart from the verification code's, so a code
    // held to the wrong ones shows (AUTH-FACT-004 AC6).
    private void AuthenticationCodeKeys()
    {
        _configuration.Set(Settings.CodeVerificationLifetime, TimeSpan.FromMinutes(30));
        _configuration.Set(Settings.CodeVerificationAttempts, 10);
        _configuration.Set(Settings.CodeSigninLifetime, TimeSpan.FromMinutes(4));
        _configuration.Set(Settings.CodeSigninAttempts, 2);
    }

    // The account has been seen on this browser, so the new-device check is not what
    // the sign-in under test is answering (AUTH-FACT-016).
    // REG-IDENT-006: the account gives the address up, as a removal does.
    private ValueTask RemovedAsync(SubjectId subject, IdentifierId identifier) =>
        _identifiers.GiveUpAsync(
            subject,
            identifier,
            _clock.GetUtcNow(),
            _clock.GetUtcNow() + TimeSpan.FromDays(1),
            [1],
            TestContext.Current.CancellationToken);

    // AUTH-FACT-016 AC8: a sign-in the primary email's factor exempted remembers the
    // browser once, as a completed check does, and announces it once.
    private async Task RememberedOnceAsync(SubjectId subject)
    {
        IReadOnlyList<Device> standing = await _devices.StandingOfAsync(
            subject,
            _clock.GetUtcNow(),
            TestContext.Current.CancellationToken);

        Assert.Equal(DeviceKind.Remembered, Assert.Single(standing).Kind);
        _ = Assert.Single(_events.Published.OfType<DeviceVerified>());
    }

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
        _ = Held(subject, factor, isPreferred);

    // AUTH-FACT-002: a text code's credential is bound to a number, which is the
    // account's own unless the test names another.
    private Authenticator Held(SubjectId subject, Factor factor, bool isPreferred = false, string number = Number)
    {
        var held = Authenticator.Existing(
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
                ? new WebAuthnMaterial(new byte[] { 1 }, new byte[] { 2 }, -7, "example.test", 0, false, false, [])
                : null,
            isPreferred,
            Authenticator.IsBoundToNumber(factor) ? number : null);

        _authenticators.Hold(held);

        return held;
    }

    // AUTH-RECOV-007: the account's credentials of one factor as a loss report, or a
    // removal that would lower reachable assurance, leaves them: suspended, with the
    // instant the window ends.
    private void Suspends(SubjectId subject, Factor factor)
    {
        foreach (Authenticator held in _authenticators.All
                     .Where(credential => credential.Subject == subject && credential.Factor == factor))
        {
            held.Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        }
    }

    // AUTH-RECOV-007, IDN-LIFE-012a: an identity at a social provider linked to the
    // account and suspended since, by a window (a loss report or a removal) or held by
    // the provider's security event.
    private async ValueTask<Authenticator> LinkedAsync(SubjectId subject, bool held)
    {
        var linked = Authenticator.Linked(
            AuthenticatorId.New(_clock),
            subject,
            Factor.Google,
            Label(Factor.Google),
            _clock.GetUtcNow());

        await _authenticators.LinkAsync(linked, "linked-at-the-provider", TestContext.Current.CancellationToken);

        if (held)
        {
            linked.Hold();
        }
        else
        {
            linked.Suspend(_clock.GetUtcNow() + TimeSpan.FromDays(7));
        }

        return linked;
    }

    // The account's credentials of one factor as a removal that completes at once
    // leaves them, gone, or as the end of a window does, invalidated.
    private async ValueTask GoneAsync(SubjectId subject, Factor factor, bool removed)
    {
        foreach (Authenticator held in _authenticators.All
                     .Where(credential => credential.Subject == subject && credential.Factor == factor)
                     .ToArray())
        {
            await GoneAsync(held, removed);
        }
    }

    // AUTH-FACT-002 AC11: what another request commits between an ask's read of the
    // credential and its lock on the identifier's row, which the ask then finds under
    // that lock: the number removed and the credential suspended for it, the credential
    // removed with its number, or the number gone from the account alone.
    private async ValueTask LeavesUnderTheLockAsync(SubjectId subject, Authenticator bound, string left)
    {
        IdentifierId number = (await _identifiers.HeldAsync(subject, TestContext.Current.CancellationToken)).All
            .First(identifier => identifier.Kind is IdentifierKind.Phone).Id;

        _identifiers.LockingRow = async (_, _) =>
        {
            _identifiers.LockingRow = null;

            await _identifiers.DiscardAsync(subject, number, TestContext.Current.CancellationToken);

            if (left == "suspended")
            {
                bound.Suspend(_clock.GetUtcNow() + Settings.RecoveryInvalidationWindow.Default);
                bound.Unbind();
            }
            else if (left == "removed")
            {
                await _authenticators.RemoveAsync(bound.Id, TestContext.Current.CancellationToken);
            }
        };
    }

    // The account's text code credential as its number leaving the account leaves it
    // where the account would reach less without it: suspended for the window and
    // bound to no number (AUTH-FACT-002).
    private async ValueTask UnboundAsync(SubjectId subject)
    {
        foreach (Authenticator held in _authenticators.All
                     .Where(credential => credential.Subject == subject && credential.Factor == Factor.PhoneCode)
                     .ToArray())
        {
            held.Suspend(_clock.GetUtcNow() + Settings.RecoveryInvalidationWindow.Default);
            held.Unbind();

            await _authenticators.RecordAsync(held, TestContext.Current.CancellationToken);
        }
    }

    // One credential as a removal that completes at once leaves it, gone, or as the end
    // of a window does, invalidated.
    private async ValueTask GoneAsync(Authenticator held, bool removed)
    {
        if (removed)
        {
            await _authenticators.RemoveAsync(held.Id, TestContext.Current.CancellationToken);
        }
        else
        {
            held.Invalidate();
        }
    }

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

    private ValueTask<SignInChallenge> BeganAsync(string identifier) => BeganAsync(identifier, browser: null);

    // A sign-in opened from a browser carrying a pre-authentication session, or none.
    private async ValueTask<SignInChallenge> BeganAsync(string identifier, string? browser) =>
        (await Service.BeginAsync(
            identifier,
            Source,
            remembered: null,
            trusted: null,
            browser,
            TestContext.Current.CancellationToken))
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
            Source,
            TestContext.Current.CancellationToken);

    // CONV-DESIGN-003 AC8: an operation called inside a unit of work the test opened
    // faults and ends it, so the test's own commit commits nothing and faults too.
    private async ValueTask InsideAsync(Func<Task> operation)
    {
        _work.Reset();
        _ = await _work.BeginAsync(TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(operation);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _work.CommitAsync(TestContext.Current.CancellationToken));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
    }

    private async ValueTask<SignInProgress> SignedInAsync(
        SubjectId subject,
        Factor factor,
        string value)
    {
        SignInChallenge began = await BeganAsync(Address);

        Assert.NotEqual(default, subject);

        return Reached(await PresentAsync(began.Challenge, factor, value));
    }

    // A password and the second step, asking that the browser be trusted.
    private async ValueTask<SignInOutcome> TrustedAsync()
    {
        SignInChallenge began = await BeganAsync(Address);

        _ = Reached(await PresentAsync(began.Challenge, Factor.Password, Secret));

        string code = new Totp(new byte[20], TotpCodes.StepSeconds, OtpHashMode.Sha1, TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);

        SignInOutcome outcome = (await Service.PresentAsync(
                began.Challenge,
                new FactorPresentation(Factor.Totp) { Value = code, TrustDevice = true },
                new SessionOrigin(Source, Browser),
                remembered: null,
                trusted: null,
                TestContext.Current.CancellationToken))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        _clock.Advance(TimeSpan.FromSeconds(TotpCodes.StepSeconds));

        return outcome;
    }

    // A sign-in opened from a browser and a link asked for from it, which the link is
    // issued on (D-226).
    private async ValueTask<(string Challenge, string Token, string Browser)> AskedAsync(
        SubjectId subject)
    {
        var browser = OpaqueToken.Draw(_randomness);
        SignInChallenge began = await BeganAsync(Address, browser.Value);

        Result<RequestedLink> asked = await Service.SendLinkAsync(
            Address,
            Language,
            Source,
            browser.Value,
            TestContext.Current.CancellationToken);

        Assert.True(asked.Match(_ => true, _ => false));
        Assert.NotNull(await _pending.FindAsync(subject, Factor.EmailLink, TestContext.Current.CancellationToken));

        return (began.Challenge, Token(), browser.Value);
    }

    // A WebAuthn credential of the account, a passkey or a second-factor security key,
    // as the enrolment of the authenticator that holds it left it: its identifier, its
    // key and the transports the browser reported.
    private Authenticator Carries(
        SubjectId subject,
        Factor factor,
        SoftwareAuthenticator authenticator,
        string named,
        params string[] transports)
    {
        var held = Authenticator.Existing(
            AuthenticatorId.New(_clock),
            subject,
            factor,
            CredentialLabel.TryParse(named, out CredentialLabel label)
                ? label
                : throw new InvalidOperationException("The label does not read."),
            AuthenticatorState.Active,
            _clock.GetUtcNow(),
            null,
            null,
            confirmed: true,
            null,
            new WebAuthnMaterial(
                authenticator.CredentialId,
                authenticator.PublicKey,
                SoftwareAuthenticator.Es256,
                "example.test",
                null,
                false,
                false,
                transports),
            isPreferred: false);

        _authenticators.Hold(held);

        return held;
    }

    // A WebAuthn credential of the account that keeps a counter and may be synced, not
    // yet backed up, as its enrolment left it.
    private Authenticator Counting(SubjectId subject, Factor factor, SoftwareAuthenticator authenticator, uint counter)
    {
        var held = Authenticator.Existing(
            AuthenticatorId.New(_clock),
            subject,
            factor,
            Label(factor),
            AuthenticatorState.Active,
            _clock.GetUtcNow(),
            null,
            null,
            confirmed: true,
            null,
            new WebAuthnMaterial(
                authenticator.CredentialId,
                authenticator.PublicKey,
                SoftwareAuthenticator.Es256,
                "example.test",
                counter,
                true,
                false,
                []),
            isPreferred: false);

        _authenticators.Hold(held);

        return held;
    }

    // What the authenticator answers the sign-in's ceremony with, user present and
    // verified, reporting the given counter and that its credential is now backed up.
    private static AuthenticatorAssertion Synced(SoftwareAuthenticator authenticator, string challenge, uint counter) =>
        authenticator.Asserted(
            challenge,
            "https://example.test",
            "example.test",
            SoftwareAuthenticator.UserPresent
                | SoftwareAuthenticator.UserVerified
                | SoftwareAuthenticator.BackupEligible
                | SoftwareAuthenticator.BackedUp,
            counter);

    // The credentials a request names, each as its identifier and its transports.
    private static (string Id, string Transports)[] Described(WebAuthnRequestOptions options) =>
        [.. options.AllowCredentials.Select(named => (named.Id, string.Join(',', named.Transports)))];

    // A WebAuthn credential of the account, a passkey unless another kind is named,
    // whose public key is the given one, as its enrolment left it, under the relying
    // party and the algorithm it was enrolled with.
    private AuthenticatorId Keyed(
        SubjectId subject,
        ECDsa key,
        uint counter,
        string relyingParty = "example.test",
        int algorithm = -7,
        Factor? factor = null)
    {
        var id = AuthenticatorId.New(_clock);

        _authenticators.Hold(Authenticator.Existing(
            id,
            subject,
            factor ?? Factor.Passkey,
            Label(factor ?? Factor.Passkey),
            AuthenticatorState.Active,
            _clock.GetUtcNow(),
            null,
            null,
            confirmed: true,
            null,
            new WebAuthnMaterial(
                CredentialId,
                SoftwareAuthenticator.Holding(key, CredentialId).PublicKey,
                algorithm,
                relyingParty,
                counter,
                false,
                false,
                []),
            isPreferred: false));

        return id;
    }

    // What an authenticator holding the key answers the sign-in's ceremony with, user
    // present and verified unless the flags say otherwise, reporting the given counter
    // for the relying party it holds the key for.
    private static AuthenticatorAssertion Asserted(
        ECDsa key,
        string challenge,
        uint counter,
        string relyingParty = "example.test",
        byte flags = 0x05)
        => SoftwareAuthenticator.Holding(key, CredentialId)
            .Asserted(challenge, "https://example.test", relyingParty, flags, counter);

    // The account's outstanding email sign-in code, as the ask left it.
    private async ValueTask<PendingSignIn> CodeSentAsync(SubjectId subject)
    {
        Enables(Factor.EmailCode);

        Result asked = await Service.SendCodeAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        Assert.True(asked.Match(() => true, _ => false));

        return Assert.IsType<PendingSignIn>(
            await _pending.FindAsync(subject, Factor.EmailCode, TestContext.Current.CancellationToken));
    }

    private async ValueTask<SignInLanding> PressedAsync(SubjectId subject)
    {
        (string challenge, string token, string browser) = await AskedAsync(subject);

        return await LandedAsync(browser, token, press: true);
    }

    private async ValueTask<SignInLanding> LandedAsync(
        string? browser,
        string token,
        bool press) =>
        (await Service.LandAsync(
                browser,
                token,
                Factor.EmailLink,
                press,
                Browser,
                Source,
                TestContext.Current.CancellationToken))
        .Match(landing => landing, error => throw new InvalidOperationException(error.Code.ToString()));

    // The message body is the code and the link token in that order, so the test reads
    // what the person reads rather than what the store holds.
    private string Code() => _notifications.Mail[^1].Values["code"];

    private string Token() => _notifications.Mail[^1].Token();

    // A code of the same shape that is not the one sent.
    private static string Other(string code) =>
        string.Equals(code, "000000", StringComparison.Ordinal) ? "111111" : "000000";

    private static SignInProgress Reached(Result<SignInProgress> outcome) =>
        outcome.Match(
            progress => progress,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private static ErrorCode? Refused(Result<SignInProgress> outcome) =>
        outcome.Match(_ => (ErrorCode?)null, error => error.Code);

    // A session of the account under a policy requiring AAL2, opened now and lapsed for
    // inactivity inside its absolute window, as the secret its cookie carries.
    private OpaqueToken Lapsed(SubjectId subject, string? breakGlassReason = null)
    {
        _configuration.Set(
            Settings.PolicyDefault,
            Janus.Core.Policies.SystemDefault with { RequiredAssurance = AssuranceLevel.Aal2 });

        var session = Session.Begin(
            SessionId.New(_clock),
            subject,
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            new SessionOrigin(Source, Browser),
            _clock.GetUtcNow() - TimeSpan.FromHours(2),
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(24),
            breakGlassReason);
        var secret = OpaqueToken.Draw(_randomness);

        _live.AddAsync(
                session,
                secret.Fingerprint(),
                OpaqueToken.Draw(_randomness).Fingerprint(),
                TestContext.Current.CancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        return secret;
    }

    private ValueTask<Result<RestoreChallenge>> RestoreBegunAsync(OpaqueToken? secret) =>
        Service.BeginRestoreAsync(secret, browser: null, Source, TestContext.Current.CancellationToken);

    private ValueTask<Result<SignInOutcome>> RestoredAsync(
        OpaqueToken? secret,
        string challenge,
        FactorPresentation presented) =>
        Service.RestoreAsync(
            secret,
            challenge,
            presented,
            new SessionOrigin(Source, Browser),
            TestContext.Current.CancellationToken);

    private static RestoreChallenge Begun(Result<RestoreChallenge> outcome) =>
        outcome.Match(
            challenge => challenge,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private static SignInOutcome Restored(Result<SignInOutcome> outcome) =>
        outcome.Match(
            restored => restored,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private static ErrorCode? RefusedRestore<TValue>(Result<TValue> outcome) =>
        outcome.Match(_ => (ErrorCode?)null, error => error.Code);

    // What a refusal asks to be done again, or nothing where it names nothing.
    private static string? Asked<TValue>(Result<TValue> outcome) =>
        outcome.Match(
            _ => null,
            error => error.Code == ErrorCodes.SessionExpired
                && error.Details.TryGetValue("reauthenticate", out JsonElement asked)
                    ? asked.GetString()
                    : error.Code.ToString());

    private static ErrorCode? Code(Result outcome) =>
        outcome.Match(() => (ErrorCode?)null, error => error.Code);

    private ValueTask<Result> ResentAsync(string challenge, string? browser) =>
        Service.ResendDeviceCodeAsync(challenge, browser, Source, TestContext.Current.CancellationToken);

    private ValueTask<Result<SignInProgress>> VerifiedAsync(string challenge, string code) =>
        Service.VerifyDeviceAsync(challenge, code, Browser, Source, TestContext.Current.CancellationToken);

    private static ErrorCode? Code(Result<SignInLanding> outcome) =>
        outcome.Match(_ => (ErrorCode?)null, error => error.Code);

    private static Error? Delayed(Result<SignInChallenge> began) =>
        began.Match(_ => (Error?)null, error => error);

    // What the caller of a refused request reads: the code and every detail.
    private static string Shape(Error? refused) =>
        refused is null
            ? string.Empty
            : refused.Code + JsonSerializer.Serialize(refused.Details);

    // Each time the identifier opens a sign-in from the source and a wrong password
    // is presented against it.
    private async ValueTask FailedAsync(string identifier, string source, int times)
    {
        for (int attempt = 0; attempt < times; attempt++)
        {
            SignInChallenge began = (await Service.BeginAsync(
                identifier,
                source,
                remembered: null,
                trusted: null,
                browser: null,
                TestContext.Current.CancellationToken))
                .Match(challenge => challenge, error => throw new InvalidOperationException(error.Code.ToString()));

            Assert.Equal(
                ErrorCodes.FactorRejected,
                Refused(await Service.PresentAsync(
                    began.Challenge,
                    new FactorPresentation(Factor.Password) { Value = "wrong" + Secret },
                    Browser,
                    source,
                    TestContext.Current.CancellationToken)));
        }
    }

    // A browser the account has passed the new-device check on, or trusted, and the
    // token it carries for it.
    private async ValueTask<string> KnownAsync(SubjectId subject, bool trusted)
    {
        Result<OpaqueToken> known = trusted
            ? await Devices.TrustAsync(subject, Browser, TestContext.Current.CancellationToken)
            : await Devices.RememberAsync(subject, Browser, TestContext.Current.CancellationToken);

        await _work.CommitAsync(TestContext.Current.CancellationToken);

        return known.Match(
            token => token.Value,
            error => throw new InvalidOperationException(error.Code.ToString()));
    }

    private ValueTask<Result<SignInChallenge>> BeganFromAsync(string? remembered, string? trusted) =>
        Service.BeginAsync(Address, Fresh, remembered, trusted, browser: null, TestContext.Current.CancellationToken);

    private ValueTask<Result<SignInOutcome>> PresentedFromAsync(string challenge, string remembered) =>
        Service.PresentAsync(
            challenge,
            new FactorPresentation(Factor.Password) { Value = Secret },
            new SessionOrigin(Fresh, Browser),
            remembered,
            trusted: null,
            TestContext.Current.CancellationToken);

    private async ValueTask<bool> AskedLinkAsync(string identifier) =>
        (await Service.SendLinkAsync(
            identifier,
            Language,
            Source,
            browser: null,
            TestContext.Current.CancellationToken))
        .Match(_ => true, _ => false);

    // A link asked for from a browser: the sign-in the request opened, or nothing where
    // the link went on one the browser holds.
    private async ValueTask<string?> LinkedAsync(string identifier, string? browser) =>
        (await Service.SendLinkAsync(
            identifier,
            Language,
            Source,
            browser,
            TestContext.Current.CancellationToken))
        .Match(asked => asked.Challenge, error => throw new InvalidOperationException(error.Code.ToString()));

    // AUTH-FACT-003 AC6: what a link asked for with an identifier is answered with, from
    // a browser holding no sign-in and from one that began a sign-in with it.
    private async ValueTask<(string? Opened, string? Held)> AnsweredAsync(string identifier)
    {
        string? opened = await LinkedAsync(identifier, OpaqueToken.Draw(_randomness).Value);
        var holding = OpaqueToken.Draw(_randomness);

        _ = await BeganAsync(identifier, holding.Value);

        return (opened, await LinkedAsync(identifier, holding.Value));
    }
}
