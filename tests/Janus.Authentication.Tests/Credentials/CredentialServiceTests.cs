using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Credentials;
using Janus.Authentication.Events;
using Janus.Authentication.Factors;
using Janus.Authentication.Invitations;
using Janus.Authentication.Organizations;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
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
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private IEvents? _outbox;

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
    public async Task AUTH_STEP_007_ASetPasswordIsAnnouncedAsync()
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

        // CONV-DESIGN-003 AC5: the refused removal ends the unit of work it was decided
        // in with nothing committed, and the window's report then commits on its own: the
        // suspension, the notices it undertakes, and what became of them.
        Assert.False(_work.Open);
        Assert.Equal(1, _work.RolledBack);
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
            ErrorCodes.EnrolmentTokenInvalid,
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
        var context = AccessContext.Of(SubjectId.New(_randomness), subject);
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
        var context = AccessContext.Of(SubjectId.New(_randomness), subject);

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

    private CredentialService Service =>
        new(
            Keys,
            _accounts,
            _restriction,
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
            new ConsentsInMemory(),
            _configuration,
            _work,
            _events,
            _clock,
            _randomness);

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
            "This laptop",
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
