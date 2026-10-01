using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly NotificationHandlerInMemory _notifications = new();
    private readonly SendingRestrictionsInMemory _restrictions = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly PhoneSignalAuditInMemory _considered = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private PhoneSignalProvider? _provider;

    /// <summary>
    /// A deployment that has named the keys with no default, the languages it writes
    /// in among them, and holds a template for every message a sign-in sends, in the
    /// shape the message goes out in.
    /// </summary>
    public AuthenticationServiceTests()
    {
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);
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
            Codes,
            _configuration,
            _work,
            _clock,
            _randomness);

    private PhoneSignals Signals => new(_provider, _considered, _work, _clock);

    private VerificationCodes Codes =>
        new(_codes, _configuration, _work, _clock, _randomness);

    private SignInLinks Links =>
        new(
            _pending,
            _identifiers,
            _accounts,
            Policies,
            Lock,
            _notifications,
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
    /// IDN-ACCT-007 (D-166): trusting a browser writes to the account, so a restricted
    /// account's sign-in is not offered it and records none where it asks, while an
    /// active account's same sign-in is offered it.
    /// </summary>
    [Fact]
    public async Task IDN_ACCT_007_ARestrictedAccountsSignInTrustsNoDeviceAsync()
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
            TestContext.Current.CancellationToken);

        _work.RefusesCommit = Error.From(ErrorCodes.SystemFault);

        Result<SignInChallenge> uncommitted = await Service.BeginAsync(
            Address,
            Fresh,
            remembered: null,
            trusted: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.SystemFault, unopened.Match(_ => (ErrorCode?)null, error => error.Code));
        Assert.Equal(ErrorCodes.SystemFault, uncommitted.Match(_ => (ErrorCode?)null, error => error.Code));
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC1, AUTH-ABUSE-003: failures against an identifier no account
    /// holds are counted against the identifier as failures against one an account
    /// holds are, so from a source that has failed nothing both are held alike and a
    /// third identifier is not held at all.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AC1_AnIdentifierNoAccountHoldsIsHeldFromAFreshSourceAsOneAnAccountHoldsAsync()
    {
        await AccountAsync();

        await FailedAsync(Elsewhere, "198.51.100.21", times: 3);
        await FailedAsync(Address, "198.51.100.22", times: 3);

        Result<SignInChallenge> nobodys = await Service.BeginAsync(
            Elsewhere,
            Fresh,
            remembered: null,
            trusted: null,
            TestContext.Current.CancellationToken);
        Result<SignInChallenge> held = await Service.BeginAsync(
            Address,
            Fresh,
            remembered: null,
            trusted: null,
            TestContext.Current.CancellationToken);
        Result<SignInChallenge> other = await Service.BeginAsync(
            "somebody@example.test",
            Fresh,
            remembered: null,
            trusted: null,
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

        _ = await Service.BeginAsync(Address, Fresh, remembered, trusted, TestContext.Current.CancellationToken);

        int held = _devices.LookedUp - before;

        _ = await Service.BeginAsync(Elsewhere, Fresh, remembered, trusted, TestContext.Current.CancellationToken);

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

        SendRequest told = Assert.Single(_notifications.Sent);

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

        Result held = await Service.SendLinkAsync(Address, Language, Source, browser: null, TestContext.Current.CancellationToken);
        Result nobodys = await Service.SendLinkAsync(Elsewhere, Language, Source, browser: null, TestContext.Current.CancellationToken);

        Assert.Same(refusal, held.Match(() => (Error?)null, error => error));
        Assert.Same(refusal, nobodys.Match(() => (Error?)null, error => error));
    }

    /// <summary>
    /// CONV-LOG-005 AC1: a link pressed in the browser that asked for it and landing
    /// on no sign-in of its account is a refused link against that account, and a
    /// pressed token that resolves to nothing is one against no account; an open that
    /// is not a press and a press in another browser are no attempt and are not
    /// recorded.
    /// </summary>
    [Fact]
    public async Task CONV_LOG_005_AC1_ALinkThatDoesNotLandIsRecordedAgainstItsAccountAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.EmailLink);
        Remembered(subject);

        (string challenge, string token, string browser) = await AskedAsync(subject);

        _ = await LandedAsync(challenge, browser, token, press: false);
        _ = await LandedAsync(challenge, browser: null, token, press: true);

        Assert.Empty(_audit.Failed);

        Result<SignInLanding> unknown = await Service.LandAsync(
            challenge,
            browser,
            "a-token-nothing-issued",
            Factor.EmailLink,
            press: true,
            Browser,
            Source,
            TestContext.Current.CancellationToken);

        Result<SignInLanding> astray = await Service.LandAsync(
            "a-handle-nothing-opened",
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
                "a-handle-nothing-opened",
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
                    "a-handle-nothing-opened",
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
            began.Challenge,
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

        string shown = Assert.IsType<string>((await LandedAsync(challenge, browser: null, token, press: false)).Code);

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
        Assert.True((await AskedAsync(began.Challenge, stepping: null)).Match(() => true, _ => false));

        SendRequest texted = Assert.Single(_notifications.Texts);
        SignInProgress reached = Reached(await PresentAsync(began.Challenge, Factor.PhoneCode, texted.Values["code"]));

        Assert.Equal(MessageKind.SecondStepCode, texted.Message);
        Assert.Equal(RestrictionPurpose.SecondFactor, texted.Purpose);
        Assert.Equal(Number, texted.Destination.Canonical);
        Assert.Equal(SignInStatus.Complete, reached.Status);
        Assert.Equal(AssuranceLevel.Aal2, reached.AssuranceLevel);
        Assert.False(reached.PhishingResistant);
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

        Assert.True((await AskedAsync(began.Challenge, stepping: null)).Match(() => true, _ => false));
        Assert.True((await AskedAsync("a-handle-nothing-opened", stepping: null)).Match(() => true, _ => false));
        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-002b AC6: where the carrier reports a recent change of SIM or of
    /// network when the text code is asked for, no code is issued and nothing goes to
    /// the number; the ask is answered as every ask is, nothing is counted, and the
    /// consideration is recorded.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_AReportedChangeAtTheAskSendsNoTextCodeAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Remembered(subject);

        SignInChallenge began = await BeganAsync(Address);

        _ = await PresentAsync(began.Challenge, Factor.Password, Secret);

        Answers(PhoneSignal.Risk);

        Assert.True((await AskedAsync(began.Challenge, stepping: null)).Match(() => true, _ => false));
        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([(Factor.PhoneCode, (PhoneSignal?)PhoneSignal.Risk, (SubjectId?)subject)], _considered.Records);
        Assert.Empty(_audit.Failed);
        Assert.Empty(_throttle.Counted);
    }

    /// <summary>
    /// AUTH-FACT-002b AC6: at a step-up the text code is withheld where the carrier
    /// reports a recent change for the number: the ask is answered as every ask is, and
    /// no code is issued or sent.
    /// </summary>
    [Fact]
    public async Task AUTH_FACT_002b_AC6_AReportedChangeWithholdsTheTextCodeFromAStepUpAsync()
    {
        SubjectId subject = await AccountAsync();

        Enables(Factor.PhoneCode);
        Holds(subject, Factor.PhoneCode);
        Answers(PhoneSignal.Risk);

        SignInChallenge began = await BeganAsync(Address);

        Assert.True((await AskedAsync(began.Challenge, subject)).Match(() => true, _ => false));
        Assert.Empty(_notifications.Texts);
        Assert.Null(await _pending.FindAsync(subject, Factor.PhoneCode, TestContext.Current.CancellationToken));
        Assert.Equal([(Factor.PhoneCode, (PhoneSignal?)PhoneSignal.Risk, (SubjectId?)subject)], _considered.Records);
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

        Result asked = await Service.SendLinkAsync(
            Number,
            Language,
            Source,
            browser: null,
            TestContext.Current.CancellationToken);

        Assert.True(asked.Match(() => true, _ => false));
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

        Result held = await Service.SendLinkAsync(
            Number,
            Language,
            Source,
            browser: null,
            TestContext.Current.CancellationToken);

        Result nobodys = await Service.SendLinkAsync(
            "+441632960099",
            Language,
            Source,
            browser: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            held.Match(() => (ErrorCode?)null, error => error.Code),
            nobodys.Match(() => (ErrorCode?)null, error => error.Code));
        Assert.Empty(_notifications.Texts);
        Assert.NotEqual(default, subject);
    }

    // AUTH-FACT-002 AC6: the text code asked for, in the language of the ask.
    private ValueTask<Result> AskedAsync(string challenge, SubjectId? stepping) =>
        Service.AskAsync(challenge, Factor.PhoneCode, stepping, Source, Language, TestContext.Current.CancellationToken);

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
        (await Service.BeginAsync(
            identifier,
            Source,
            remembered: null,
            trusted: null,
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
        Service.BeginAsync(Address, Fresh, remembered, trusted, TestContext.Current.CancellationToken);

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
        .Match(() => true, _ => false);
}
