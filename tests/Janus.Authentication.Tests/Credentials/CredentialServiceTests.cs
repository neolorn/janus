using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Credentials;
using Janus.Authentication.Events;
using Janus.Authentication.Factors;
using Janus.Authentication.Invitations;
using Janus.Authentication.Organizations;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Records;
using Janus.Authentication.Recovery;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Invitations;
using Janus.Authentication.Tests.Oidc;
using Janus.Authentication.Tests.Organizations;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Records;
using Janus.Authentication.Tests.Recovery;
using Janus.Authentication.Tests.Registration;
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
    private static readonly AccessContext Sweeper = AccessContext.Of(
        SystemPrincipal.ForDeployment("expiry-sweep", "OPS-OBS-003", SystemOperation.ExpirySweep));

    private const string Language = "en";
    private const string Source = "198.51.100.7";
    private const string Address = "person@example.test";
    private const string Number = "+441632960011";
    private const string Elsewhere = "+441632960055";
    private const string Secret = "orangemarmaladeandtoast";
    private const string Another = "quincejellyonasaucer";
    private const string Origin = "https://app.example.com";
    private const string RelyingParty = "example.com";

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly OrganizationId Staff = new(Guid.NewGuid());

    private static readonly SessionOrigin Somewhere = new(Source, new DeviceDescription("Firefox", "Fedora"))
    {
        Location = new SessionLocation("Alexandria", "EG"),
    };

    private readonly KeyCeremonyStoreInMemory _ceremonies = new();
    private readonly RecoveryLinkStoreInMemory _links = new();
    private readonly RecoveryApprovalStoreInMemory _approvals = new();
    private readonly LossReportStoreInMemory _reports = new();
    private readonly RecoveryAuditInMemory _recorded = new();
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly AccountDirectoryInMemory _accounts = new(PreferenceDeclarations.None);
    private readonly SettingsRestrictionInMemory _restriction = new();
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
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly LocationResolverInMemory _locations = new();
    private readonly ThrottleLedgerInMemory _throttle = new();
    private readonly NoticeLedgerInMemory _notices = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly GovernedSendInMemory _notifications = new();
    private readonly SendingRestrictionsInMemory _restrictions = new();
    private readonly PendingEnrolmentStoreInMemory _enrolments = new();
    private readonly PhoneSignalAuditInMemory _considered = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private IEvents? _outbox;
    private PhoneSignalProvider? _provider;

    /// <summary>
    /// A deployment that can send the notice every enrolment carries, over origins the
    /// relying party identifier sits above.
    /// </summary>
    public CredentialServiceTests()
    {
        _notifications.Work = _work;
        _restrictions.Work = _work;
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);
        _configuration.Set(Settings.ServiceName, "Example");
        _configuration.Set(Settings.NotificationLanguages, [Language]);
        _configuration.Set(
            Settings.WebAuthnOrigins,
            (IReadOnlyList<string>)[Origin, "https://id.example.com"]);

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

        _notifications.Sent.Clear();

        _ = await ConfirmedAsync(subject, session);

        Assert.NotEmpty(_notifications.Mail);
        Assert.NotEmpty(_notifications.Texts);
    }

    /// <summary>
    /// AUTH-STEP-007, chapter 10 section 5b: an authenticator that reached active is
    /// announced once, carrying what was enrolled and whose it is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_007_AnEnrolmentThatReachedActiveIsAnnouncedAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        _events.Published.Clear();

        EnrolledCredential enrolled = await ConfirmedAsync(subject, session);

        CredentialEnrolled announced = Assert.Single(_events.Of<CredentialEnrolled>());

        Assert.Equal(enrolled.Credential, announced.Credential);
        Assert.Equal(FactorCatalogue.Generated, announced.Kind);
        Assert.Equal(subject, announced.Subject);
        Assert.Equal(subject, announced.Actor);
        Assert.Equal(Noon, announced.RaisedAt);
    }

    /// <summary>
    /// AUTH-STEP-007, CONV-DESIGN-002: the enrolment's event row is written in the one
    /// transaction the confirmation, the codes beside it and its record are written
    /// in, so a row that cannot be written fails the enrolment before anything of it
    /// commits, and no notice of an enrolment that does not stand goes out.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_007_AnEnrolmentWhoseEventRowFailsCommitsNothingAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        GeneratorEnrolment begun = Value(await Service.BeginGeneratorAsync(
            Authority(subject, session),
            "Phone",
            TestContext.Current.CancellationToken));

        _outbox = new EventOutbox(new PendingEventsUnwritable(), _work);
        _work.Reset();
        _notifications.Sent.Clear();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.ConfirmGeneratorAsync(
            Authority(subject, session),
            begun.Credential,
            Code(begun.Credential),
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Empty(_notifications.Sent);
        Assert.DoesNotContain(_events.Of<CredentialEnrolled>(), each => each.Credential == begun.Credential);
    }

    /// <summary>
    /// AUTH-STEP-007 AC4: a password set on an existing account, by the person in a
    /// session or through the enrolment session an approved recovery opened, is
    /// announced as an enrolment of the catalogue entry <c>password</c> with no
    /// credential identifier.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_007_AC4_ASetPasswordIsAnnouncedAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        SubjectId recovering = await AccountAsync(password: false);
        EnrolmentSession opened = await OpenedAsync(recovering);

        _events.Published.Clear();

        Assert.True(Succeeded(await Service.SetPasswordAsync(
            Authority(subject, session),
            Another,
            Source,
            TestContext.Current.CancellationToken)));
        Assert.True(Succeeded(await Service.SetPasswordAsync(
            CredentialAuthority.Of(opened.Id),
            Another,
            Source,
            TestContext.Current.CancellationToken)));

        IReadOnlyList<CredentialEnrolled> announced = _events.Of<CredentialEnrolled>();

        Assert.Equal(2, announced.Count);
        Assert.All(announced, each => Assert.Null(each.Credential));
        Assert.All(announced, each => Assert.Equal(FactorCatalogue.Password, each.Kind));
        Assert.Equal([subject, recovering], announced.Select(each => each.Subject));
        Assert.Equal([subject, recovering], announced.Select(each => each.Actor));
    }

    /// <summary>
    /// IDN-LIFE-008 (D-218): a password set in a session ends the account's other
    /// sessions, each announced as ended by the person, and the one that set it stands.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_008_APasswordSetEndsAndAnnouncesTheOtherSessionsAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IssuedSession other = Value(await Sessions.BeginAsync(
            subject,
            [Factor.Password],
            Somewhere,
            TestContext.Current.CancellationToken));

        _events.Published.Clear();

        Assert.True(Succeeded(await Service.SetPasswordAsync(
            Authority(subject, session),
            Another,
            Source,
            TestContext.Current.CancellationToken)));

        SessionEnded ended = Assert.Single(_events.Of<SessionEnded>());

        Assert.Equal(
            (other.Id, SessionEndReason.Ended, subject, subject),
            (ended.Session, ended.Reason, ended.Subject, ended.Actor));
        Assert.Null((await _live.FindAsync(session, TestContext.Current.CancellationToken))!.EndedAt);
    }

    /// <summary>
    /// AUTH-STEP-007 AC5: a social-only account whose session began longer ago than
    /// the maximum age the policy gives <c>factor:enrol</c> sets a password with no
    /// presentation, the gate's level being <c>delegated</c>, which asks no maximum
    /// age, and the enrolment is notified to every recorded channel.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_007_AC5_ASocialOnlyAccountSetsAPasswordFromASessionOlderThanTheGatesMaximumAgeAsync()
    {
        SubjectId subject = await AccountAsync(password: false);

        await _authenticators.LinkAsync(
            Authenticator.Linked(AuthenticatorId.New(_clock), subject, Factor.Google, Label("Google"), Noon),
            "provider-subject",
            TestContext.Current.CancellationToken);

        IssuedSession issued = Value(await Sessions.BeginAsync(
            subject,
            [Factor.Google],
            Somewhere,
            TestContext.Current.CancellationToken));

        _clock.Advance(
            Janus.Core.Policies.SystemDefault.Gates[StepUpAction.FactorEnrol].MaximumAge + TimeSpan.FromMinutes(1));
        _notifications.Sent.Clear();
        _events.Published.Clear();

        Result set = await Service.SetPasswordAsync(
            Authority(subject, issued.Id),
            Another,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Null(set.Match(() => (ErrorCode?)null, error => error.Code));
        Assert.NotEmpty(_notifications.Mail);
        Assert.NotEmpty(_notifications.Texts);
        Assert.Equal(FactorCatalogue.Password, Assert.Single(_events.Of<CredentialEnrolled>()).Kind);
    }

    /// <summary>
    /// AUTH-STEP-007, chapter 09 <c>POST /auth/step-up</c>: a social-only account whose
    /// session was downgraded since it signed in is refused the password it sets, and
    /// the refusal of that gate, whose level is <c>delegated</c> and which asks no
    /// maximum age, carries <c>maxAge</c> null and never the age the policy gives
    /// <c>factor:enrol</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_007_TheRefusalOfASocialOnlyAccountsEnrolmentGateCarriesNoMaximumAgeAsync()
    {
        SubjectId subject = await AccountAsync(password: false);

        await _authenticators.LinkAsync(
            Authenticator.Linked(AuthenticatorId.New(_clock), subject, Factor.Google, Label("Google"), Noon),
            "provider-subject",
            TestContext.Current.CancellationToken);

        IssuedSession issued = Value(await Sessions.BeginAsync(
            subject,
            [Factor.Google],
            Somewhere,
            TestContext.Current.CancellationToken));
        Session live = Assert.IsType<Session>(
            await _live.FindAsync(issued.Id, TestContext.Current.CancellationToken));

        live.Downgrade(_clock.GetUtcNow());

        Error? refused = (await Service.SetPasswordAsync(
                Authority(subject, issued.Id),
                Another,
                Source,
                TestContext.Current.CancellationToken))
            .Match(() => (Error?)null, error => error);

        Assert.Equal(ErrorCodes.StepUpRequired, refused?.Code);
        Assert.Equal("delegated", refused?.Details["required"].GetProperty("level").GetString());
        Assert.Equal(JsonValueKind.Null, refused?.Details["required"].GetProperty("maxAge").ValueKind);
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
    /// REG-PM-001: the ceremony an account opens carries that account's own subject
    /// identifier as the handle and its primary email as the name, and the display
    /// name is empty where the account shows none.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_PM_001_TheCeremonyCarriesTheAccountsHandleAndPrimaryEmailAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        CredentialCeremony ceremony = Value(await Service.BeginKeyAsync(
            Authority(subject, session),
            Factor.Passkey,
            TestContext.Current.CancellationToken));

        Assert.Equal(
            subject.Value,
            new Guid(Base64Url.DecodeFromChars(ceremony.User.Id), bigEndian: true));
        Assert.Equal(Address, ceremony.User.Name);
        Assert.Equal(string.Empty, ceremony.User.DisplayName);
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
            ErrorCodes.FactorPasswordRequired,
            Refused(await Service.GenerateRecoveryCodesAsync(
                Authority(subject, session),
                TestContext.Current.CancellationToken)));

        Assert.Null(await _sets.FindAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-008 AC4: a set is viewed in the unit of work whose response returns
    /// it, the second-step enrolment's and the generation's alike, so a set returned
    /// has the instant, and neither leaves it exported.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AC4_ASetReturnedIsViewedInTheUnitOfWorkThatReturnsItAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        _ = await ConfirmedAsync(subject, session);
        RecoveryCodeSet brought = (await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!;
        await PresentedAsync(subject, session);
        _clock.Advance(TimeSpan.FromMinutes(3));
        int committed = _work.OutermostCommitted;
        GeneratedRecoveryCodes generated = Value(await Service.GenerateRecoveryCodesAsync(
            Authority(subject, session),
            TestContext.Current.CancellationToken));
        RecoveryCodeSet regenerated = (await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!;

        Assert.Equal((Noon, null), (brought.ViewedAt, brought.ExportedAt));
        Assert.Equal((generated.GeneratedAt, null), (regenerated.ViewedAt, regenerated.ExportedAt));
        Assert.Equal(Noon + TimeSpan.FromMinutes(3), regenerated.ViewedAt);
        Assert.Equal(committed + 1, _work.OutermostCommitted);
    }

    /// <summary>
    /// AUTH-FACT-008 AC4 and AUTH-RECOV-006 AC2: the report of a copy, download or
    /// print is recorded on the set the account holds, at the instant it is made.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AC4_AReportedExportIsRecordedOnTheSetAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        _ = await ConfirmedAsync(subject, session);

        _clock.Advance(TimeSpan.FromMinutes(2));

        Accepted(await Service.MarkRecoveryCodesExportedAsync(
            AccessContext.Of(subject),
            TestContext.Current.CancellationToken));

        RecoveryCodeSet? held = await _sets.FindAsync(subject, TestContext.Current.CancellationToken);

        Assert.NotNull(held);
        Assert.Equal(_clock.GetUtcNow(), held.ExportedAt);
    }

    /// <summary>
    /// AUTH-FACT-008 and chapter 09 section 4: an account holding no set has nothing an
    /// export could be recorded on, which is the service's refusal.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AnExportReportedWithNoSetIsRefusedAsNotEnrolledAsync()
    {
        (SubjectId subject, _) = await SignedInAsync();

        Assert.Equal(
            ErrorCodes.FactorNotEnrolled,
            Refused(await Service.MarkRecoveryCodesExportedAsync(
                AccessContext.Of(subject),
                TestContext.Current.CancellationToken)));

        Assert.Null(await _sets.FindAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-ACCT-007 AC2: recording an export changes the set's record, so a restricted
    /// account is refused it with the code the gate refuses a modifying action with,
    /// before any unit of work begins, and the set stays unexported.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_007_AC2_ARestrictedAccountRecordsNoExportAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        _ = await ConfirmedAsync(subject, session);
        _restriction.Restrict(subject);
        int opened = _work.Opened;

        ErrorCode refused = Refused(await Service.MarkRecoveryCodesExportedAsync(
            AccessContext.Of(subject),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.Restricted, refused);
        Assert.Equal(opened, _work.Opened);
        Assert.Null((await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!.ExportedAt);
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC3: a restriction of the account committed after the gate step
    /// and before the first write refuses the report of an export inside its unit of
    /// work, which rolls back and leaves the set unexported.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ARestrictionCommittedSinceTheGateStepRefusesTheExportAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        _ = await ConfirmedAsync(subject, session);

        await RestrictedSinceTheGateStepAsync(
            subject,
            async () => Refused(await Service.MarkRecoveryCodesExportedAsync(
                AccessContext.Of(subject),
                TestContext.Current.CancellationToken)));

        Assert.Null((await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!.ExportedAt);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10: the first report of an export commits its write, and a
    /// report made again, which changes nothing, is a success that wrote nothing and
    /// leaves its unit of work rolled back with the first instant standing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_AnExportReportedAgainWritesNothingAndRollsBackAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        _ = await ConfirmedAsync(subject, session);
        int committed = _work.OutermostCommitted;
        int rolledBack = _work.RolledBack;

        Accepted(await Service.MarkRecoveryCodesExportedAsync(
            AccessContext.Of(subject),
            TestContext.Current.CancellationToken));
        _clock.Advance(TimeSpan.FromMinutes(2));
        Accepted(await Service.MarkRecoveryCodesExportedAsync(
            AccessContext.Of(subject),
            TestContext.Current.CancellationToken));

        Assert.Equal((committed + 1, rolledBack + 1), (_work.OutermostCommitted, _work.RolledBack));
        Assert.False(_work.Open);
        Assert.Equal(Noon, (await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!.ExportedAt);
    }

    /// <summary>
    /// CONV-DESIGN-002 AC3: the operation is over the caller's own set, so a context
    /// that names no account is denied and no set is read for it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_002_AC3_AnExportReportedByNoAccountIsDeniedAsync() =>
        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Service.MarkRecoveryCodesExportedAsync(
                Sweeper,
                TestContext.Current.CancellationToken)));

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
            ErrorCodes.FactorPasswordRequired,
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
    /// IDN-ACCT-007 AC2: a restricted account changes none of its credentials: a new
    /// password, a passkey and a code generator are each refused with the code the gate
    /// refuses a modifying action with, and nothing is enrolled.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_007_AC2_ARestrictedAccountChangesNoCredentialAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        _restriction.Restrict(subject);

        Assert.Equal(
            ErrorCodes.Restricted,
            Refused(await Service.SetPasswordAsync(
                Authority(subject, session),
                Another,
                Source,
                TestContext.Current.CancellationToken)));
        Assert.Equal(
            ErrorCodes.Restricted,
            Refused(await Service.BeginKeyAsync(
                Authority(subject, session),
                Factor.Passkey,
                TestContext.Current.CancellationToken)));
        Assert.Equal(
            ErrorCodes.Restricted,
            Refused(await Service.BeginGeneratorAsync(
                Authority(subject, session),
                "A generator",
                TestContext.Current.CancellationToken)));

        Assert.Empty(await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken));
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
    /// AUTH-FACT-001 AC5: an enrolment under a label the account already holds for that
    /// kind, in other capitals, is refused and enrols nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_001_AC5_AnEnrolmentUnderALabelHeldInOtherCapitalsIsRefusedAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        EnrolledCredential confirmed = await ConfirmedAsync(subject, session);
        await PresentedAsync(subject, session);

        Assert.Equal(
            ErrorCodes.CredentialLabelInvalid,
            Refused(await Service.BeginGeneratorAsync(
                Authority(subject, session),
                "PHONE",
                TestContext.Current.CancellationToken)));

        Assert.Equal(
            [confirmed.Credential],
            (await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken))
                .Select(credential => credential.Id));
    }

    /// <summary>
    /// AUTH-RECOV-007 AC5 and D-092: removing the credential the account's assurance
    /// rests on runs the notified window rather than taking it away at once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC5_RemovingTheLastSecondStepRunsTheWindowAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        EnrolledCredential confirmed = await ConfirmedAsync(subject, session);

        await PresentedAsync(subject, session);

        _work.Reset();

        Error window = Failure(await Service.RemoveAsync(
            Authority(subject, session),
            confirmed.Credential,
            Source,
            TestContext.Current.CancellationToken));

        // CONV-DESIGN-003 AC13 (D-207): the removal answered as the status commits the
        // suspension in its own unit of work, rolling nothing back, and the notices it
        // undertakes and what became of them commit after it.
        Assert.False(_work.Open);
        Assert.Equal(0, _work.RolledBack);
        Assert.Equal(3, _work.OutermostCommitted);
        Assert.Equal(3, _work.Committed);

        Assert.Equal(ErrorCodes.CredentialLastSecondFactor, window.Code);
        Assert.Equal(
            _clock.GetUtcNow() + TimeSpan.FromDays(7),
            window.Details["invalidatesAt"].GetDateTimeOffset());
        Assert.Equal(
            AuthenticatorState.Suspended,
            (await _authenticators.FindAsync(
                confirmed.Credential,
                TestContext.Current.CancellationToken))!.State);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC5 and CONV-DESIGN-003 AC13 (D-207): a removal that would lower
    /// reachable assurance suspends the credential in its own unit of work, on the row
    /// its decision read under the lock, so a sign-in's write to the credential that
    /// waited on that lock and committed as soon as the removal's transaction ended is
    /// not overwritten by the suspension.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC5_ASignInsWriteWhileTheRemovalRunsIsNotOverwrittenAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        EnrolledCredential confirmed = await ConfirmedAsync(subject, session);

        await PresentedAsync(subject, session);

        DateTimeOffset signedIn = _clock.GetUtcNow() + TimeSpan.FromMinutes(1);

        _authenticators.Locking = held =>
        {
            if (held.Id == confirmed.Credential)
            {
                _authenticators.Locking = null;
                _work.Meanwhile = () => SignedIn(confirmed.Credential, signedIn);
            }
        };

        Error window = Failure(await Service.RemoveAsync(
            Authority(subject, session),
            confirmed.Credential,
            Source,
            TestContext.Current.CancellationToken));

        Authenticator stood = Held(confirmed.Credential);

        Assert.Equal(ErrorCodes.CredentialLastSecondFactor, window.Code);
        Assert.Null(_work.Meanwhile);
        Assert.Equal(AuthenticatorState.Suspended, stood.State);
        Assert.Equal(signedIn, stood.LastUsedAt);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC7: an account whose one passkey is gone and whose password came
    /// from recovery reports the passkey on that password alone, and once the window has
    /// run it enrols another on the same password.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC7_ARecoveredPasswordReportsThePasskeyAndEnrolsAnotherAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync(password: false);

        byte[] recovered = Encoding.UTF8.GetBytes(Another);

        _ = await Passwords.SetAsync(
            subject,
            recovered,
            [],
            AssuranceLevel.Aal1,
            actor: null,
            TestContext.Current.CancellationToken);

        AuthenticatorId lost = _authenticators.All.Single().Id;

        Assert.NotNull(Value(await Losses.ReportAsync(
            AccessContext.Of(subject),
            lost,
            Source,
            TestContext.Current.CancellationToken)));
        Assert.Equal(AuthenticatorState.Suspended, Held(lost).State);

        _clock.Advance(TimeSpan.FromDays(8));

        _ = await Losses.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken);

        Assert.Equal(AuthenticatorState.Invalidated, Held(lost).State);

        await PresentedAsync(subject, session);

        EnrolledCredential enrolled = await KeyAsync(subject, session, synced: true);

        Assert.Equal(AuthenticatorState.Active, Held(enrolled.Credential).State);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC5 and D-092: removing one of several credentials leaves what
    /// the account reaches where it was, so it goes at once after the gate.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC5_RemovingOneOfSeveralCredentialsCompletesAtOnceAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        EnrolledCredential confirmed = await ConfirmedAsync(subject, session);

        await PresentedAsync(subject, session);

        EnrolledCredential key = await KeyAsync(subject, session, synced: true);

        await PresentedAsync(subject, session);

        Accepted(await Service.RemoveAsync(
            Authority(subject, session),
            confirmed.Credential,
            Source,
            TestContext.Current.CancellationToken));

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
            ErrorCodes.SessionExpired,
            Refused(await Service.BeginGeneratorAsync(
                CredentialAuthority.Of(opened.Id),
                "Phone",
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// IDN-ACCT-007 AC2 (D-166): the enrolment session an approved recovery opened is how
    /// a restricted account gets back in, so it sets the password it enrols, while the
    /// account's own session is still refused the same change.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_007_AC2_AnApprovedRecoveryEnrolsForARestrictedAccountAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        _restriction.Restrict(subject);

        EnrolmentSession opened = await OpenedAsync(subject);

        Assert.True(Succeeded(await Service.SetPasswordAsync(
            CredentialAuthority.Of(opened.Id),
            Another,
            Source,
            TestContext.Current.CancellationToken)));
        Assert.Equal(
            ErrorCodes.Restricted,
            Refused(await Service.SetPasswordAsync(
                Authority(subject, session),
                Another,
                Source,
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
            ErrorCodes.SessionExpired,
            Refused(await Service.SetPasswordAsync(
                CredentialAuthority.Of(opened.Id),
                Another,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Null(await _passwords.FindAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-RECOV-002, D-188: an enrolment session that stands reaches none of the
    /// operations chapter 09 does not name for it, and each refuses it as a missing
    /// permission is refused, with nothing of the account changed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_TheEnrolmentSessionIsDeniedWhatItDoesNotReachAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        EnrolledCredential generator = await ConfirmedAsync(subject, session);
        var authority = CredentialAuthority.Of((await OpenedAsync(subject)).Id);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        ErrorCode[] refused = await UnreachedAsync(authority, generator.Credential, cancellationToken);

        Assert.All(refused, code => Assert.Equal(ErrorCodes.Denied, code));
        Assert.NotNull(await _authenticators.FindAsync(generator.Credential, cancellationToken));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-002, D-188: an enrolment session that has ended is a session that
    /// has ended at every operation it reached, and nothing of the account changes.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_AnEndedEnrolmentSessionIsExpiredAtWhatItReachedAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        _ = await ConfirmedAsync(subject, session);
        EnrolmentSession opened = await OpenedAsync(subject);
        var authority = CredentialAuthority.Of(opened.Id);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        _clock.Advance(TimeSpan.FromHours(2));

        ErrorCode[] refused =
        [
            Refused(await Service.SetPasswordAsync(authority, Another, Source, cancellationToken)),
            Refused(await Service.BeginKeyAsync(authority, Factor.Passkey, cancellationToken)),
            Refused(await Service.BeginGeneratorAsync(authority, "Phone", cancellationToken)),
            Refused(await Service.MarkRecoveryCodesExportedAsync(opened.Id, cancellationToken)),
        ];

        Assert.All(refused, code => Assert.Equal(ErrorCodes.SessionExpired, code));
        Assert.Null((await _sets.FindAsync(subject, cancellationToken))!.ExportedAt);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// CONV-DESIGN-002 AC3, D-189: an operation chapter 09 does not list for the
    /// enrolment session refuses a context of its authority first, before any load, so
    /// one that stands and one that has ended are refused alike and the link the
    /// session stands on is never read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_002_AC3_AnEnrolmentSessionsAuthorityIsDeniedBeforeAnyLoadAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        EnrolledCredential generator = await ConfirmedAsync(subject, session);
        var authority = CredentialAuthority.Of((await OpenedAsync(subject)).Id);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        int read = _links.SessionReads;

        ErrorCode[] standing = await UnreachedAsync(authority, generator.Credential, cancellationToken);
        _clock.Advance(TimeSpan.FromHours(2));
        ErrorCode[] ended = await UnreachedAsync(authority, generator.Credential, cancellationToken);

        Assert.All(standing, code => Assert.Equal(ErrorCodes.Denied, code));
        Assert.All(ended, code => Assert.Equal(ErrorCodes.Denied, code));
        Assert.Equal(read, _links.SessionReads);
        Assert.NotNull(await _authenticators.FindAsync(generator.Credential, cancellationToken));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-006 AC6, D-190: in an enrolment session whose second step has shown
    /// no codes, the report of an export is refused <c>authz.denied</c> and records
    /// nothing: the set the account already holds stays unexported, the session stays
    /// open and no unit of work is begun.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_006_AC6_AReportBeforeTheSecondStepShowedCodesIsDeniedAndRecordsNothingAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        _ = await ConfirmedAsync(subject, session);
        EnrolmentSession opened = await OpenedAsync(subject);
        _work.Reset();

        ErrorCode refused = Refused(await Service.MarkRecoveryCodesExportedAsync(
            opened.Id,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.Denied, refused);
        Assert.Null((await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!.ExportedAt);
        Assert.NotNull(await Enrolments.FindAsync(opened.Id, TestContext.Current.CancellationToken));
        Assert.Equal(0, _work.Opened);
    }

    /// <summary>
    /// AUTH-RECOV-006 AC6, D-190: that refusal is decided before any other and before
    /// the restriction is asked, so a restricted account, and one that holds no set, is
    /// answered <c>authz.denied</c> there and neither <c>authz.restricted</c> nor
    /// <c>auth.factor.notenrolled</c>.
    /// </summary>
    /// <param name="restricted">Whether the account's processing is restricted.</param>
    /// <param name="holdsCodes">Whether the account holds a recovery-code set.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task AUTH_RECOV_006_AC6_TheReportIsDeniedBeforeAnyOtherRefusalAndTheRestrictionAsync(
        bool restricted,
        bool holdsCodes)
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        if (holdsCodes)
        {
            _ = await ConfirmedAsync(subject, session);
        }

        EnrolmentSession opened = await OpenedAsync(subject);
        if (restricted)
        {
            _restriction.Restrict(subject);
        }

        bool asked = false;
        _restriction.Admitted = _ => asked = true;

        ErrorCode refused = Refused(await Service.MarkRecoveryCodesExportedAsync(
            opened.Id,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.Denied, refused);
        Assert.False(asked);
        Assert.Null((await _sets.FindAsync(subject, TestContext.Current.CancellationToken))?.ExportedAt);
    }

    /// <summary>
    /// AUTH-RECOV-006 AC5 and AC2, D-189: an enrolment session whose second step
    /// showed recovery codes stays open on what it reaches, whatever else it sets
    /// there, until the report of their export ends it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_006_AC5_AnEnrolmentSessionThatShowedCodesStaysOpenUntilTheirExportIsReportedAsync()
    {
        SubjectId subject = await AccountAsync();
        EnrolmentSession opened = await OpenedAsync(subject);
        var authority = CredentialAuthority.Of(opened.Id);

        EnrolledCredential confirmed = await ConfirmedAsync(authority);

        _clock.Advance(TimeSpan.FromMinutes(2));

        EnrolmentSession? standing = await Enrolments.FindAsync(opened.Id, TestContext.Current.CancellationToken);
        bool reached = Succeeded(await Service.SetPasswordAsync(
            authority,
            Another,
            Source,
            TestContext.Current.CancellationToken));
        EnrolmentSession? afterPassword = await Enrolments.FindAsync(opened.Id, TestContext.Current.CancellationToken);

        Accepted(await Service.MarkRecoveryCodesExportedAsync(opened.Id, TestContext.Current.CancellationToken));

        Assert.NotNull(confirmed.RecoveryCodes);
        Assert.NotNull(standing);
        Assert.True(reached);
        Assert.NotNull(afterPassword);
        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.MarkRecoveryCodesExportedAsync(opened.Id, TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-RECOV-006 AC5 and AC2, D-189: the one report of the confirm-saved control
    /// sets the export against the set the second step showed and ends the enrolment
    /// session, which then reaches nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_006_AC5_TheReportSetsTheExportAndEndsTheEnrolmentSessionAsync()
    {
        SubjectId subject = await AccountAsync();
        EnrolmentSession opened = await OpenedAsync(subject);
        var authority = CredentialAuthority.Of(opened.Id);
        _ = await ConfirmedAsync(authority);
        _clock.Advance(TimeSpan.FromMinutes(2));

        Accepted(await Service.MarkRecoveryCodesExportedAsync(opened.Id, TestContext.Current.CancellationToken));

        Assert.Equal(
            _clock.GetUtcNow(),
            (await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!.ExportedAt);
        Assert.Null(await Enrolments.FindAsync(opened.Id, TestContext.Current.CancellationToken));
        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.BeginGeneratorAsync(authority, "Tablet", TestContext.Current.CancellationToken)));
        Assert.Equal(
            ErrorCodes.SessionExpired,
            Refused(await Service.MarkRecoveryCodesExportedAsync(opened.Id, TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-RECOV-006 AC5, D-148, D-189: an enrolment that showed no recovery codes
    /// ends the enrolment session as before, a passkey bringing none.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_006_AC5_AnEnrolmentThatShowedNoCodesEndsTheEnrolmentSessionAsync()
    {
        SubjectId subject = await AccountAsync();
        EnrolmentSession opened = await OpenedAsync(subject);
        var authority = CredentialAuthority.Of(opened.Id);

        EnrolledCredential enrolled = await PasskeyAsync(authority);

        Assert.Null(enrolled.RecoveryCodes);
        Assert.Null(await Enrolments.FindAsync(opened.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-ACCT-007 AC2, D-189: the report that completes an enrolment session is
    /// admitted for a restricted account, as the second step it completes is, while
    /// the account's own session is still refused the same report.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_007_AC2_ARestrictedAccountsReportCompletingAnEnrolmentSessionIsAdmittedAsync()
    {
        (SubjectId subject, SessionId _) = await SignedInAsync();
        _restriction.Restrict(subject);
        EnrolmentSession opened = await OpenedAsync(subject);
        _ = await ConfirmedAsync(CredentialAuthority.Of(opened.Id));

        ErrorCode own = Refused(await Service.MarkRecoveryCodesExportedAsync(
            AccessContext.Of(subject),
            TestContext.Current.CancellationToken));

        Accepted(await Service.MarkRecoveryCodesExportedAsync(opened.Id, TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.Restricted, own);
        Assert.Equal(
            _clock.GetUtcNow(),
            (await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!.ExportedAt);
        Assert.Null(await Enrolments.FindAsync(opened.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-ACCT-007 AC2, D-189: a WebAuthn enrolment that sets a credential by
    /// recovery in an enrolment session is admitted for a restricted account, while
    /// the account's own session is refused the same ceremony.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_007_AC2_ARestrictedAccountEnrolsAKeyByRecoveryInAnEnrolmentSessionAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        _restriction.Restrict(subject);
        EnrolmentSession opened = await OpenedAsync(subject);

        ErrorCode own = Refused(await Service.BeginKeyAsync(
            Authority(subject, session),
            Factor.Passkey,
            TestContext.Current.CancellationToken));

        EnrolledCredential enrolled = await PasskeyAsync(CredentialAuthority.Of(opened.Id));

        Assert.Equal(ErrorCodes.Restricted, own);
        Assert.Contains(_authenticators.All, held => held.Id == enrolled.Credential && held.Confirmed);
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

        _work.Reset();

        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refused(await Service.CompleteKeyAsync(
                Authority(subject, session),
                Attestation(OpaqueToken.Draw(_randomness).Value, synced: true),
                "This phone",
                Source,
                TestContext.Current.CancellationToken)));

        // CONV-DESIGN-003 AC5: the refusal ends the unit of work the account's row was
        // held in with nothing committed.
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);

        Assert.NotEqual(ceremony.Challenge, string.Empty);
        Assert.Equal(AuthenticatorState.Active, Held(key.Credential).State);
        Assert.Single(_authenticators.All);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a password the floor refuses is refused after the enrolment
    /// session was held, and the refusal ends the unit of work with nothing committed
    /// and the session still open.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ARefusedPasswordRollsBackAsync()
    {
        SubjectId subject = await AccountAsync(password: false);
        EnrolmentSession opened = await OpenedAsync(subject);

        _work.Reset();

        Assert.Equal(
            ErrorCodes.PasswordTooShort,
            Refused(await Service.SetPasswordAsync(
                CredentialAuthority.Of(opened.Id),
                "short",
                Source,
                TestContext.Current.CancellationToken)));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.Null(await _passwords.FindAsync(subject, TestContext.Current.CancellationToken));
        Assert.NotNull(await Enrolments.FindAsync(opened.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a generator confirmed with a wrong code is refused after the
    /// account's row was held, and the refusal ends the unit of work with nothing
    /// committed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AGeneratorConfirmedWithAWrongCodeRollsBackAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        GeneratorEnrolment begun = Value(await Service.BeginGeneratorAsync(
            Authority(subject, session),
            "Phone",
            TestContext.Current.CancellationToken));

        string right = Code(begun.Credential);

        _work.Reset();

        Assert.Equal(
            ErrorCodes.CodeInvalid,
            Refused(await Service.ConfirmGeneratorAsync(
                Authority(subject, session),
                begun.Credential,
                string.Equals(right, "000000", StringComparison.Ordinal) ? "111111" : "000000",
                Source,
                TestContext.Current.CancellationToken)));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.False(Held(begun.Credential).Confirmed);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: an enrolment that cannot be announced is refused after the
    /// confirmation and the codes beside it were written, and the refusal ends the
    /// unit of work with nothing of it committed and no notice sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AnEnrolmentThatCannotBeAnnouncedRollsBackAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        GeneratorEnrolment begun = Value(await Service.BeginGeneratorAsync(
            Authority(subject, session),
            "Phone",
            TestContext.Current.CancellationToken));

        _events.Refusal = Error.From(ErrorCodes.SystemFault);
        _notifications.Sent.Clear();
        _work.Reset();

        Assert.Equal(
            ErrorCodes.SystemFault,
            Refused(await Service.ConfirmGeneratorAsync(
                Authority(subject, session),
                begun.Credential,
                Code(begun.Credential),
                Source,
                TestContext.Current.CancellationToken)));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_notifications.Sent);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: the removal of a credential the account does not hold is
    /// refused under the lock on its credentials, and the refusal ends the unit of work
    /// with nothing committed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ARemovalOfAnUnknownCredentialRollsBackAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        _work.Reset();

        Assert.Equal(
            ErrorCodes.CredentialNotFound,
            Refused(await Service.RemoveAsync(
                Authority(subject, session),
                AuthenticatorId.New(_clock),
                Source,
                TestContext.Current.CancellationToken)));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a link that finds, under the lock on the account's row, an
    /// identity of the provider linked meanwhile is refused, and the refusal ends the
    /// unit of work with nothing committed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ALinkOfAProviderLinkedMeanwhileRollsBackAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        _accounts.Holding = held => _authenticators.Hold(
            Authenticator.Linked(AuthenticatorId.New(_clock), held, Factor.Google, Label("Google"), Noon));
        _work.Reset();

        Assert.Equal(
            ErrorCodes.FactorRejected,
            Refused(await Service.LinkAsync(
                Authority(subject, session),
                Factor.Google,
                "provider-subject",
                Label("Google"),
                Source,
                TestContext.Current.CancellationToken)));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.DoesNotContain(_credentials.Records, record => record.Action == AuditActions.CredentialEnrolled);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a link that cannot be announced is refused after it was
    /// written, and the refusal ends the unit of work with nothing committed and no
    /// notice sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ALinkThatCannotBeAnnouncedRollsBackAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        _events.Refusal = Error.From(ErrorCodes.SystemFault);
        _notifications.Sent.Clear();
        _work.Reset();

        Assert.Equal(
            ErrorCodes.SystemFault,
            Refused(await Service.LinkAsync(
                Authority(subject, session),
                Factor.Google,
                "provider-subject",
                Label("Google"),
                Source,
                TestContext.Current.CancellationToken)));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.Empty(_notifications.Sent);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: an unlink that finds, under the locks on the account's
    /// credentials, the identity gone meanwhile is refused, and the refusal ends the
    /// unit of work with nothing committed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AnUnlinkOfAnIdentityGoneMeanwhileRollsBackAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();

        var linked = Authenticator.Linked(AuthenticatorId.New(_clock), subject, Factor.Google, Label("Google"), Noon);

        await _authenticators.LinkAsync(linked, "provider-subject", TestContext.Current.CancellationToken);
        await PresentedAsync(subject, session);

        _authenticators.Locking = credential =>
            _ = _authenticators.RemoveAsync(credential.Id, TestContext.Current.CancellationToken).AsTask();
        _work.Reset();

        Assert.Equal(
            ErrorCodes.CredentialNotFound,
            Refused(await Service.UnlinkAsync(
                Authority(subject, session),
                Factor.Google,
                Source,
                TestContext.Current.CancellationToken)));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.DoesNotContain(_credentials.Records, record => record.Action == AuditActions.CredentialRemoved);
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC3: a restriction of the account committed after the gate step
    /// and before the first write refuses the opening and the completing of a key's
    /// ceremony, a key's upgrade, a removal, a link and an unlink, each inside its unit
    /// of work, which rolls back and leaves the credentials as they stood.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ARestrictionCommittedSinceTheGateStepRefusesEachCredentialChangeAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (SubjectId subject, SessionId session) = await SignedInAsync();
        CredentialAuthority authority = Authority(subject, session);

        await RestrictedSinceTheGateStepAsync(
            subject,
            async () => Refused(await Service.BeginKeyAsync(authority, Factor.SecurityKey, cancellationToken)));

        CredentialCeremony ceremony = Value(await Service.BeginKeyAsync(authority, Factor.SecurityKey, cancellationToken));

        await RestrictedSinceTheGateStepAsync(
            subject,
            async () => Refused(await Service.CompleteKeyAsync(
                authority,
                Attestation(ceremony.Challenge, synced: false),
                "This key",
                Source,
                cancellationToken)));

        Assert.Empty(_authenticators.All);

        EnrolledCredential key = Value(await Service.CompleteKeyAsync(
            authority,
            Attestation(ceremony.Challenge, synced: false),
            "This key",
            Source,
            cancellationToken));

        await PresentedAsync(subject, session);
        await RestrictedSinceTheGateStepAsync(
            subject,
            async () => Refused(await Service.UpgradeKeyAsync(authority, key.Credential, cancellationToken)));
        await RestrictedSinceTheGateStepAsync(
            subject,
            async () => Refused(await Service.RemoveAsync(authority, key.Credential, Source, cancellationToken)));
        await RestrictedSinceTheGateStepAsync(
            subject,
            async () => Refused(await Service.LinkAsync(
                authority,
                Factor.Google,
                "provider-subject",
                Label("Google"),
                Source,
                cancellationToken)));

        Assert.Equal(AuthenticatorState.Active, Assert.Single(_authenticators.All).State);

        var linked = Authenticator.Linked(AuthenticatorId.New(_clock), subject, Factor.Google, Label("Google"), Noon);

        await _authenticators.LinkAsync(linked, "provider-subject", cancellationToken);
        await PresentedAsync(subject, session);
        await RestrictedSinceTheGateStepAsync(
            subject,
            async () => Refused(await Service.UnlinkAsync(authority, Factor.Google, Source, cancellationToken)));

        Assert.Equal([key.Credential, linked.Id], _authenticators.All.Select(held => held.Id));
    }

    /// <summary>
    /// AUTHZ-IMP-001 AC5: an enrolment that reached active is announced with the acting
    /// and the effective identity of the context that confirmed it, each as the context
    /// gives it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_IMP_001_AC5_AConfirmedEnrolmentCarriesBothIdentitiesOfItsContextAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        var context = AccessContext.Of(subject);
        var authority = CredentialAuthority.Of(context, session);

        _events.Published.Clear();

        GeneratorEnrolment begun = Value(await Service.BeginGeneratorAsync(
            authority,
            "Phone",
            TestContext.Current.CancellationToken));

        _ = Value(await Service.ConfirmGeneratorAsync(
            authority,
            begun.Credential,
            Code(begun.Credential),
            Source,
            TestContext.Current.CancellationToken));

        CredentialEnrolled announced = Assert.Single(_events.Of<CredentialEnrolled>());

        Assert.Equal((context.Acting, context.Effective), (announced.Actor, announced.Effective));
    }

    /// <summary>
    /// AUTHZ-IMP-001 AC5: a linked provider is announced with the acting and the
    /// effective identity of the context that linked it, each as the context gives it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_IMP_001_AC5_ALinkedProviderCarriesBothIdentitiesOfItsContextAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        var context = AccessContext.Of(subject);

        _events.Published.Clear();

        Assert.True(Succeeded(await Service.LinkAsync(
            CredentialAuthority.Of(context, session),
            Factor.Google,
            "provider-subject",
            Label("Google"),
            Source,
            TestContext.Current.CancellationToken)));

        CredentialEnrolled announced = Assert.Single(_events.Of<CredentialEnrolled>());

        Assert.Equal((context.Acting, context.Effective), (announced.Actor, announced.Effective));
    }

    /// <summary>
    /// AUTH-FACT-002 AC9: a text code is enrolled by a begin naming a verified number
    /// of the account, which texts that number a code as <c>secondstep-code</c> under
    /// the purpose <c>secondfactor</c> and stages no credential, and a confirm
    /// presenting the code, which makes the credential active, bound to that number.
    /// The send is named by the enrolment, and its one count is released when the code
    /// is presented right (AUTH-ABUSE-004).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AC9_ABeginTextsTheNamedNumberAndAConfirmMakesTheCredentialActiveBoundToItAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);

        Accepted(await BegunAsync(subject, session, number));

        OutboundMessage texted = Assert.Single(_notifications.Texts);
        PendingEnrolment staged = Assert.Single(_enrolments.All);

        Assert.Equal(
            (MessageKind.SecondStepCode, RestrictionPurpose.SecondFactor, Number, (SubjectId?)subject),
            (texted.Message, texted.Purpose, texted.Destination.Canonical, texted.Subject));
        Assert.Equal((subject, TextedEnrolments.Entry, Number), (staged.Subject, staged.Factor, staged.Number));
        Assert.Equal(Noon + TimeSpan.FromMinutes(10), staged.ExpiresAt);
        Assert.Equal(staged.Send, Assert.Single(_restrictions.Carried).Answer);
        Assert.Empty(await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken));
        Assert.False(_work.Open);

        TextCodeEnrolment enrolled = Value(await Service.ConfirmTextCodeAsync(
            Authority(subject, session),
            texted.Values["code"],
            Source,
            TestContext.Current.CancellationToken));

        Authenticator held = Assert.Single(await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken));

        Assert.Equal((enrolled.Credential.Id, Number, true), (held.Id, held.Number, held.IsUsable));
        Assert.Equal(
            (TextedEnrolments.Entry, "My phone", AuthenticatorState.Active),
            (enrolled.Credential.Kind, enrolled.Credential.Label, enrolled.Credential.State));
        Assert.Equal(Noon, enrolled.Credential.AddedAt);
        Assert.NotNull(enrolled.RecoveryCodes);
        Assert.NotEmpty(enrolled.RecoveryCodes.Codes);
        Assert.Empty(_enrolments.All);
        Assert.Equal(staged.Send, Assert.Single(_restrictions.Answered));
        Assert.Contains((AuditActions.CredentialEnrolled, subject, held.Id), _credentials.Records);
        Assert.Contains(_events.Of<CredentialEnrolled>(), announced => announced.Credential == held.Id);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-STEP-007 AC1 and chapter 09: the enrolment of a text code is told to every
    /// recorded channel of the account.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_007_AC1_AConfirmedTextCodeIsToldToEveryRecordedChannelAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);

        Accepted(await BegunAsync(subject, session, number));

        string code = Assert.Single(_notifications.Texts).Values["code"];

        _notifications.Sent.Clear();

        _ = Value(await Service.ConfirmTextCodeAsync(
            Authority(subject, session),
            code,
            Source,
            TestContext.Current.CancellationToken));

        Assert.NotEmpty(_notifications.Sent);
        Assert.All(_notifications.Sent, told => Assert.Equal(MessageKind.CredentialEnrolled, told.Message));
        Assert.Equal(
            (await _identifiers.HeldAsync(subject, TestContext.Current.CancellationToken)).NoticeSet.Count,
            _notifications.Sent.Count);
    }

    /// <summary>
    /// AUTH-FACT-002 AC9 and AUTH-FACT-001: a right code retires every other text-code
    /// credential of the account in the unit of work that makes the new one active, the
    /// report running against a suspended one with it, and the label is judged without
    /// them, so the label of the one retired is free.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AC9_ARightCodeRetiresEveryOtherTextCodeCredentialAndTheirLabelIsFreeAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);

        Authenticator active = Bound(subject, "My phone");
        Authenticator suspended = Bound(subject, "Old phone");

        suspended.Suspend(Noon + TimeSpan.FromDays(7));
        await _reports.AddAsync(
            LossReport.Open(suspended.Id, subject, OpaqueToken.Draw(_randomness), Noon, TimeSpan.FromDays(7)),
            TestContext.Current.CancellationToken);
        await PresentedAsync(subject, session);

        Accepted(await BegunAsync(subject, session, number));

        int committed = _work.OutermostCommitted;

        TextCodeEnrolment enrolled = Value(await Service.ConfirmTextCodeAsync(
            Authority(subject, session),
            Assert.Single(_notifications.Texts).Values["code"],
            Source,
            TestContext.Current.CancellationToken));

        Authenticator held = Assert.Single(
            await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken),
            credential => credential.Factor == TextedEnrolments.Entry);

        Assert.Equal((enrolled.Credential.Id, "My phone"), (held.Id, held.Label.Value));
        Assert.NotEqual(active.Id, held.Id);
        Assert.Null(await _reports.FindAsync(suspended.Id, TestContext.Current.CancellationToken));
        Assert.Contains((AuditActions.CredentialRemoved, subject, active.Id), _credentials.Records);
        Assert.Contains((AuditActions.CredentialRemoved, subject, suspended.Id), _credentials.Records);
        Assert.Equal(committed + 1, _work.OutermostCommitted);
    }

    /// <summary>
    /// AUTH-FACT-002 AC9 and AUTH-FACT-002b AC6: a begin whose number's signal answers
    /// a risk sends nothing, stages nothing, records the consideration and is refused
    /// <c>auth.factor.notpermitted</c>, whatever the identifier holds by then.
    /// </summary>
    /// <param name="gone">Whether the number has left the account by the time it would be locked.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUTH_FACT_002_AC9_ABeginWhoseSignalAnswersARiskSendsNothingAndIsNotPermittedAsync(bool gone)
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);

        _provider = new PhoneSignalProvider(async (_, cancellationToken) =>
        {
            if (gone)
            {
                await _identifiers.DiscardAsync(subject, number, cancellationToken);
            }

            return PhoneSignal.Risk;
        });

        Assert.Equal(ErrorCodes.FactorNotPermitted, Refused(await BegunAsync(subject, session, number)));

        Assert.Empty(_notifications.Texts);
        Assert.Empty(_enrolments.All);
        Assert.Empty(_identifiers.RowsLocked);
        Assert.Equal([(TextedEnrolments.Entry, (PhoneSignal?)PhoneSignal.Risk, (SubjectId?)subject)], _considered.Records);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-002 AC9: a begin whose number's signal did not answer a risk and
    /// whose identifier, once its row is locked in the unit of work, no longer holds,
    /// verified, the number read before sends nothing, stages nothing and is refused
    /// <c>identity.identifier.invalid</c>: the identifier removed, or its value
    /// replaced, while the begin waited for the lock.
    /// </summary>
    /// <param name="replaced">Whether the value was replaced rather than the identifier removed.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUTH_FACT_002_AC9_ABeginWhoseIdentifierNoLongerHoldsTheNumberUnderItsLockSendsNothingAsync(bool replaced)
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);

        _provider = new PhoneSignalProvider((_, _) => ValueTask.FromResult(PhoneSignal.Clear));
        _identifiers.LockingRow = async (holder, locked) =>
        {
            if (replaced)
            {
                await _identifiers.ReplaceAsync(
                    holder,
                    locked,
                    Elsewhere,
                    Elsewhere,
                    Noon,
                    Noon + TimeSpan.FromDays(7),
                    [1],
                    TestContext.Current.CancellationToken);
            }
            else
            {
                await _identifiers.DiscardAsync(holder, locked, TestContext.Current.CancellationToken);
            }
        };

        int rolledBack = _work.RolledBack;

        Assert.Equal(ErrorCodes.IdentifierInvalid, Refused(await BegunAsync(subject, session, number)));

        Assert.Equal([number], _identifiers.RowsLocked);
        Assert.Equal(0, _enrolments.Locks);
        Assert.Empty(_notifications.Texts);
        Assert.Empty(_enrolments.All);
        Assert.Empty(_restrictions.Carried);
        Assert.Equal(rolledBack + 1, _work.RolledBack);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-002 AC9 and chapter 09: a begin naming no verified phone identifier of
    /// the account (an address, an identifier of another account, one the account does
    /// not hold) is refused <c>identity.identifier.invalid</c> before any signal is
    /// asked or anything sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AC9_ABeginNamingNoVerifiedNumberOfTheAccountIsRefusedAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        _ = await TextableAsync(subject);

        SubjectId other = new(Guid.NewGuid());
        IdentifierId theirs = _identifiers.Verified(other, IdentifierKind.Phone, Elsewhere);
        IdentifierId address = (await _identifiers.HeldAsync(subject, TestContext.Current.CancellationToken)).All
            .First(identifier => identifier.Kind is IdentifierKind.Email).Id;
        int asked = 0;

        _provider = new PhoneSignalProvider((_, _) =>
        {
            asked++;

            return ValueTask.FromResult(PhoneSignal.Clear);
        });

        foreach (IdentifierId named in new[] { theirs, address, new IdentifierId(Guid.NewGuid()) })
        {
            Assert.Equal(ErrorCodes.IdentifierInvalid, Refused(await BegunAsync(subject, session, named)));
        }

        Assert.Equal(0, asked);
        Assert.Empty(_notifications.Texts);
        Assert.Empty(_enrolments.All);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-002: a new begin replaces a pending enrolment, whose code then
    /// confirms nothing: presented, it is a wrong code of the enrolment that stands.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_ANewBeginReplacesAPendingEnrolmentWhoseCodeThenConfirmsNothingAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);

        Accepted(await BegunAsync(subject, session, number));
        Accepted(await BegunAsync(subject, session, number, "Second"));

        string first = _notifications.Texts[0].Values["code"];
        string second = _notifications.Texts[1].Values["code"];

        Assert.Equal("Second", Assert.Single(_enrolments.All).Label.Value);

        if (!string.Equals(first, second, StringComparison.Ordinal))
        {
            Assert.Equal(ErrorCodes.CodeInvalid, await ConfirmedAsync(subject, session, first));
            Assert.Empty(await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken));
        }

        Assert.Equal(default, await ConfirmedAsync(subject, session, second));
        Assert.Equal(
            "Second",
            Assert.Single(await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken)).Label.Value);
    }

    /// <summary>
    /// CONV-DESIGN-003 (D-207): a text code's enrolment begun, which asks the carrier's
    /// signal of its number, inside a unit of work another opened is a fault of the
    /// calling code: it ends the level it joined, so nothing of the outer unit of work
    /// commits, nothing is staged and nothing is sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_ATextCodeBegunInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);

        _work.Reset();
        _ = await _work.BeginAsync(TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await BegunAsync(subject, session, number));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _work.CommitAsync(TestContext.Current.CancellationToken));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Empty(_enrolments.All);
        Assert.Empty(_notifications.Texts);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC8, AUTHZ-INHERIT-002: a confirm code, whose wrong try keeps its
    /// count, presented inside a unit of work another opened is a fault of the calling
    /// code: it ends the level it joined before it reads or writes anything else, so
    /// nothing of the outer unit of work commits and no try is counted.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC8_AConfirmCodeInsideAnOpenUnitOfWorkIsAFaultAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);

        Accepted(await BegunAsync(subject, session, number));

        string code = Assert.Single(_notifications.Texts).Values["code"];
        string wrong = code == "000000" ? "111111" : "000000";

        _work.Reset();
        _ = await _work.BeginAsync(TestContext.Current.CancellationToken);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ConfirmedAsync(subject, session, wrong));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _work.CommitAsync(TestContext.Current.CancellationToken));

        Assert.False(_work.Open);
        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(0, Assert.Single(_enrolments.All).WrongAttempts);
        Assert.Empty(await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-FACT-002 and AUTH-FACT-004: a wrong confirm code is refused
    /// <c>auth.code.invalid</c> and counts against the code's own
    /// <c>code.signin.attempts</c> and nothing else: the count is committed, no delay is
    /// counted and no credential record written, and once the tries are spent the
    /// enrolment is gone, so the right code answers <c>auth.code.expired</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AWrongConfirmCodeCountsAgainstItsOwnAttemptsAndNothingElseAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);

        _configuration.Set(Settings.CodeSigninAttempts, 2);

        Accepted(await BegunAsync(subject, session, number));

        string code = Assert.Single(_notifications.Texts).Values["code"];
        string wrong = code == "000000" ? "111111" : "000000";
        int committed = _work.OutermostCommitted;
        int recorded = _credentials.Records.Count;

        Assert.Equal(ErrorCodes.CodeInvalid, await ConfirmedAsync(subject, session, wrong));

        Assert.Equal(1, Assert.Single(_enrolments.All).WrongAttempts);
        Assert.Equal(committed + 1, _work.OutermostCommitted);
        Assert.Empty(_throttle.Failures);
        Assert.Equal(recorded, _credentials.Records.Count);
        Assert.Empty(_restrictions.Answered);

        Assert.Equal(ErrorCodes.CodeInvalid, await ConfirmedAsync(subject, session, wrong));

        Assert.Empty(_enrolments.All);
        Assert.Equal(ErrorCodes.CodeExpired, await ConfirmedAsync(subject, session, code));
        Assert.Empty(await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// Chapter 09 and AUTH-FACT-004 AC3: a confirm with no enrolment pending, or with
    /// one past its lifetime, is refused <c>auth.code.expired</c>, the right code
    /// included, and writes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AConfirmWithNoEnrolmentPendingOrOnePastItsLifetimeIsExpiredAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);

        Assert.Equal(ErrorCodes.CodeExpired, await ConfirmedAsync(subject, session, "123456"));

        Accepted(await BegunAsync(subject, session, number));

        _clock.Advance(TimeSpan.FromMinutes(10));

        int committed = _work.OutermostCommitted;

        Assert.Equal(
            ErrorCodes.CodeExpired,
            await ConfirmedAsync(subject, session, Assert.Single(_notifications.Texts).Values["code"]));
        Assert.Equal(0, Assert.Single(_enrolments.All).WrongAttempts);
        Assert.Equal(committed, _work.OutermostCommitted);
        Assert.Empty(await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken));
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-002 AC10: a confirm whose number left the account while it waited for
    /// the lock on the identifier's row is answered <c>auth.code.expired</c>, the right
    /// code included, and enrols nothing; the row is locked before the enrolment and
    /// the credentials are.
    /// </summary>
    /// <param name="replaced">Whether the value was replaced rather than the identifier removed.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUTH_FACT_002_AC10_AConfirmWhoseNumberLeftBeforeItsLockIsExpiredAndEnrolsNothingAsync(bool replaced)
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);

        Accepted(await BegunAsync(subject, session, number));

        int locks = _enrolments.Locks;

        _identifiers.LockingRow = async (holder, locked) =>
        {
            if (replaced)
            {
                await _identifiers.ReplaceAsync(
                    holder,
                    locked,
                    Elsewhere,
                    Elsewhere,
                    Noon,
                    Noon + TimeSpan.FromDays(7),
                    [1],
                    TestContext.Current.CancellationToken);
            }
            else
            {
                await _identifiers.DiscardAsync(holder, locked, TestContext.Current.CancellationToken);
            }
        };

        Assert.Equal(
            ErrorCodes.CodeExpired,
            await ConfirmedAsync(subject, session, Assert.Single(_notifications.Texts).Values["code"]));

        Assert.Equal(locks, _enrolments.Locks);
        Assert.Empty(await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken));
        Assert.Empty(_restrictions.Answered);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-FACT-002, CONV-DESIGN-003: a begin and a confirm each lock the identifier's
    /// row before the pending enrolment's, and a confirm locks the credentials after
    /// both.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_ABeginAndAConfirmLockTheIdentifiersRowBeforeTheEnrolmentAndTheCredentialsAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);
        var order = new List<string>();

        _ = Bound(subject, "Old phone");
        await PresentedAsync(subject, session);

        _identifiers.LockingRow = (_, _) =>
        {
            order.Add("identifier");

            return ValueTask.CompletedTask;
        };
        _enrolments.Locking = _ =>
        {
            order.Add("enrolment");

            return ValueTask.CompletedTask;
        };
        _authenticators.Locking = _ => order.Add("credential");

        Accepted(await BegunAsync(subject, session, number));

        Assert.Equal(["identifier", "enrolment"], order);

        order.Clear();

        Assert.Equal(
            default,
            await ConfirmedAsync(subject, session, Assert.Single(_notifications.Texts).Values["code"]));
        Assert.Equal(["identifier", "enrolment", "credential"], order);
    }

    /// <summary>
    /// AUTH-FACT-002 AC1 and chapter 09: <c>phoneCode</c> is off by default in
    /// <c>loginFactors</c>, and where the policy in force does not permit it neither a
    /// begin nor a confirm is: each is refused <c>auth.factor.notpermitted</c> and
    /// nothing is sent or enrolled.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_002_AC1_ATextCodeIsNotEnrolledWhereThePolicyDoesNotPermitItAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);

        Accepted(await BegunAsync(subject, session, number));

        string code = Assert.Single(_notifications.Texts).Values["code"];

        _configuration.Set(Settings.PolicyDefault, Core.Policies.SystemDefault);

        Assert.DoesNotContain(TextedEnrolments.Entry, Core.Policies.SystemDefault.LoginFactors);
        Assert.Equal(ErrorCodes.FactorNotPermitted, Refused(await BegunAsync(subject, session, number)));
        Assert.Equal(ErrorCodes.FactorNotPermitted, await ConfirmedAsync(subject, session, code));
        _ = Assert.Single(_notifications.Texts);
        Assert.Empty(await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Chapter 09 and AUTH-STEP-007: the enrolment of a text code is gated at
    /// <c>factor:enrol</c>, refused to a restricted account, to an account with no
    /// password, to the enrolment session an approved recovery opened and to the
    /// registration session's security step, and a label that is none is refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_007_ATextCodesEnrolmentIsGatedAndRefusedAsItsRouteStatesAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(
            ErrorCodes.CredentialLabelInvalid,
            Refused(await BegunAsync(subject, session, number, new string('a', 65))));

        var recovering = CredentialAuthority.Of((await OpenedAsync(subject)).Id);

        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Service.BeginTextCodeAsync(recovering, number, "My phone", Source, Language, cancellationToken)));
        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Service.ConfirmTextCodeAsync(recovering, "123456", Source, cancellationToken)));

        var registering = CredentialAuthority.Of(new RegistrationSessionId(Guid.NewGuid()));

        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Service.BeginTextCodeAsync(registering, number, "My phone", Source, Language, cancellationToken)));
        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Service.ConfirmTextCodeAsync(registering, "123456", Source, cancellationToken)));

        _restriction.Restrict(subject);

        Assert.Equal(ErrorCodes.Restricted, Refused(await BegunAsync(subject, session, number)));
        Assert.Equal(ErrorCodes.Restricted, await ConfirmedAsync(subject, session, "123456"));

        _restriction.Lift(subject);

        // A second step held and not presented under the session: the gate asks for it.
        _ = await ConfirmedAsync(subject, session);

        Assert.Equal(ErrorCodes.StepUpRequired, Refused(await BegunAsync(subject, session, number)));
        Assert.Equal(ErrorCodes.StepUpRequired, await ConfirmedAsync(subject, session, "123456"));

        (SubjectId passwordless, SessionId other) = await SignedInAsync(password: false);
        IdentifierId theirs = (await _identifiers.HeldAsync(passwordless, cancellationToken)).All
            .First(identifier => identifier.Kind is IdentifierKind.Phone).Id;

        Assert.Equal(ErrorCodes.FactorPasswordRequired, Refused(await BegunAsync(passwordless, other, theirs)));
        Assert.Equal(ErrorCodes.FactorPasswordRequired, await ConfirmedAsync(passwordless, other, "123456"));

        Assert.DoesNotContain(_notifications.Texts, texted => texted.Message is MessageKind.SecondStepCode);
        Assert.Empty(_enrolments.All);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC3: a restriction committed after the gate step of a begin or a
    /// confirm refuses it inside its unit of work, before anything is sent or enrolled.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ATextCodesEnrolmentIsRefusedByARestrictionCommittedSinceItsGateStepAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);

        await RestrictedSinceTheGateStepAsync(
            subject,
            async () => Refused(await BegunAsync(subject, session, number)));

        Assert.Empty(_notifications.Texts);

        Accepted(await BegunAsync(subject, session, number));

        string code = Assert.Single(_notifications.Texts).Values["code"];

        await RestrictedSinceTheGateStepAsync(subject, async () => await ConfirmedAsync(subject, session, code));

        Assert.Empty(await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Chapter 09 and AUTH-ABUSE-004: where a restriction refuses the text, the begin
    /// is refused <c>auth.restriction.exceeded</c> and stages nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_ABeginWhoseTextARestrictionRefusesStagesNothingAsync()
    {
        (SubjectId subject, SessionId session) = await SignedInAsync();
        IdentifierId number = await TextableAsync(subject);

        _notifications.Refusal = Error.From(ErrorCodes.RestrictionExceeded);

        Assert.Equal(ErrorCodes.RestrictionExceeded, Refused(await BegunAsync(subject, session, number)));

        Assert.Empty(_enrolments.All);
        Assert.Empty(_restrictions.Carried);
        Assert.False(_work.Open);
    }

    // AUTHZ-GATE-006 AC3: the change made with the account restricted in the moment
    // before the next unit of work begins, which is after the change's gate step. It is
    // refused as the gate refuses, the unit of work it began is rolled back and none is
    // left open; the restriction is lifted once the change has answered.
    private async ValueTask RestrictedSinceTheGateStepAsync(SubjectId subject, Func<ValueTask<ErrorCode>> change)
    {
        int rolledBack = _work.RolledBack;

        _restriction.Admitted = admitted => _work.Meanwhile = () => _restriction.Restrict(admitted);

        ErrorCode refused = await change();

        _restriction.Lift(subject);

        Assert.Equal(ErrorCodes.Restricted, refused);
        Assert.Equal(rolledBack + 1, _work.RolledBack);
        Assert.False(_work.Open);
    }

    // AUTH-FACT-002: the policy in force permits the text code, and the account's
    // verified number is the one an enrolment names.
    private async ValueTask<IdentifierId> TextableAsync(SubjectId subject)
    {
        _configuration.Set(
            Settings.PolicyDefault,
            Core.Policies.SystemDefault with
            {
                LoginFactors = new HashSet<Factor>(
                    [.. Core.Policies.SystemDefault.LoginFactors, TextedEnrolments.Entry]),
            });

        return (await _identifiers.HeldAsync(subject, TestContext.Current.CancellationToken)).All
            .First(identifier => identifier.Kind is IdentifierKind.Phone).Id;
    }

    private ValueTask<Result> BegunAsync(
        SubjectId subject,
        SessionId session,
        IdentifierId number,
        string label = "My phone") =>
        Service.BeginTextCodeAsync(
            Authority(subject, session),
            number,
            label,
            Source,
            Language,
            TestContext.Current.CancellationToken);

    // What a confirm of a text code's enrolment was refused with, or nothing where it
    // enrolled the credential.
    private async ValueTask<ErrorCode> ConfirmedAsync(SubjectId subject, SessionId session, string code) =>
        Refused(await Service.ConfirmTextCodeAsync(
            Authority(subject, session),
            code,
            Source,
            TestContext.Current.CancellationToken));

    // A text code's credential the account already holds, bound to its number.
    private Authenticator Bound(SubjectId subject, string label)
    {
        var bound = Authenticator.Bound(
            AuthenticatorId.New(_clock),
            subject,
            TextedEnrolments.Entry,
            Label(label),
            Number,
            _clock.GetUtcNow());

        _authenticators.Hold(bound);

        return bound;
    }

    // AUTH-FACT-002b: the signal of the number an enrolment names, asked before its
    // unit of work, as the deployment registered it.
    private PhoneSignals Signals => new(_provider, _considered, _work, _clock);

    private TextedEnrolments Texted =>
        new(
            _enrolments,
            _authenticators,
            _reports,
            _identifiers,
            Signals,
            _notifications,
            _restrictions,
            _configuration,
            _clock,
            _randomness);

    private CredentialService Service =>
        new(
            Keys,
            _accounts,
            _restriction,
            Totp,
            Texted,
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
            _notifications,
            _credentials,
            _outbox ?? _events,
            _configuration,
            Registration,
            _work,
            _clock);

    // The registration session reaches the four enrolment operations through the same
    // service (REG-SESS-006), so it is built beside it on the same stores.
    private RegistrationService Registration =>
        new(
            new RegistrationSessionStoreInMemory(),
            new RegistrationDirectoryInMemory(),
            _notifications,
            Landing.Links,
            _notices,
            Passwords,
            _passwords,
            Codes,
            _sets,
            _authenticators,
            Keys,
            Totp,
            new OidcClientStoreInMemory(),
            Policies,
            new InvitationStoreInMemory(),
            new InvitationOpening(new InvitationStoreInMemory(), _work, _clock),
            new DomainLock(_memberships, _configuration, new DomainStoreInMemory()),
            Sessions,
            new DeviceService(new DeviceStoreInMemory(), _configuration, _work, _events, _clock, _randomness),
            Throttle,
            new VerificationCodes(new VerificationCodeStoreInMemory(), _configuration, _work, _clock, _randomness),
            _restrictions,
            new BotDefence(
                _configuration,
                new DatacenterRangesInMemory(),
                new RegistrationSourcesInMemory(),
                new BotDefenceAuditInMemory(),
                _work,
                verifier: null,
                _clock),
            new ConsentsInMemory(),
            new LegalDocumentsInMemory(),
            _configuration,
            _work,
            _events,
            _clock,
            _randomness,
            new RecordOffers(new RecordOfferStoreInMemory(), new RecordDirectoryInMemory(), _configuration, _clock));

    private WebAuthnService Keys =>
        new(_authenticators, _passwords, _credentials, _configuration, _work, _clock, _randomness);

    private TotpService Totp =>
        new(_authenticators, _passwords, _configuration, _work, _clock, _randomness);

    private RecoveryCodeService Codes =>
        new(_sets, new Argon2idHasher(_randomness), _configuration, _work, _clock, _randomness);

    private StepUpGuard Guard =>
        new(_live, _authenticators, _passwords, Policies, _identifiers, new PhoneSignals(null, new PhoneSignalAuditInMemory(), _work, _clock), _clock);

    private PolicyResolution Policies => new(_memberships, _configuration, _raises);

    private SessionService Sessions =>
        new(
            _live,
            _audit,
            _authenticators,
            _credentials,
            Policies,
            _configuration,
            new AdministrativeScope(_gate, _administrative),
            Guard,
            _accounts,
            _locations,
            new ConcurrentSessions(_live, _configuration, _events),
            new OidcClientStoreInMemory(),
            _events,
            _work,
            _clock,
            _randomness);

    private ThrottleService Throttle =>
        new(_configuration, _throttle, _work, _events, _clock);

    private LossReports Losses =>
        new(
            _reports,
            _accounts,
            _authenticators,
            _passwords,
            _sets,
            _identifiers,
            Policies,
            _notifications,
            Landing.Links,
            _credentials,
            _events,
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
            Sessions,
            Guard,
            new AdministrativeScope(_gate, _administrative),
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
            new PhoneSignals(null, new PhoneSignalAuditInMemory(), _work, _clock),
            Throttle,
            _events,
            _configuration,
            _work,
            _clock,
            _randomness);

    private PasswordService Passwords =>
        new(
            _passwords,
            new PasswordScreening(_corpus, _words, _configuration, _screening, _events, _clock),
            new Argon2idHasher(_randomness),
            _events,
            _configuration,
            _work,
            _clock);

    private static CredentialAuthority Authority(SubjectId subject, SessionId session) =>
        CredentialAuthority.Of(AccessContext.Of(subject), session);

    private static bool Succeeded(Result result) => result.Match(() => true, _ => false);

    private static ErrorCode Refused<TValue>(Result<TValue> result) =>
        result.Match(_ => default, error => error.Code);

    private static ErrorCode Refused(Result result) =>
        result.Match(() => default, error => error.Code);

    private static TValue Value<TValue>(Result<TValue> result) =>
        result.Match(value => value, error => throw new Xunit.Sdk.XunitException(error.Code.ToString()));

    private static Error Failure(Result result)
    {
        Error? refused = null;

        result.Switch(
            () => throw new InvalidOperationException("The removal was accepted."),
            error => refused = error);

        return refused!;
    }

    private static void Accepted(Result result) =>
        result.Switch(
            () => { },
            error => throw new InvalidOperationException("It was refused: " + error.Code));

    // What a browser sends back from a creation ceremony: the challenge the server
    // issued, an origin the relying party admits, and a key the runtime can read.
    private static AuthenticatorAttestation Attestation(string challenge, bool synced)
        // User present and user verified, and where the credential is synced the two
        // backup flags AUTH-FACT-013 reads.
        => SoftwareAuthenticator.Holding().Attested(challenge, Origin, RelyingParty, synced ? (byte)0x1D : (byte)0x05);

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
            "This laptop",
            Source,
            TestContext.Current.CancellationToken));
    }

    private Authenticator Held(AuthenticatorId credential) =>
        _authenticators.All.Single(held => held.Id == credential);

    // What a sign-in commits to a credential's row: the row as it stands, presented at
    // the instant given.
    private void SignedIn(AuthenticatorId credential, DateTimeOffset at)
    {
        Authenticator stood = Held(credential);

        _authenticators.Hold(Authenticator.Existing(
            stood.Id,
            stood.Subject,
            stood.Factor,
            stood.Label,
            stood.State,
            stood.AddedAt,
            at,
            stood.InvalidatesAt,
            stood.Confirmed,
            stood.Totp,
            stood.WebAuthn,
            stood.IsPreferred,
            stood.Number));
    }

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

    private async ValueTask<EnrolledCredential> ConfirmedAsync(SubjectId subject, SessionId session) =>
        await ConfirmedAsync(Authority(subject, session));

    private async ValueTask<EnrolledCredential> ConfirmedAsync(CredentialAuthority authority)
    {
        GeneratorEnrolment begun = Value(await Service.BeginGeneratorAsync(
            authority,
            "Phone",
            TestContext.Current.CancellationToken));

        return Value(await Service.ConfirmGeneratorAsync(
            authority,
            begun.Credential,
            Code(begun.Credential),
            Source,
            TestContext.Current.CancellationToken));
    }

    private async ValueTask<EnrolledCredential> PasskeyAsync(CredentialAuthority authority)
    {
        CredentialCeremony ceremony = Value(await Service.BeginKeyAsync(
            authority,
            Factor.Passkey,
            TestContext.Current.CancellationToken));

        return Value(await Service.CompleteKeyAsync(
            authority,
            Attestation(ceremony.Challenge, synced: true),
            "This laptop",
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

    // Every credential operation chapter 09 does not list for the enrolment session
    // (POST /enrol/begin), asked under one authority, and what each answered.
    private async ValueTask<ErrorCode[]> UnreachedAsync(
        CredentialAuthority authority,
        AuthenticatorId credential,
        CancellationToken cancellationToken) =>
        [
            Refused(await Service.RemoveAsync(authority, credential, Source, cancellationToken)),
            Refused(await Service.UpgradeKeyAsync(authority, credential, cancellationToken)),
            Refused(await Service.GenerateRecoveryCodesAsync(authority, cancellationToken)),
            Refused(await Service.LinkableAsync(authority, Factor.Google, cancellationToken)),
            Refused(await Service.LinkAsync(
                authority,
                Factor.Google,
                "provider-subject",
                Label("Google"),
                Source,
                cancellationToken)),
            Refused(await Service.UnlinkAsync(authority, Factor.Google, Source, cancellationToken)),
        ];

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
                actor: null,
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
                    Counter: 0,
                    Transports: []),
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
