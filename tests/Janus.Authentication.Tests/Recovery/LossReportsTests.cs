using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Events;
using Janus.Authentication.Factors;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Recovery;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests.Accounts;
using Janus.Authentication.Tests.Factors;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Passwords;
using Janus.Authentication.Tests.Policies;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using OtpNet;
using Xunit;

namespace Janus.Authentication.Tests.Recovery;

/// <summary>
/// Reporting a credential lost: refused at once, notified through the window,
/// cancellable from any notice, and gone only when the window has run and somebody
/// was told (AUTH-RECOV-007, AUTH-RECOV-007a, AUTH-RECOV-008).
/// </summary>
[Trait("kind", "unit")]
public sealed class LossReportsTests : IAsyncDisposable
{
    private static readonly AccessContext Sweeper = AccessContext.Of(
        SystemPrincipal.ForDeployment("expiry-sweep", "OPS-OBS-003", SystemOperation.ExpirySweep));

    private const string Language = "en";
    private const string Source = "198.51.100.7";
    private const string Address = "person@example.test";
    private const string Number = "+441632960011";
    private const string Secret = "orangemarmaladeandtoast";
    private const string Short = "marmalade1";

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly OrganizationId Staff = new(Guid.NewGuid());

    private readonly LossReportStoreInMemory _reports = new();
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly AccountDirectoryInMemory _accounts = new(PreferenceDeclarations.None);
    private readonly AuthenticatorStoreInMemory _authenticators = new();
    private readonly PasswordStoreInMemory _passwords = new();
    private readonly LeakedPasswordCorpusInMemory _corpus = new();
    private readonly WordListInMemory _words = new();
    private readonly ScreeningLogInMemory _screening = new();
    private readonly RecoveryCodeStoreInMemory _sets = new();
    private readonly CredentialAuditInMemory _credentials = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly PolicyRaiseStoreInMemory _raises = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly GovernedSendInMemory _notifications = new();
    private readonly EventsInMemory _events = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private IEvents? _outbox;

    /// <summary>
    /// A deployment that can send the notices the window carries.
    /// </summary>
    public LossReportsTests()
    {
        _notifications.Work = _work;
        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);
        _configuration.Set(Settings.NotificationLanguages, [Language, "ar"]);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// INF-BG-002 AC1, IDN-PRIN-001 AC3: the advance runs as a named principal that may
    /// sweep what has expired, and is refused to a person and to a principal named for
    /// other work.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_BG_002_AC1_TheAdvanceNeverRunsAsNobodyAsync()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () => await Service.AdvanceAsync(
            AccessContext.Of(new SubjectId(Guid.CreateVersion7())),
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(async () => await Service.AdvanceAsync(
            AccessContext.Of(SystemPrincipal.ForDeployment(
                "mail-reconciliation",
                "INT-MAIL-007",
                SystemOperation.Reconciliation)),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-RECOV-007 AC1: a session holding nothing but a password reports the lost
    /// generator, no step-up is asked for, and it is refused from that instant.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC1_APasswordAloneReportsALossAndSuspendsItAtOnceAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        LossReported? reported = Value(await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken));

        Assert.NotNull(reported);
        Assert.Equal(_clock.GetUtcNow() + TimeSpan.FromDays(7), reported.InvalidatesAt);
        Assert.Equal(AuthenticatorState.Suspended, await StateAsync(generator));
        Assert.NotEmpty(_notifications.Mail);
    }

    /// <summary>
    /// IDN-PRIN-001 AC4, INF-BG-002 AC2: the credential the window invalidates was
    /// invalidated by nobody, so the record names the principal the sweep ran as and
    /// the reason it stated, where a report names the person who made it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_PRIN_001_AC4_AnInvalidationIsRecordedUnderTheSweepAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(8));

        _ = await Service.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken);

        int reported = _credentials.Records.FindIndex(record => record.Action == AuditActions.CredentialReportedLost);
        int invalidated = _credentials.Records.FindIndex(record => record.Action == AuditActions.CredentialInvalidated);

