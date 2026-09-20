using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Credentials;
using Janus.Authentication.Factors;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Recovery;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Recovery;
using Janus.Authentication.Tests.Sending;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using OtpNet;
using Xunit;

namespace Janus.Authentication.Tests.Credentials;

/// <summary>
/// What an account does to the credentials it holds: the gate each operation asks
/// for, the notice every enrolment sends, the codes a second step brings with it, and
/// the window a removal runs instead of going at once (AUTH-STEP-007, AUTH-RECOV-001,
/// AUTH-RECOV-006, AUTH-RECOV-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class CredentialServiceTests : IAsyncDisposable
{
    private const string Language = "en";
    private const string Source = "198.51.100.7";
    private const string Address = "person@example.test";
    private const string Number = "+441632960011";
    private const string Secret = "orangemarmaladeandtoast";
    private const string Another = "quincejellyonasaucer";
    private const string Origin = "https://app.example.com";
    private const string RelyingParty = "example.com";

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly OrganizationId Staff = new(Guid.NewGuid());

    private static readonly SessionOrigin Somewhere = new(
        Source,
        new DeviceDescription("Firefox", "Fedora"),
        new SessionLocation("Alexandria", "EG"));

    private readonly KeyCeremonyStoreInMemory _ceremonies = new();
    private readonly RecoveryLinkStoreInMemory _links = new();
    private readonly RecoveryApprovalStoreInMemory _approvals = new();
    private readonly LossReportStoreInMemory _reports = new();
    private readonly RecoveryAuditInMemory _recorded = new();
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly AccountDirectoryInMemory _accounts = new(PreferenceDeclarations.None);
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly LeakedPasswordCorpusInMemory _corpus = new();
    private readonly WordListInMemory _words = new();
    private readonly ScreeningLogInMemory _screening = new();
    private readonly RecoveryCodeStoreInMemory _sets = new();
    private readonly CredentialAuditInMemory _credentials = new();
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
    /// A deployment that can send the notice every enrolment carries, over origins the
    /// relying party identifier sits above.
    /// </summary>
    public CredentialServiceTests()
    {
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);
        _configuration.Set(Settings.ServiceName, "Example");
        _configuration.Set(
            Settings.WebAuthnOrigins,
            (IReadOnlyList<string>)[Origin, "https://id.example.com"]);

        MessageKind[] messages = [MessageKind.SecurityNotice, MessageKind.CredentialEnrolled];

        foreach (MessageKind message in messages)
        {
            foreach (SendKind kind in Enum.GetValues<SendKind>())
            {
                _templates.Set(
                    message,
                    kind,
                    Language,
                    new MessageTemplate(kind is SendKind.Email ? "subject" : null, "body"));
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-STEP-007 AC1: enrolling a credential is told to every recorded channel,
    /// which is the control that stands whatever the gate cost.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_007_AC1_AnEnrolmentReachesEveryRecordedChannelAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        _mail.Taken.Clear();
        _sms.Taken.Clear();

        _ = await ConfirmedAsync(subject, session);

        Assert.NotEmpty(_mail.Taken);
        Assert.NotEmpty(_sms.Taken);
    }

    /// <summary>
    /// AUTH-STEP-007 AC2: a password-only customer enrols a passkey on the strength of
    /// the password, because the gate is the lower of what the account reaches and what
    /// the credential contributes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_007_AC2_APasswordOnlyCustomerEnrolsAPasskeyAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        CredentialCeremony ceremony = Value(await Service.BeginKeyAsync(
            Authority(subject, session),
            Factor.Passkey,
            TestContext.Current.CancellationToken));

        EnrolledCredential enrolled = Value(await Service.CompleteKeyAsync(
            Authority(subject, session),
            Attestation(ceremony.Challenge, synced: true),
            "This phone",
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            AuthenticatorState.Active,
            (await _authenticators.FindAsync(
                enrolled.Credential,
                TestContext.Current.CancellationToken))!.State);
    }

    /// <summary>
    /// AUTH-STEP-007 AC2: an account that reaches AAL2 has to present AAL2, so the
    /// session that presented the password alone is sent to step up.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_007_AC2_AnAccountReachingAal2MustPresentAal2Async()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        _ = await ConfirmedAsync(subject, session);

        Assert.Equal(
            ErrorCodes.StepUpRequired,
            Refused(await Service.BeginKeyAsync(
                Authority(subject, session),
                Factor.Passkey,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-RECOV-001 AC1: a synced credential survives the device it was made on, so
    /// nothing is asked for beside it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_001_AC1_ASyncedCredentialPromptsForNoSecondOneAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        EnrolledCredential enrolled = await KeyAsync(subject, session, synced: true);

        Assert.Null(enrolled.SecondCredential);
    }

    /// <summary>
    /// AUTH-RECOV-001 AC2: a device-bound credential prompts, and a public user may
    /// decline the prompt.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_001_AC2_ADeviceBoundCredentialPromptsAndMayBeDeclinedAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        EnrolledCredential enrolled = await KeyAsync(subject, session, synced: false);

        Assert.Equal(CredentialRedundancy.Advisory, enrolled.SecondCredential);
    }

    /// <summary>
    /// AUTH-RECOV-001 AC3: under a policy that enforces redundancy the same credential
    /// prompts and the prompt cannot be declined; the prompt reads the flag and never
    /// the authenticator's make.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_001_AC3_AnEnforcedPolicyHoldsTheMemberToASecondOneAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        _memberships.Place(subject, Staff);
        _configuration.Set(
            Settings.OrganizationPolicy,
            Staff.ToString(),
            new PolicyOverride(
                null,
                null,
                null,
                CredentialRedundancy.Enforced,
                null,
                null));

        EnrolledCredential enrolled = await KeyAsync(subject, session, synced: false);

        Assert.Equal(CredentialRedundancy.Enforced, enrolled.SecondCredential);
    }

    /// <summary>
    /// AUTH-RECOV-006 AC1: a second step enrolled beside a password brings a set of
    /// recovery codes with it, generated whether or not the person asked.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_006_AC1_ASecondStepBesideAPasswordBringsRecoveryCodesAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        EnrolledCredential confirmed = await ConfirmedAsync(subject, session);

        Assert.NotNull(confirmed.RecoveryCodes);
        Assert.Equal(
            Settings.FactorRecoveryCodesCount.Default,
            confirmed.RecoveryCodes.Count);
        Assert.NotNull(await _sets.FindAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-RECOV-006 AC3 and AUTH-FACT-009: regenerating replaces the set, and no code
    /// of the previous one validates afterwards.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_006_AC3_RegeneratingReplacesTheWholeSetAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        EnrolledCredential confirmed = await ConfirmedAsync(subject, session);
        string spent = confirmed.RecoveryCodes![0];

        await PresentedAsync(subject, session);

        GeneratedRecoveryCodes generated = Value(await Service.GenerateRecoveryCodesAsync(
            Authority(subject, session),
            TestContext.Current.CancellationToken));

        Assert.Equal(_clock.GetUtcNow(), generated.GeneratedAt);
        Assert.DoesNotContain(spent, generated.Codes);

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Codes.SpendAsync(subject, spent, TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-RECOV-006 AC4: a passkey-only account has no second step for the codes to
    /// stand in for, so it holds no set and is offered none.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_006_AC4_APasskeyOnlyAccountIsOfferedNoCodesAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync(password: false);

        _ = await KeyAsync(subject, session, synced: true);

        Assert.Equal(
            ErrorCodes.FactorNotPermitted,
            Refused(await Service.GenerateRecoveryCodesAsync(
                Authority(subject, session),
                TestContext.Current.CancellationToken)));

        Assert.Null(await _sets.FindAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-002b: a second step is second to a password, so an account holding
    /// none is refused one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_ASecondStepIsRefusedWithoutAPasswordAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync(password: false);

        Assert.Equal(
            ErrorCodes.FactorNotPermitted,
            Refused(await Service.BeginGeneratorAsync(
                Authority(subject, session),
                "Phone",
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-014: the value the authenticator signs over is the one the server
    /// issued, so a completion with no ceremony standing reaches nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_014_ACompletionWithoutACeremonyIsRefusedAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refused(await Service.CompleteKeyAsync(
                Authority(subject, session),
                Attestation("a-challenge-nobody-issued", synced: true),
                "This phone",
                Source,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-001: a label outside the length the chapter admits is refused, and no
    /// credential is left behind by the refusal.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_001_ALabelOutsideTheLimitEnrolsNothingAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        Assert.Equal(
            ErrorCodes.CredentialLabelInvalid,
            Refused(await Service.BeginGeneratorAsync(
                Authority(subject, session),
                new string('a', 65),
                TestContext.Current.CancellationToken)));

        Assert.Empty(await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-RECOV-007 and D-092: removing the credential the account's assurance rests
    /// on runs the notified window rather than taking it away at once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_RemovingTheLastSecondStepRunsTheWindowAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        EnrolledCredential confirmed = await ConfirmedAsync(subject, session);

        await PresentedAsync(subject, session);

        LossReported? reported = Reported(await Service.RemoveAsync(
            Authority(subject, session),
            confirmed.Credential,
            Source,
            TestContext.Current.CancellationToken));

        Assert.NotNull(reported);
        Assert.Equal(_clock.GetUtcNow() + TimeSpan.FromDays(7), reported.InvalidatesAt);
        Assert.Equal(
            AuthenticatorState.Suspended,
            (await _authenticators.FindAsync(
                confirmed.Credential,
                TestContext.Current.CancellationToken))!.State);
    }

    /// <summary>
    /// AUTH-RECOV-007 and D-092: removing one of several credentials leaves what the
    /// account reaches where it was, so it goes at once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_RemovingOneOfSeveralCredentialsCompletesAtOnceAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        EnrolledCredential confirmed = await ConfirmedAsync(subject, session);

        await PresentedAsync(subject, session);

        EnrolledCredential key = await KeyAsync(subject, session, synced: true);

        await PresentedAsync(subject, session);

        Assert.Null(Reported(await Service.RemoveAsync(
            Authority(subject, session),
            confirmed.Credential,
            Source,
            TestContext.Current.CancellationToken)));

        Assert.Null(await _authenticators.FindAsync(
            confirmed.Credential,
            TestContext.Current.CancellationToken));

        Assert.NotNull(await _authenticators.FindAsync(
            key.Credential,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-RECOV-002 and D-148: an enrolment session sets the password with no gate at
    /// all, and completing the enrolment ends it, so the person signs in with what they
    /// set.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_TheEnrolmentSessionSetsAPasswordAndThenEndsAsync()
    {
        SubjectId subject = await AccountAsync(password: false);

        EnrolmentSession opened = await OpenedAsync(subject);

        Assert.True(Succeeded(await Service.SetPasswordAsync(
            CredentialAuthority.Of(opened.Id),
            Another,
            Source,
            TestContext.Current.CancellationToken)));

        Assert.NotNull(await _passwords.FindAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.EnrolmentTokenInvalid,
            Refused(await Service.BeginGeneratorAsync(
                CredentialAuthority.Of(opened.Id),
                "Phone",
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// D-148: the enrolment session reaches the account it was opened for and nothing
    /// else, so one that has lapsed reaches nothing at all.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_ALapsedEnrolmentSessionReachesNothingAsync()
    {
        SubjectId subject = await AccountAsync(password: false);

        EnrolmentSession opened = await OpenedAsync(subject);

        _clock.Advance(TimeSpan.FromHours(2));

        Assert.Equal(
            ErrorCodes.EnrolmentTokenInvalid,
            Refused(await Service.SetPasswordAsync(
                CredentialAuthority.Of(opened.Id),
                Another,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Null(await _passwords.FindAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-002b AC3: the upgrade replaces a second-factor security key and
    /// refuses anything else, so a passkey has nothing to be upgraded from.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC3_OnlyASecurityKeyIsUpgradedAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        EnrolledCredential key = await KeyAsync(subject, session, synced: true);

        await PresentedAsync(subject, session);

        Assert.Equal(
            ErrorCodes.CredentialNotUpgradable,
            Refused(await Service.UpgradeKeyAsync(
                Authority(subject, session),
                key.Credential,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-FACT-002b AC3: the upgrade completes a passkey registration on the same
    /// hardware; the passkey is listed and the entry it came from is retired.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC3_AnUpgradedSecurityKeyIsListedAndTheEntryRetiresAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        EnrolledCredential key = await SecurityKeyAsync(subject, session);

        await PresentedAsync(subject, session);

        CredentialCeremony ceremony = Value(await Service.UpgradeKeyAsync(
            Authority(subject, session),
            key.Credential,
            TestContext.Current.CancellationToken));

        Assert.True(ceremony.DiscoverableCredential);

        EnrolledCredential upgraded = Value(await Service.CompleteKeyAsync(
            Authority(subject, session),
            Attestation(ceremony.Challenge, synced: true),
            "This phone",
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(Factor.Passkey, Held(upgraded.Credential).Factor);
        Assert.Equal(AuthenticatorState.Active, Held(upgraded.Credential).State);
        Assert.Equal(AuthenticatorState.Invalidated, Held(key.Credential).State);
    }

    /// <summary>
    /// AUTH-FACT-002b AC3: an upgrade whose ceremony is refused leaves the account as
    /// it was, so the security key is still the account's and still a second step.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002b_AC3_AFailedUpgradeChangesNothingAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        EnrolledCredential key = await SecurityKeyAsync(subject, session);

        await PresentedAsync(subject, session);

        CredentialCeremony ceremony = Value(await Service.UpgradeKeyAsync(
            Authority(subject, session),
            key.Credential,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refused(await Service.CompleteKeyAsync(
                Authority(subject, session),
                Attestation(OpaqueToken.Draw(_randomness).Value, synced: true),
                "This phone",
                Source,
                TestContext.Current.CancellationToken)));

        Assert.NotEqual(ceremony.Challenge, string.Empty);
        Assert.Equal(AuthenticatorState.Active, Held(key.Credential).State);
        Assert.Single(_authenticators.All);
    }

    private CredentialService Service =>
        new(
            Keys,
            Totp,
            Codes,
            Passwords,
            Losses,
            Enrolments,
            Guard,
            Policies,
            _ceremonies,
            _authenticators,
            _passwords,
            _identifiers,
            _live,
            Sending,
            _credentials,
            _configuration,
            _work,
            _clock);

    private WebAuthnService Keys =>
        new(_authenticators, _passwords, _credentials, _configuration, _work, _clock, _randomness);

    private TotpService Totp =>
        new(_authenticators, _passwords, _configuration, _work, _clock, _randomness);

    private RecoveryCodeService Codes =>
        new(_sets, new Argon2idHasher(_randomness), _configuration, _work, _clock, _randomness);

    private StepUpGuard Guard => new(_live, _authenticators, _passwords, Policies, _clock);

    private PolicyResolution Policies => new(_memberships, _configuration, _raises);

    private SessionService Sessions =>
        new(_live, _audit, Policies, _configuration, _gate, _work, _clock, _randomness);

    private ThrottleService Throttle =>
        new(_configuration, _throttle, _work, _events, _clock);

    private LossReports Losses =>
        new(
            _reports,
            _authenticators,
            _passwords,
            _sets,
            _identifiers,
            Policies,
            Sending,
            _credentials,
            _configuration,
            _work,
            _clock,
            _randomness);

    private EnrolmentSessions Enrolments => new(_links, _work, _clock);

    private RecoveryService Recovery =>
        new(
            _links,
            _approvals,
            Losses,
            _recorded,
            _identifiers,
            _accounts,
            _authenticators,
            Passwords,
            Policies,
            _memberships,
            Sessions,
            Guard,
            _gate,
            Sending,
            new NonExistenceNotice(_configuration, Sending, _notices, _work, _events, _clock),
            Throttle,
            _events,
            _configuration,
            _work,
            _clock,
            _randomness);

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

    private static CredentialAuthority Authority(SubjectId subject, SessionId session) =>
        CredentialAuthority.Of(AccessContext.Of(subject), session);

    private static bool Succeeded(Result result) => result.Match(() => true, _ => false);

    private static ErrorCode Refused<TValue>(Result<TValue> result) =>
        result.Match(_ => default, error => error.Code);

    private static ErrorCode Refused(Result result) =>
        result.Match(() => default, error => error.Code);

    private static TValue Value<TValue>(Result<TValue> result) =>
        result.Match(value => value, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static LossReported? Reported(Result<LossReported?> result) =>
        result.Match(reported => reported, _ => null);

    // What a browser sends back from a creation ceremony: the challenge the server
    // issued, an origin the relying party admits, and a key the runtime can read.
    private static AuthenticatorAttestation Attestation(string challenge, bool synced)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        byte[] clientData = Encoding.UTF8.GetBytes(
            "{\"type\":\"webauthn.create\",\"challenge\":\""
            + challenge
            + "\",\"origin\":\""
            + Origin
            + "\"}");

        byte[] authenticatorData = new byte[37];

        SHA256.HashData(Encoding.UTF8.GetBytes(RelyingParty)).CopyTo(authenticatorData, 0);

        // User present and user verified, and where the credential is synced the two
        // backup flags AUTH-FACT-013 reads.
        authenticatorData[32] = synced ? (byte)0x1D : (byte)0x05;

        return new AuthenticatorAttestation(
            Base64Url.EncodeToString(Guid.NewGuid().ToByteArray()),
            Base64Url.EncodeToString(clientData),
            Base64Url.EncodeToString(authenticatorData),
            Base64Url.EncodeToString(key.ExportSubjectPublicKeyInfo()),
            Algorithm: -7);
    }

    private async ValueTask<EnrolledCredential> KeyAsync(
        SubjectId subject,
        SessionId session,
        bool synced)
    {
        CredentialCeremony ceremony = Value(await Service.BeginKeyAsync(
            Authority(subject, session),
            Factor.Passkey,
            TestContext.Current.CancellationToken));

        return Value(await Service.CompleteKeyAsync(
            Authority(subject, session),
            Attestation(ceremony.Challenge, synced),
            "This phone",
            Source,
            TestContext.Current.CancellationToken));
    }

    private Authenticator Held(AuthenticatorId credential) =>
        _authenticators.All.Single(held => held.Id == credential);

    private async ValueTask<EnrolledCredential> SecurityKeyAsync(
        SubjectId subject,
        SessionId session)
    {
        CredentialCeremony ceremony = Value(await Service.BeginKeyAsync(
            Authority(subject, session),
            Factor.SecurityKey,
            TestContext.Current.CancellationToken));

        return Value(await Service.CompleteKeyAsync(
            Authority(subject, session),
            Attestation(ceremony.Challenge, synced: false),
            "This key",
            Source,
            TestContext.Current.CancellationToken));
    }

    private async ValueTask<EnrolledCredential> ConfirmedAsync(SubjectId subject, SessionId session)
    {
        GeneratorEnrolment begun = Value(await Service.BeginGeneratorAsync(
            Authority(subject, session),
            "Phone",
            TestContext.Current.CancellationToken));

        return Value(await Service.ConfirmGeneratorAsync(
            Authority(subject, session),
            begun.Credential,
            Code(begun.Credential),
            Source,
            TestContext.Current.CancellationToken));
    }

    private string Code(AuthenticatorId generator) =>
        new Totp(
                _authenticators.All
                    .Single(credential => credential.Id == generator)
                    .Totp!.Secret.ToArray(),
                TotpCodes.StepSeconds,
                OtpHashMode.Sha1,
                TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);

    // A session that has just presented everything the account holds, which is what
    // stands between a person and a gate they can reach at all.
    private async ValueTask PresentedAsync(SubjectId subject, SessionId session)
    {
        Session live = (await _live.FindAsync(session, TestContext.Current.CancellationToken))!;

        IReadOnlyList<Authenticator> enrolled = await _authenticators
            .OfAsync(subject, TestContext.Current.CancellationToken);

        bool password =
            await _passwords.FindAsync(subject, TestContext.Current.CancellationToken) is not null;

        live.Present(
            StepUp.Reachable(HeldFactors.Of(enrolled, password).Standing),
            _clock.GetUtcNow());

        await _live.RecordAsync(live, TestContext.Current.CancellationToken);
    }

    private async ValueTask<EnrolmentSession> OpenedAsync(SubjectId subject)
    {
        var token = OpaqueToken.Draw(_randomness);

        await _links.ReplaceAsync(
            RecoveryLink.Issue(
                token,
                subject,
                RecoveryPurpose.Enrolment,
                _clock.GetUtcNow(),
                TimeSpan.FromHours(1)),
            TestContext.Current.CancellationToken);

        await _work.CommitAsync(TestContext.Current.CancellationToken);

        return Value(await Recovery.BeginEnrolmentAsync(
            token.Value,
            TestContext.Current.CancellationToken));
    }

    private async ValueTask<SubjectId> AccountAsync(bool password = true)
    {
        var subject = new SubjectId(Guid.NewGuid());

        _accounts.Stands(subject, AccountState.Active);
        _accounts.Registered(subject, _clock.GetUtcNow());
        _identifiers.Reads(subject, Language);

        IdentifierId email = _identifiers.Verified(subject, IdentifierKind.Email, Address);

        _ = _identifiers.Verified(subject, IdentifierKind.Phone, Number);

        await _identifiers.PromoteAsync(subject, email, TestContext.Current.CancellationToken);

        if (password)
        {
            byte[] presented = Encoding.UTF8.GetBytes(Secret);

            _ = await Passwords.SetAsync(
                subject,
                presented,
                [],
                AssuranceLevel.Aal1,
                TestContext.Current.CancellationToken);
        }

        await _work.CommitAsync(TestContext.Current.CancellationToken);

        return subject;
    }

    // An account signed in with what it holds: a password, or, where it holds none,
    // the passkey a passkey-only account is a passkey-only account by holding.
    private async ValueTask<(SubjectId Subject, SessionId Session)> SignedInAsync(
        bool password = true)
    {
        SubjectId subject = await AccountAsync(password);

        if (!password)
        {
            _authenticators.Hold(Authenticator.WebAuthnCredential(
                AuthenticatorId.New(_clock),
                subject,
                Factor.Passkey,
                Label("This phone"),
                new WebAuthnMaterial(
                    Guid.NewGuid().ToByteArray(),
                    new byte[] { 4, 5, 6 },
                    Algorithm: -7,
                    RelyingPartyId: RelyingParty,
                    BackupEligible: true,
                    BackupState: true,
                    Counter: 0),
                _clock.GetUtcNow()));
        }

        IssuedSession issued = Value(await Sessions.BeginAsync(
            subject,
            password ? [Factor.Password] : [Factor.Passkey],
            Somewhere,
            TestContext.Current.CancellationToken));

        return (subject, issued.Id);
    }

    private static CredentialLabel Label(string entered) =>
        CredentialLabel.TryParse(entered, out CredentialLabel label)
            ? label
            : throw new InvalidOperationException("The label is not one.");
}
