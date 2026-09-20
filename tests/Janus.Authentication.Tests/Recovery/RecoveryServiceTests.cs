using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
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
using Janus.Authentication.Tests.Sending;
using Janus.Authentication.Tests.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Recovery;

/// <summary>
/// Getting back into an account: the link a person asks for, the password it sets,
/// and the re-enrolment an approver opens after confirming the person somewhere the
/// account already reaches (AUTH-RECOV-002 to AUTH-RECOV-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class RecoveryServiceTests : IAsyncDisposable
{
    private const string Language = "en";
    private const string Source = "198.51.100.7";
    private const string Address = "person@example.test";
    private const string Elsewhere = "nobody@example.test";
    private const string Number = "+441632960011";
    private const string Secret = "orangemarmaladeandtoast";
    private const string Reason = "Passport checked on the recorded number.";

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly OrganizationId Support = new(Guid.NewGuid());

    private static readonly SessionOrigin Somewhere = new(
        Source,
        new DeviceDescription("Firefox", "Fedora"),
        new SessionLocation("Alexandria", "EG"));

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
    /// A deployment that can send, whose templates carry the token the message is
    /// read back from in these tests.
    /// </summary>
    public RecoveryServiceTests()
    {
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);

        MessageKind[] messages =
        [
            MessageKind.RecoveryLink,
            MessageKind.EnrolmentLink,
            MessageKind.SecurityNotice,
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
                    new MessageTemplate(kind is SendKind.Email ? "subject" : null, "{token}"));
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
    /// AUTH-RECOV-005 AC2: an account holding no password sets one through recovery,
    /// and the passkey it holds is still there afterwards.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_005_AC2_RecoverySetsAPasswordAndRemovesNoFactorAsync()
    {
        SubjectId subject = await AccountAsync(password: false);
        AuthenticatorId passkey = Enrolled(subject, Factor.Passkey);

        Assert.True(Succeeded(await Service.BeginAsync(
            Address,
            Language,
            Source,
            TestContext.Current.CancellationToken)));

        Assert.True(Succeeded(await Service.CompleteAsync(
            Sent(),
            Secret,
            Source,
            TestContext.Current.CancellationToken)));

        Assert.NotNull(await _passwords.FindAsync(subject, TestContext.Current.CancellationToken));
        Assert.Equal(
            AuthenticatorState.Active,
            (await _authenticators.FindAsync(passkey, TestContext.Current.CancellationToken))!.State);
    }

    /// <summary>
    /// AUTH-RECOV-005 AC3: the password recovery set is enough on its own to report the
    /// passkey that was lost, which is the whole point of having set it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_005_AC3_TheRecoveredPasswordReportsTheLostPasskeyAsync()
    {
        SubjectId subject = await AccountAsync(password: false);
        AuthenticatorId passkey = Enrolled(subject, Factor.Passkey);

        _ = await Service.BeginAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        Assert.True(Succeeded(await Service.CompleteAsync(
            Sent(),
            Secret,
            Source,
            TestContext.Current.CancellationToken)));

        IssuedSession recovered = Value(await Sessions.BeginAsync(
            subject,
            [Factor.Password],
            Somewhere,
            TestContext.Current.CancellationToken))!;

        Assert.NotNull(recovered);
        Assert.NotNull(Value(await Losses.ReportAsync(
            AccessContext.Of(subject),
            passkey,
            Source,
            TestContext.Current.CancellationToken)));
        Assert.Equal(
            AuthenticatorState.Suspended,
            (await _authenticators.FindAsync(passkey, TestContext.Current.CancellationToken))!.State);
    }

    /// <summary>
    /// AUTH-STEP-007 AC3: setting a password through recovery changes the password and
    /// nothing else, so every other credential stands exactly as it did.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_STEP_007_AC3_TheRecoveredPasswordLeavesEveryOtherCredentialAsItWasAsync()
    {
        SubjectId subject = await AccountAsync(password: false);
        AuthenticatorId passkey = Enrolled(subject, Factor.Passkey);
        AuthenticatorId generator = Enrolled(subject, Factor.Totp);

        _ = await Service.BeginAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        Assert.True(Succeeded(await Service.CompleteAsync(
            Sent(),
            Secret,
            Source,
            TestContext.Current.CancellationToken)));

        foreach (AuthenticatorId held in new[] { passkey, generator })
        {
            Assert.Equal(
                AuthenticatorState.Active,
                (await _authenticators.FindAsync(held, TestContext.Current.CancellationToken))!.State);
        }
    }

    /// <summary>
    /// AUTH-RECOV-005 AC1: what recovery sets is a password, so an account whose policy
    /// asks for two factors still has to present the second one to sign in.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_005_AC1_TheSecondFactorIsStillRequiredAfterRecoveryAsync()
    {
        SubjectId subject = await AccountAsync(password: false);
        _ = Enrolled(subject, Factor.Totp);

        _configuration.Set(
            Settings.PolicyDefault,
            Janus.Core.Policies.SystemDefault with { RequiredAssurance = AssuranceLevel.Aal2 });

        _ = await Service.BeginAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        Assert.True(Succeeded(await Service.CompleteAsync(
            Sent(),
            Secret,
            Source,
            TestContext.Current.CancellationToken)));

        Assert.Equal(
            ErrorCodes.FactorRequired,
            Refused(await Sessions.BeginAsync(
                subject,
                [Factor.Password],
                Somewhere,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// D-140: recovery is the way back for an account its own holder deactivated, and
    /// completing it stands the account up again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_013_ACompletedRecoveryStandsASelfSuspendedAccountUpAsync()
    {
        SubjectId subject = await AccountAsync();

        _accounts.Suspended(subject, SuspensionOrigin.Self);

        _ = await Service.BeginAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        Assert.True(Succeeded(await Service.CompleteAsync(
            Sent(),
            Secret,
            Source,
            TestContext.Current.CancellationToken)));

        Assert.Equal(
            AccountState.Active,
            await _accounts.StateAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-RECOV-004 AC1: an account whose policy closes self-service recovery is
    /// offered no email or text route, and the caller cannot tell that from an
    /// identifier no account holds.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_004_AC1_APolicyThatClosesRecoverySendsNothingAsync()
    {
        SubjectId subject = await AccountAsync();

        _memberships.Place(subject, Support);
        _configuration.Set(
            Settings.OrganizationPolicy,
            Support.ToString(),
            new PolicyOverride(null, null, null, null, SelfServiceRecovery: false, null));

        Assert.True(Succeeded(await Service.BeginAsync(
            Address,
            Language,
            Source,
            TestContext.Current.CancellationToken)));

        Assert.Empty(_mail.Taken);
    }

    /// <summary>
    /// AUTH-RECOV-002 AC1: the link is single-use and time-boxed. Spending it once
    /// works, spending it twice does not, and one left until it lapses does not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_AC1_TheLinkExpiresAndIsNotReusedAsync()
    {
        _ = await AccountAsync();
        _ = await Service.BeginAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        string token = Sent();

        Assert.True(Succeeded(await Service.CompleteAsync(
            token,
            Secret,
            Source,
            TestContext.Current.CancellationToken)));

        Assert.Equal(
            ErrorCodes.RecoveryTokenInvalid,
            Refused(await Service.CompleteAsync(
                token,
                Secret,
                Source,
                TestContext.Current.CancellationToken)));

        _mail.Taken.Clear();
        _clock.Advance(TimeSpan.FromMinutes(5));

        _ = await Service.BeginAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        string second = Sent();

        _clock.Advance(TimeSpan.FromHours(2));

        Assert.Equal(
            ErrorCodes.RecoveryTokenExpired,
            Refused(await Service.CompleteAsync(
                second,
                Secret,
                Source,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-RECOV-002 AC2: an approval with no written reason is refused, and one with
    /// a reason records it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_AC2_TheReasonIsMandatoryAndRecordedAsync()
    {
        SubjectId subject = await AccountAsync();
        (SubjectId approver, SessionId session) = await ApproverAsync();

        Assert.Equal(
            ErrorCodes.RecoveryReasonRequired,
            Refused(await Approving(approver, session, subject, "   ")));

        Assert.NotNull(Value(await Approving(approver, session, subject, Reason)));
        Assert.Equal(Reason, _recorded.Written[^1].Reason);
        Assert.Equal(subject, _recorded.Written[^1].Subject);
        Assert.Equal(approver, _recorded.Written[^1].Approver);
    }

    /// <summary>
    /// AUTH-RECOV-002 AC3: the number of approvers is a setting, so where two are
    /// required one approval sends no link and the second sends it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_AC3_TheApproverCountIsConfigurableAsync()
    {
        SubjectId subject = await AccountAsync();
        (SubjectId first, SessionId opened) = await ApproverAsync();
        (SubjectId second, SessionId another) = await ApproverAsync();

        _configuration.Set(Settings.RecoveryApproversRequired, 2);

        Assert.Null(Value(await Approving(first, opened, subject, Reason))!.EnrolmentLinkExpiresAt);
        Assert.Empty(_mail.Taken);

        Assert.NotNull(Value(await Approving(second, another, subject, Reason))!.EnrolmentLinkExpiresAt);
        Assert.NotEmpty(_mail.Taken);
    }

    /// <summary>
    /// AUTH-RECOV-002 AC4: an approver giving more approvals than the threshold admits
    /// raises the alert that says so, with nobody watching.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_AC4_AnUnusualApprovalFrequencyIsSurfacedAsync()
    {
        (SubjectId approver, SessionId session) = await ApproverAsync();

        _configuration.Set(Settings.AlertingRecoveryApproverThreshold, 2);

        for (int given = 0; given < 2; given++)
        {
            string address = Named(given);

            _ = await Approving(approver, session, await AccountAsync(address), Reason, address);
        }

        Assert.Contains(
            _events.Published.OfType<AlertRaised>(),
            raised => raised.Condition is AlertCondition.ApproverVolume);
    }

    /// <summary>
    /// AUTH-RECOV-002 AC6 and AUTH-RECOV-003 AC1: a channel the requester supplies is
    /// refused, and only one the account already holds is accepted.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_003_AC1_AChannelTheRequesterSuppliedIsRefusedAsync()
    {
        SubjectId subject = await AccountAsync();
        (SubjectId approver, SessionId session) = await ApproverAsync();

        Assert.Equal(
            ErrorCodes.RecoveryChannelNotOnAccount,
            Refused(await Approving(approver, session, subject, Reason, Elsewhere)));

        Assert.Empty(_mail.Taken);
        Assert.NotNull(Value(await Approving(approver, session, subject, Reason, Address)));
        Assert.Equal(Address, _mail.Taken[0].Destination.Value);
    }

    /// <summary>
    /// AUTH-RECOV-003 AC2: which channel carried the confirmation is recorded with the
    /// approval.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_003_AC2_TheChannelUsedIsRecordedAsync()
    {
        SubjectId subject = await AccountAsync();
        (SubjectId approver, SessionId session) = await ApproverAsync();

        _ = await Approving(approver, session, subject, Reason, Number);

        Assert.Equal(IdentifierKind.Phone, _recorded.Written[^1].Channel);
    }

    /// <summary>
    /// AUTH-RECOV-002a AC1 and AC2: an approver's own subject is refused, and the
    /// refusal is the service's own and not an endpoint's.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002a_AC1_AnApproverCannotApproveTheirOwnRecoveryAsync()
    {
        (SubjectId approver, SessionId session) = await ApproverAsync();

        _ = _identifiers.Verified(approver, IdentifierKind.Email, Address);

        Assert.Equal(
            ErrorCodes.RecoverySelfApproval,
            Refused(await Approving(approver, session, approver, Reason)));
    }

    /// <summary>
    /// AUTH-RECOV-002a AC3: a break-glass session approves the sole administrator's
    /// recovery, which is what it exists for.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002a_AC3_ABreakGlassSessionApprovesTheAdministratorAsync()
    {
        SubjectId administrator = await AccountAsync();
        (SubjectId emergency, SessionId session) = await ApproverAsync();

        _memberships.Place(administrator, Support);

        Assert.NotNull(Value(await Approving(emergency, session, administrator, Reason)));
    }

    /// <summary>
    /// D-147 and D-148: the enrolment link opens the enrolment session and is the only
    /// thing that does; the recovery link does not, and neither opens twice.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_TheEnrolmentLinkIsTheOnlyOneThatOpensASessionAsync()
    {
        SubjectId subject = await AccountAsync();
        (SubjectId approver, SessionId session) = await ApproverAsync();

        _ = await Service.BeginAsync(Address, Language, Source, TestContext.Current.CancellationToken);

        string recovery = Sent();

        Assert.Equal(
            ErrorCodes.EnrolmentTokenInvalid,
            Refused(await Service.BeginEnrolmentAsync(
                recovery,
                TestContext.Current.CancellationToken)));

        _mail.Taken.Clear();
        _clock.Advance(TimeSpan.FromMinutes(5));

        _ = await Approving(approver, session, subject, Reason);

        string enrolment = Sent();

        EnrolmentSession? opened = Value(await Service.BeginEnrolmentAsync(
            enrolment,
            TestContext.Current.CancellationToken));

        Assert.NotNull(opened);
        Assert.Equal(subject, opened.Subject);

        Assert.Equal(
            ErrorCodes.EnrolmentTokenInvalid,
            Refused(await Service.BeginEnrolmentAsync(
                enrolment,
                TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// AUTH-RECOV-002 and D-111: an approver who reached the person somewhere other
    /// than the mailbox opens a session that may settle a new address on the new
    /// address alone, and the rest of the notice set hears of the approval.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_AC5_AnUnreachableMailboxOpensASessionThatMayReplaceItAsync()
    {
        SubjectId subject = await AccountAsync();
        (SubjectId approver, SessionId session) = await ApproverAsync();

        _ = await Approving(approver, session, subject, Reason, Number);

        EnrolmentSession? opened = Value(await Service.BeginEnrolmentAsync(
            Texted(),
            TestContext.Current.CancellationToken));

        Assert.NotNull(opened);
        Assert.True(opened.MailboxLost);
        Assert.Contains(_mail.Taken, sent => string.Equals(sent.Destination.Value, Address, StringComparison.Ordinal));
    }

    /// <summary>
    /// AUTH-RECOV-002 AC6: the link goes to what the account records and never to what
    /// the request carried, so a channel the account does not hold reaches nobody and
    /// one it does is written to in the form the account holds it in.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002_AC6_TheLinkGoesToWhatTheAccountRecordsAsync()
    {
        SubjectId subject = await AccountAsync();
        (SubjectId approver, SessionId session) = await ApproverAsync();

        Assert.Equal(
            ErrorCodes.RecoveryChannelNotOnAccount,
            Refused(await Approving(approver, session, subject, Reason, Elsewhere)));
        Assert.DoesNotContain(
            _mail.Taken,
            sent => string.Equals(sent.Destination.Value, Elsewhere, StringComparison.Ordinal));

        _ = await Approving(approver, session, subject, Reason, "  PERSON@Example.TEST ");

        Assert.Contains(
            _mail.Taken,
            sent => string.Equals(sent.Destination.Value, Address, StringComparison.Ordinal));
    }

    /// <summary>
    /// INT-SMS-001 AC3: the enrolment link reaches the number only behind an approval
    /// that carries the confirmation and the written reason; without the reason nothing
    /// is sent and nothing is recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_SMS_001_AC3_TheEnrolmentLinkGoesByTextOnlyBehindAnApprovalAsync()
    {
        SubjectId subject = await AccountAsync();
        (SubjectId approver, SessionId session) = await ApproverAsync();

        Assert.Equal(
            ErrorCodes.RecoveryReasonRequired,
            Refused(await Approving(approver, session, subject, "  ", Number)));
        Assert.Empty(_sms.Taken);
        Assert.Empty(_approvals.All);

        _ = await Approving(approver, session, subject, Reason, Number);

        Assert.NotEmpty(_sms.Taken);
        Assert.Equal(IdentifierKind.Phone, Assert.Single(_recorded.Written).Channel);
        Assert.Equal(Reason, _recorded.Written[^1].Reason);
    }

    /// <summary>
    /// AUTH-RECOV-002a AC2: an approval an interface could find nothing wrong with,
    /// carrying a live session that passed the gate, a recorded channel and a written
    /// reason, is still refused because the approver is the subject.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_002a_AC2_TheRefusalIsTheDomainsAndNotTheInterfacesAsync()
    {
        (SubjectId approver, SessionId session) = await ApproverAsync();

        _ = _identifiers.Verified(approver, IdentifierKind.Email, Address);

        Assert.Equal(
            ErrorCodes.RecoverySelfApproval,
            Refused(await Approving(approver, session, approver, Reason)));
        Assert.Empty(_approvals.All);
    }

    /// <summary>
    /// AUTH-ABUSE-003: an address no account holds is told there is no account, and
    /// the caller is told what an address an account holds is told.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_ABUSE_003_AnAddressNoAccountHoldsIsToldSoAsync()
    {
        _ = await AccountAsync();

        Assert.True(Succeeded(await Service.BeginAsync(
            Elsewhere,
            Language,
            Source,
            TestContext.Current.CancellationToken)));

        Assert.Equal(Elsewhere, _mail.Taken[0].Destination.Value);
    }

    private RecoveryService Service =>
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
            new StepUpGuard(_live, _authenticators, _passwords, Policies, _clock),
            _gate,
            Sending,
            new NonExistenceNotice(_configuration, Sending, _notices, _work, _events, _clock),
            Throttle,
            _events,
            _configuration,
            _work,
            _clock,
            _randomness);

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

    private PolicyResolution Policies => new(_memberships, _configuration, _raises);

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

    private static CredentialLabel Label(string entered) =>
        CredentialLabel.TryParse(entered, out CredentialLabel label)
            ? label
            : throw new InvalidOperationException("The label is not one.");

    private static string Named(int which) => "person" + which.ToString(
        System.Globalization.CultureInfo.InvariantCulture) + "@example.test";

    private ValueTask<Result<ApprovedRecovery>> Approving(
        SubjectId approver,
        SessionId session,
        SubjectId subject,
        string reason,
        string channel = Address) =>
        Service.ApproveAsync(
            AccessContext.Of(approver),
            session,
            subject,
            reason,
            channel,
            Language,
            Source,
            TestContext.Current.CancellationToken);

    // The token the last message carried, which is what a person would open.
    private string Sent() => _mail.Taken[^1].Body.Trim();

    private string Texted() => _sms.Taken[^1].Text.Trim();

    private AuthenticatorId Enrolled(SubjectId subject, Factor factor)
    {
        var id = new AuthenticatorId(Guid.NewGuid());

        _authenticators.Hold(Authenticator.Existing(
            id,
            subject,
            factor,
            Label("Phone"),
            AuthenticatorState.Active,
            _clock.GetUtcNow(),
            lastUsedAt: null,
            invalidatesAt: null,
            confirmed: true,
            totp: null,
            webAuthn: null));

        return id;
    }

    private async ValueTask<(SubjectId Approver, SessionId Session)> ApproverAsync()
    {
        SubjectId approver = await AccountAsync("approver" + Guid.NewGuid().ToString("N") + "@example.test");

        _memberships.Place(approver, Support);
        _gate.Grant(approver, Support, Permissions.RecoveryApprove);

        IssuedSession issued = Value(await Sessions.BeginAsync(
            approver,
            [Factor.Password],
            Somewhere,
            TestContext.Current.CancellationToken))!;

        return (approver, issued.Id);
    }

    private async ValueTask<SubjectId> AccountAsync(string address = Address, bool password = true)
    {
        var subject = new SubjectId(Guid.NewGuid());

        _accounts.Stands(subject, AccountState.Active);
        _accounts.Registered(subject, _clock.GetUtcNow());
        _identifiers.Reads(subject, Language);

        IdentifierId email = _identifiers.Verified(subject, IdentifierKind.Email, address);

        if (string.Equals(address, Address, StringComparison.Ordinal))
        {
            _ = _identifiers.Verified(subject, IdentifierKind.Phone, Number);
        }

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

    private async ValueTask<SubjectId> AccountAsync(bool password) =>
        await AccountAsync(Address, password);

    private static bool Succeeded(Result result) => result.Match(() => true, _ => false);

    private static ErrorCode Refused(Result result) =>
        result.Match(() => default, error => error.Code);

    private static ErrorCode Refused<TValue>(Result<TValue> result) =>
        result.Match(_ => default, error => error.Code);

    private static TValue? Value<TValue>(Result<TValue> result)
        where TValue : class =>
        result.Match<TValue?>(value => value, _ => null);
}