        Assert.Null(_credentials.Principals[reported]);
        Assert.Same(Sweeper.Principal, _credentials.Principals[invalidated]);
        Assert.Equal((AuditActions.CredentialInvalidated, subject, generator), _credentials.Records[invalidated]);
    }

    /// <summary>
    /// AUTH-RECOV-007, CONV-DESIGN-003 AC6: a cancellation committed while the window's
    /// end waited for the credential's row is found under the lock, so the credential
    /// stays as the cancellation left it and nothing is invalidated.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_ACancellationCommittedMeanwhileStandsAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(8));
        _authenticators.Locking = credential => credential.Restore();

        _ = await Service.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken);

        Assert.Equal(
            AuthenticatorState.Active,
            (await _authenticators.FindAsync(generator, TestContext.Current.CancellationToken))!.State);
        Assert.DoesNotContain(_credentials.Records, record => record.Action == AuditActions.CredentialInvalidated);
        Assert.Empty(_events.Of<CredentialInvalidated>());
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10: the window's end for a report cancelled meanwhile
    /// invalidates nothing and writes nothing, so its unit of work is rolled back.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_AWindowEndForACancelledReportIsRolledBackAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(8));
        _authenticators.Locking = credential => credential.Restore();
        _work.Reset();

        _ = await Service.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken);

        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// AUTH-RECOV-007, chapter 10 section 5b: the three things that become of a
    /// reported credential are each announced, carrying what it is and, for the
    /// suspension, when the window ends.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_EachTurnOfAReportIsAnnouncedAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        CredentialSuspended suspended = Assert.Single(_events.Of<CredentialSuspended>());

        Assert.Equal(generator, suspended.Credential);
        Assert.Equal(FactorCatalogue.Generated, suspended.Kind);
        Assert.Equal(subject, suspended.Subject);
        Assert.Equal(_clock.GetUtcNow() + TimeSpan.FromDays(7), suspended.InvalidatesAt);

        _ = await Service.CancelAsync(
            AccessContext.Of(subject),
            generator,
            cancelToken: null,
            TestContext.Current.CancellationToken);

        CredentialRestored restored = Assert.Single(_events.Of<CredentialRestored>());

        Assert.Equal(generator, restored.Credential);
        Assert.Equal(subject, restored.Subject);
        Assert.Equal(subject, restored.Actor);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(8));

        _ = await Service.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken);

        CredentialInvalidated invalidated = Assert.Single(_events.Of<CredentialInvalidated>());

        Assert.Equal(generator, invalidated.Credential);
        Assert.Equal(FactorCatalogue.Generated, invalidated.Kind);
        Assert.Equal(subject, invalidated.Subject);
    }

    /// <summary>
    /// AUTH-RECOV-007, CONV-DESIGN-002: the suspension's event row is written in the
    /// transaction that suspends the credential, opens the report and records it, so a
    /// row that cannot be written fails the report before that transaction commits:
    /// nothing of it stands once the unit of work is disposed, and no notice of a
    /// report that does not stand goes out.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_ASuspensionWhoseEventRowFailsLeavesTheCredentialActiveAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);
        int sent = _notifications.Sent.Count;

        _outbox = new EventOutbox(new PendingEventsUnwritable(), _work);
        _work.Reset();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(0, _work.OutermostCommitted);
        Assert.Equal(sent, _notifications.Sent.Count);
        Assert.Empty(_events.Of<CredentialSuspended>());
    }

    /// <summary>
    /// AUTH-RECOV-007, chapter 10 section 5b: a suspension names who reported it; a
    /// cancellation from a session names who cancelled and whose identity they acted
    /// under, and one from the link a notice carried names nobody.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AReportNamesWhoMadeItAsync()
    {
        SubjectId own = await AccountAsync();
        AuthenticatorId owned = await EnrolledAsync(own);
        SubjectId other = await AccountAsync();
        AuthenticatorId others = await EnrolledAsync(other);
        var acting = new SubjectId(Guid.NewGuid());

        _ = await Service.ReportAsync(AccessContext.Of(own), owned, Source, TestContext.Current.CancellationToken);

        string token = _notifications.Mail[^1].Token();

        _ = await Service.ReportAsync(
            AccessContext.Of(acting, other),
            others,
            Source,
            TestContext.Current.CancellationToken);
        _ = await Service.CancelAsync(null, owned, token, TestContext.Current.CancellationToken);
        _ = await Service.CancelAsync(
            AccessContext.Of(acting, other),
            others,
            cancelToken: null,
            TestContext.Current.CancellationToken);

        IReadOnlyList<CredentialSuspended> suspended = _events.Of<CredentialSuspended>();
        IReadOnlyList<CredentialRestored> restored = _events.Of<CredentialRestored>();

        Assert.Equal(((SubjectId?)own, (SubjectId?)own), (suspended[0].Subject, suspended[0].Actor));
        Assert.Equal(((SubjectId?)other, (SubjectId?)acting), (suspended[1].Subject, suspended[1].Actor));
        Assert.Equal(((SubjectId?)own, (SubjectId?)null, (SubjectId?)null), (restored[0].Subject, restored[0].Actor, restored[0].Effective));
        Assert.Equal(((SubjectId?)other, (SubjectId?)acting, (SubjectId?)other), (restored[1].Subject, restored[1].Actor, restored[1].Effective));
    }

    /// <summary>
    /// AUTHZ-IMP-001, chapter 10 section 5b: a suspension carries the acting and the
    /// effective identity of the context that reported it, each as the context gives
    /// it, whether or not they are one account.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_IMP_001_ASuspensionCarriesBothIdentitiesAsTheContextGivesThemAsync()
    {
        SubjectId own = await AccountAsync();
        AuthenticatorId owned = await EnrolledAsync(own);
        SubjectId other = await AccountAsync();
        AuthenticatorId others = await EnrolledAsync(other);
        var acting = new SubjectId(Guid.NewGuid());

        _ = await Service.ReportAsync(AccessContext.Of(own), owned, Source, TestContext.Current.CancellationToken);
        _ = await Service.ReportAsync(
            AccessContext.Of(acting, other),
            others,
            Source,
            TestContext.Current.CancellationToken);

        IReadOnlyList<CredentialSuspended> suspended = _events.Of<CredentialSuspended>();

        Assert.Equal(((SubjectId?)own, (SubjectId?)own), (suspended[0].Actor, suspended[0].Effective));
        Assert.Equal(((SubjectId?)acting, (SubjectId?)other), (suspended[1].Actor, suspended[1].Effective));
    }

    /// <summary>
    /// AUTH-RECOV-007: a sweep whose announcement was refused answers with the
    /// refusal, so an invalidation no consumer was told of is not reported as work
    /// the sweep carried (LIB-API-001).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_ASweepWhoseAnnouncementIsRefusedAnswersWithTheRefusalAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(8));
        _events.Refusal = Error.From(ErrorCodes.SystemFault);
        _work.Reset();

        Assert.Equal(
            ErrorCodes.SystemFault,
            Refused(await Service.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken)));

        // CONV-DESIGN-003 AC5: the refusal ends the unit of work the invalidation was
        // written in with nothing committed.
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a suspension whose announcement is refused is refused after
    /// it was written, and the refusal ends the unit of work with nothing committed and
    /// no notice sent.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ASuspensionThatCannotBeAnnouncedRollsBackAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);
        int sent = _notifications.Sent.Count;

        _events.Refusal = Error.From(ErrorCodes.SystemFault);
        _work.Reset();

        Assert.Equal(
            ErrorCodes.SystemFault,
            Refused(await Service.ReportAsync(
                AccessContext.Of(subject),
                generator,
                Source,
                TestContext.Current.CancellationToken)));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.Equal(sent, _notifications.Sent.Count);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a cancellation that finds, under the credential's lock, a
    /// report no longer running is refused as an unknown credential is, and the refusal
    /// ends the unit of work with nothing committed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ACancellationOfAReportEndedMeanwhileRollsBackAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _authenticators.Locking = credential => credential.Restore();
        _work.Reset();

        Result cancelled = await Service.CancelAsync(
            AccessContext.Of(subject),
            generator,
            cancelToken: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.CredentialNotFound, cancelled.Match<ErrorCode?>(() => null, error => error.Code));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        Assert.DoesNotContain(_credentials.Records, record => record.Action == AuditActions.CredentialReportCancelled);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a cancellation whose announcement is refused is refused
    /// after it was written, and the refusal ends the unit of work with nothing
    /// committed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ACancellationThatCannotBeAnnouncedRollsBackAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _events.Refusal = Error.From(ErrorCodes.SystemFault);
        _work.Reset();

        Result cancelled = await Service.CancelAsync(
            AccessContext.Of(subject),
            generator,
            cancelToken: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.SystemFault, cancelled.Match<ErrorCode?>(() => null, error => error.Code));
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC2: a suspended generator is no longer presentable and no longer
    /// counted towards a gate. Presented anyway it is judged as an active one would be:
    /// the code it gives is refused <c>auth.credential.suspended</c>, and a code whose
    /// step is spent is refused as an active generator's is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC2_ASuspendedCredentialIsRejectedAtSignInAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);
        string spent = Code(generator);
        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        ErrorCode? replayed = Refused(await Totp.PresentAsync(subject, spent, TestContext.Current.CancellationToken));
        _clock.Advance(TimeSpan.FromSeconds(3 * TotpCodes.StepSeconds));
        ErrorCode? given = Refused(await Totp.PresentAsync(subject, Code(generator), TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.CodeReplayed, replayed);
        Assert.Equal(ErrorCodes.CredentialSuspended, given);
        Assert.DoesNotContain(Factor.Totp, await UsableAsync(subject));
    }

    /// <summary>
    /// AUTH-RECOV-007 AC4: while the report runs, the account still reaches what it
    /// reached before, so every gate that needed the lost factor stays closed to
    /// everyone; only invalidation moves it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC4_ReachableAssuranceMovesOnlyOnInvalidationAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        Assert.Equal(AssuranceLevel.Aal2, await ReachableAsync(subject));

        _clock.Advance(TimeSpan.FromDays(8));

        _ = await Service.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken);

        Assert.Equal(AssuranceLevel.Aal1, await ReachableAsync(subject));
    }

    /// <summary>
    /// AUTH-RECOV-007 AC3: invalidation waits for the window, and a cancellation inside
    /// it puts the credential back.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC3_CancellingInsideTheWindowRestoresTheCredentialAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(3));

        _ = await Service.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken);

        Assert.Equal(AuthenticatorState.Suspended, await StateAsync(generator));

        Assert.True(Succeeded(await Service.CancelAsync(
            AccessContext.Of(subject),
            generator,
            cancelToken: null,
            TestContext.Current.CancellationToken)));

        Assert.Equal(AuthenticatorState.Active, await StateAsync(generator));

        _clock.Advance(TimeSpan.FromDays(8));

        _ = await Service.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken);

        Assert.Equal(AuthenticatorState.Active, await StateAsync(generator));
    }

    /// <summary>
    /// AUTH-RECOV-007 AC3: the link every notice carried cancels the report on its own,
    /// which is what someone who cannot sign in has.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC3_TheLinkInTheNoticeCancelsTheReportAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        Assert.True(Succeeded(await Service.CancelAsync(
            AccessContext.Of(new SubjectId(Guid.NewGuid())),
            generator,
            _notifications.Mail[^1].Token(),
            TestContext.Current.CancellationToken)));

        Assert.Equal(AuthenticatorState.Active, await StateAsync(generator));
    }

    /// <summary>
    /// AUTH-RECOV-007 AC3: with every notice failing delivery, invalidation is held
    /// when the window ends and the report carries that it was.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC3_InvalidationIsHeldWhereNoNoticeDeliveredAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _notifications.Refusal = Error.From(ErrorCodes.SystemFault);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(8));

        _ = await Service.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken);

        Assert.Equal(AuthenticatorState.Suspended, await StateAsync(generator));
        Assert.NotNull(
            (await _reports.FindAsync(generator, TestContext.Current.CancellationToken))!.HeldAt);
    }

    /// <summary>
    /// AUTH-RECOV-007 AC6: invalidating the last second factor takes the recovery code
    /// set with it, because there is nothing left for the codes to stand in for.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AC6_TheLastSecondFactorTakesTheRecoveryCodesAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = Value(await Codes.GenerateAsync(subject, TestContext.Current.CancellationToken));

        Assert.NotNull(await _sets.FindAsync(subject, TestContext.Current.CancellationToken));

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(8));

        _ = await Service.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken);

        Assert.Null(await _sets.FindAsync(subject, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-RECOV-007a AC1 and AC2: an invalidation that leaves the account below AAL2
    /// re-reads the password against the single-factor floor, and one that falls short
    /// is marked for change at the next sign-in rather than refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007a_AC1_AnInvalidationRereadsThePasswordAgainstTheFloorAsync()
    {
        SubjectId subject = await AccountAsync(Short);
        AuthenticatorId generator = await EnrolledAsync(subject);

        Assert.False(
            (await _passwords.FindAsync(subject, TestContext.Current.CancellationToken))!
                .MeetsSingleFactorFloor);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(8));

        _ = await Service.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken);

        Assert.True(
            (await _passwords.FindAsync(subject, TestContext.Current.CancellationToken))!
                .ChangeRequired);
    }

    /// <summary>
    /// AUTH-RECOV-007a AC1: a password that already met the single-factor floor is left
    /// alone by the same invalidation.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007a_AC1_APasswordThatMeetsTheFloorIsNotMarkedAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(8));

        _ = await Service.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken);

        Assert.False(
            (await _passwords.FindAsync(subject, TestContext.Current.CancellationToken))!
                .ChangeRequired);
    }

    /// <summary>
    /// AUTH-RECOV-008 AC1: an account whose policy holds two credentials reports a loss
    /// through an approver, and the self-service report is not open to it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_008_AC1_SelfServiceLossReportingIsClosedToStaffAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _memberships.Place(subject, Staff);
        _configuration.Set(
            Settings.OrganizationPolicy,
            Staff.ToString(),
            new PolicyOverride(null, null, null, null, SelfServiceRecovery: false, null, null));

        Assert.Equal(
            ErrorCodes.LossReportNotPermitted,
            Refused(await Service.ReportAsync(
                AccessContext.Of(subject),
                generator,
                Source,
                TestContext.Current.CancellationToken)));

        Assert.Equal(AuthenticatorState.Active, await StateAsync(generator));
    }

    /// <summary>
    /// AUTH-RECOV-007, `09` `POST /recovery/report-loss`: one report stands per
    /// credential, so a second report against the same one is refused
    /// <c>auth.lossreport.pending</c> with the instant its window ends, rather than
    /// restarting the window.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_ASecondReportAgainstOneCredentialIsRefusedPendingWithItsWindowAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);
        DateTimeOffset completes = _clock.GetUtcNow() + TimeSpan.FromDays(7);
        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);
        _clock.Advance(TimeSpan.FromDays(1));

        Error refused = Refusal(await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.LossReportPending, refused.Code);
        Assert.Equal(completes, refused.Details["invalidatesAt"].GetDateTimeOffset());
        Assert.Equal(completes, (await _authenticators.FindAsync(generator, TestContext.Current.CancellationToken))!.InvalidatesAt);
    }

    /// <summary>
    /// AUTH-RECOV-007, `09` `POST /recovery/report-loss`: a credential a removal that
    /// would lower the account's reachable assurance already suspended is reported lost
    /// to no effect, and the report is refused <c>auth.lossreport.pending</c> with the
    /// instant its window ends, never <c>auth.credential.suspended</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AReportOnACredentialARemovalSuspendedIsRefusedPendingWithItsWindowAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);
        Authenticator held = (await _authenticators.FindAsync(generator, TestContext.Current.CancellationToken))!;
        DateTimeOffset completes = _clock.GetUtcNow() + TimeSpan.FromDays(7);
        _ = await Service.SuspendAsync(
            AccessContext.Of(subject),
            held,
            Source,
            TestContext.Current.CancellationToken);
        int sent = _notifications.Mail.Count;

        Error refused = Refusal(await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.LossReportPending, refused.Code);
        Assert.Equal(completes, refused.Details["invalidatesAt"].GetDateTimeOffset());
        Assert.Equal(sent, _notifications.Mail.Count);
    }

    /// <summary>
    /// AUTH-RECOV-007, `09` `POST /recovery/report-loss`: a suspended credential whose
    /// report's row is gone is still one already suspended, and its report is refused
    /// <c>auth.lossreport.pending</c> with the instant the credential carries.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AReportOnASuspendedCredentialWithNoReportIsRefusedPendingWithItsWindowAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);
        Authenticator held = (await _authenticators.FindAsync(generator, TestContext.Current.CancellationToken))!;
        DateTimeOffset completes = _clock.GetUtcNow() + TimeSpan.FromDays(3);
        held.Suspend(completes);

        Error refused = Refusal(await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.LossReportPending, refused.Code);
        Assert.Equal(completes, refused.Details["invalidatesAt"].GetDateTimeOffset());
    }

    /// <summary>
    /// AUTH-RECOV-007, `09` `POST /recovery/report-loss`: an invalidated credential, and
    /// one that is not the account's, are reported lost as one the account does not
    /// hold: <c>auth.credential.notfound</c>, and nothing is suspended.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_AReportOnAnInvalidatedCredentialOrAnothersIsRefusedNotFoundAsync()
    {
        SubjectId subject = await AccountAsync();
        var theirs = AuthenticatorId.New(_clock);
        AuthenticatorId gone = await EnrolledAsync(subject);
        _authenticators.Hold(Authenticator.Existing(
            theirs,
            new SubjectId(Guid.NewGuid()),
            Factor.Totp,
            Label(),
            AuthenticatorState.Active,
            _clock.GetUtcNow(),
            null,
            null,
            confirmed: true,
            new TotpMaterial(new byte[20], null),
            null));
        (await _authenticators.FindAsync(gone, TestContext.Current.CancellationToken))!.Invalidate();

        Error invalidated = Refusal(await Service.ReportAsync(
            AccessContext.Of(subject),
            gone,
            Source,
            TestContext.Current.CancellationToken));
        Error anothers = Refusal(await Service.ReportAsync(
            AccessContext.Of(subject),
            theirs,
            Source,
            TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.CredentialNotFound, invalidated.Code);
        Assert.Equal(ErrorCodes.CredentialNotFound, anothers.Code);
        Assert.Empty(invalidated.Details);
        Assert.Equal(AuthenticatorState.Active, await StateAsync(theirs));
        Assert.Null(await _reports.FindAsync(theirs, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTH-RECOV-007 and D-153: the notice repeats across the window, and every one of
    /// them is the credential-suspended message carrying the link that ends the report.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_RECOV_007_TheNoticeRepeatsAcrossTheWindowAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        string first = _notifications.Mail[^1].Token();
        int sent = _notifications.Mail.Count;

        _clock.Advance(TimeSpan.FromDays(1));

        _ = await Service.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken);

        Assert.True(_notifications.Mail.Count > sent);
        Assert.Equal(first, _notifications.Mail[^1].Token());
        Assert.All(_notifications.Mail, notice => Assert.Equal(MessageKind.CredentialSuspended, notice.Message));
    }

    /// <summary>
    /// IDN-ATTR-001 AC2 and AC3: the notice the sweep repeats has no request in front
    /// of it, so it goes out in the language the account settled on, and in every
    /// declared language once the account holds none the deployment writes in.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ATTR_001_AC2_ANoticeTheSweepSendsResolvesItsLanguageWithoutARequestAsync()
    {
        SubjectId subject = await AccountAsync();
        AuthenticatorId generator = await EnrolledAsync(subject);

        _identifiers.Reads(subject, "ar-EG");

        _ = await Service.ReportAsync(
            AccessContext.Of(subject),
            generator,
            Source,
            TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromDays(1));
        _ = await Service.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken);

        string? settled = _notifications.Mail[^1].Language;

        _identifiers.Reads(subject, "fr");
        _clock.Advance(TimeSpan.FromDays(1));
        _ = await Service.AdvanceAsync(Sweeper, TestContext.Current.CancellationToken);

        Assert.Equal("ar", settled);
        Assert.Null(_notifications.Mail[^1].Language);
    }

    private LossReports Service =>
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
            _outbox ?? _events,
            _configuration,
            _work,
            _clock,
            _randomness);

    private TotpService Totp =>
        new(_authenticators, _passwords, _configuration, _work, _clock, _randomness);

    private RecoveryCodeService Codes =>
        new(
            _sets,
            new Argon2idHasher(_randomness),
            _configuration,
            _work,
            _clock,
            _randomness);

    private PolicyResolution Policies => new(_memberships, _configuration, _raises);

    private PasswordService Passwords =>
        new(
            _passwords,
            new PasswordScreening(_corpus, _words, _configuration, _screening, _events, _clock),
            new Argon2idHasher(_randomness),
            _events,
            _configuration,
            _work,
            _clock);

    private string Code(AuthenticatorId generator) =>
        new Totp(
                _authenticators.All
                    .Single(credential => credential.Id == generator)
                    .Totp!.Secret.ToArray(),
                TotpCodes.StepSeconds,
                OtpHashMode.Sha1,
                TotpCodes.Digits)
            .ComputeTotp(_clock.GetUtcNow().UtcDateTime);

    private async ValueTask<AuthenticatorState> StateAsync(AuthenticatorId credential) =>
        (await _authenticators.FindAsync(credential, TestContext.Current.CancellationToken))!.State;

    private async ValueTask<IReadOnlySet<Factor>> UsableAsync(SubjectId subject) =>
        HeldFactors.Of(
                await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken),
                password: true)
            .Usable;

    private async ValueTask<AssuranceLevel> ReachableAsync(SubjectId subject) =>
        StepUp.Reachable(
                HeldFactors.Of(
                        await _authenticators.OfAsync(subject, TestContext.Current.CancellationToken),
                        password: true)
                    .Standing)
            .Level;

    private async ValueTask<AuthenticatorId> EnrolledAsync(SubjectId subject)
    {
        TotpEnrolment enrolment = Value(await Totp.BeginAsync(
            subject,
            Label(),
            TestContext.Current.CancellationToken))!;

        _ = await Totp.ConfirmAsync(
            subject,
            enrolment.Id,
            new Totp(
                    enrolment.Secret.ToArray(),
                    TotpCodes.StepSeconds,
                    OtpHashMode.Sha1,
                    TotpCodes.Digits)
                .ComputeTotp(_clock.GetUtcNow().UtcDateTime),
            TestContext.Current.CancellationToken);

        return enrolment.Id;
    }

    private static CredentialLabel Label() =>
        CredentialLabel.TryParse("Phone", out CredentialLabel label)
            ? label
            : throw new InvalidOperationException("The label is not one.");

    private async ValueTask<SubjectId> AccountAsync(string password = Secret)
    {
        var subject = new SubjectId(Guid.NewGuid());

        _accounts.Stands(subject, AccountState.Active);
        _accounts.Registered(subject, _clock.GetUtcNow());
        _identifiers.Reads(subject, Language);

        IdentifierId email = _identifiers.Verified(subject, IdentifierKind.Email, Address);

        _ = _identifiers.Verified(subject, IdentifierKind.Phone, Number);

        await _identifiers.PromoteAsync(subject, email, TestContext.Current.CancellationToken);

        byte[] presented = Encoding.UTF8.GetBytes(password);

        _ = await Passwords.SetAsync(
            subject,
            presented,
            [],
            AssuranceLevel.Aal2,
            actor: null,
            TestContext.Current.CancellationToken);

        await _work.CommitAsync(TestContext.Current.CancellationToken);

        return subject;
    }

    private static bool Succeeded(Result result) => result.Match(() => true, _ => false);

    private static ErrorCode Refused<TValue>(Result<TValue> result) =>
        result.Match(_ => default, error => error.Code);

    private static Error Refusal<TValue>(Result<TValue> result) =>
        result.Match(
            _ => throw new Xunit.Sdk.XunitException("The report was not refused."),
            error => error);

    private static TValue? Value<TValue>(Result<TValue> result)
        where TValue : class =>
        result.Match<TValue?>(value => value, _ => null);
}
