using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Policies;
using Janus.Privacy.Requests;
using Janus.Privacy.Tests.Exports;
using Janus.Privacy.Tests.Outbox;
using Xunit;
using Xunit.Sdk;

namespace Janus.Privacy.Tests.Requests;

/// <summary>
/// The data subject request queue: what a subject submits, what a human enters for a
/// request that arrived out of band, and the decision.
/// </summary>
[Trait("kind", "unit")]
public sealed class PrivacyRequestTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private static readonly SubjectId Mona =
        new(Guid.Parse("22222222-2222-4222-8222-222222222222"));

    private static readonly OrganizationId Company =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly SessionId Browser =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private readonly PrivacyRequestStoreInMemory _requests = new();
    private readonly AccountStatesInMemory _accounts = new();
    private readonly OutboxStoreInMemory _outbox = new();
    private readonly SubjectNoticesInMemory _notices = new();
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly StepUpGateInMemory _stepUp = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// A deployment in Cairo, with one member of staff who works the queue and two
    /// customers who do not.
    /// </summary>
    public PrivacyRequestTests()
    {
        _configuration.Set(Settings.PrivacyCalendarTimeZone, "Africa/Cairo");
        _accounts.Hold(Ahmed, AccountState.Active);
        _accounts.Hold(Mona, AccountState.Active);
        _administrative.Organization = Company;
        _gate.Grant(Mona, Company, Permissions.PrivacyRequestManage);
    }

    private PrivacyRequestService Requests =>
        new(
            _requests,
            new WorkingCalendar(_configuration),
            new AdministrativeScope(_gate, _administrative),
            _stepUp,
            _accounts,
            new RestrictionGrant(_accounts, _outbox),
            _notices,
            _audit,
            _configuration,
            _work,
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// PRIV-RIGHT-002 AC1: every request carries a creation timestamp, a computed
    /// decision deadline, and a receipt-sent timestamp equal to creation.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_ARequestCarriesItsCreationDeadlineAndReceiptAsync()
    {
        PrivacyRequestReceipt receipt = await SubmittedAsync(PrivacyRequestType.Restriction);

        QueuedRequest held = Assert.Single(_requests.Queue);

        Assert.Equal(Noon, receipt.ReceiptSentAt);
        Assert.Equal(Noon, held.CreatedAt);
        Assert.Equal(receipt.ReceiptSentAt, held.ReceiptSentAt);
        Assert.Equal(new DateOnly(2026, 9, 28), DateOnly.FromDateTime(receipt.DecisionDue.DateTime));
        Assert.Equal(receipt.DecisionDue, held.DecisionDue);
    }

    /// <summary>
    /// PRIV-RIGHT-002: the receipt goes out the moment the request enters the queue,
    /// and is not a decision.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_TheSubjectIsSentAReceiptOnEntryAsync()
    {
        _ = await SubmittedAsync(PrivacyRequestType.Rectification);

        (SubjectId subject, MessageKind message) = Assert.Single(_notices.Told);

        Assert.Equal(Ahmed, subject);
        Assert.Equal(MessageKind.PrivacyRequestReceived, message);
        Assert.Equal(PrivacyRequestStatus.Open, Assert.Single(_requests.Queue).Status);
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC1: a receipt every channel refuses, as a sending restriction
    /// does, leaves the request standing: it is queued, audited and committed with its
    /// deadline, and it carries no receipt-sent timestamp.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_ASubmittedRequestWhoseReceiptIsRefusedStandsWithNoReceiptAsync()
    {
        _notices.Refuses = true;

        PrivacyRequestReceipt receipt = await SubmittedAsync(PrivacyRequestType.Restriction);

        QueuedRequest held = Assert.Single(_requests.Queue);

        Assert.Null(receipt.ReceiptSentAt);
        Assert.Null(held.ReceiptSentAt);
        Assert.Null(held.Read().ReceiptSentAt);
        Assert.Equal(receipt.RequestId, held.Id);
        Assert.Equal(PrivacyRequestStatus.Open, held.Status);
        Assert.Equal(Noon, held.CreatedAt);
        Assert.Equal(receipt.DecisionDue, held.DecisionDue);
        Assert.Empty(_notices.Told);
        Assert.Contains(_audit.Entries, entry => entry.Action == AuditActions.RequestSubmitted);
        Assert.False(_work.Open);
        Assert.Equal(1, _work.Committed);
        Assert.Equal(0, _work.RolledBack);
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC1: a request entered out of band whose receipt every channel
    /// refuses stands the same way, and its clock still runs from the date received.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_AnEnteredRequestWhoseReceiptIsRefusedStandsWithNoReceiptAsync()
    {
        _notices.Refuses = true;

        PrivacyRequestReceipt receipt =
            await EnteredAsync(PrivacyRequestType.Erasure, new DateOnly(2026, 9, 18));

        QueuedRequest held = Assert.Single(_requests.Queue);

        Assert.Null(receipt.ReceiptSentAt);
        Assert.Null(held.ReceiptSentAt);
        Assert.Equal(PrivacyRequestStatus.Open, held.Status);
        Assert.Equal(new DateOnly(2026, 9, 18), held.ReceivedAt);
        Assert.Equal(receipt.DecisionDue, held.DecisionDue);
        Assert.Equal(1, _work.Committed);
        Assert.Equal(0, _work.RolledBack);
    }

    /// <summary>
    /// PRIV-RIGHT-001 AC1: the subject reaches restriction and rectification without
    /// a support contact, holding nothing but their own session.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC1_TheSubjectSubmitsWithoutAHumanAsync()
    {
        _ = await SubmittedAsync(PrivacyRequestType.Restriction);
        _ = await SubmittedAsync(PrivacyRequestType.Rectification);

        Assert.Equal(
            [PrivacyRequestType.Restriction, PrivacyRequestType.Rectification],
            _requests.Queue.Select(request => request.Type));
    }

    /// <summary>
    /// PRIV-RIGHT-001, 09 section 7: erasure is not submitted here, because a
    /// signed-in customer exercises it with account deletion and one that arrives out
    /// of band needs a human to confirm who asked.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC1_ErasureIsNotSubmittedOnTheQueueAsync()
    {
        Result<PrivacyRequestReceipt> refused = await Requests.SubmitAsync(
            AccessContext.Of(Ahmed),
            PrivacyRequestType.Erasure,
            "please erase me",
            CancellationToken.None);

        Assert.Equal(ErrorCodes.Denied, refused.Match(_ => default, error => error.Code));
        Assert.Empty(_requests.Queue);
    }

    /// <summary>
    /// PRIV-RIGHT-001 AC2: an authorised human enters an out-of-band erasure on the
    /// subject's behalf, and what they did to confirm the requester is recorded.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC2_AnOutOfBandRequestRecordsItsIdentityConfirmationAsync()
    {
        _ = await EnteredAsync(PrivacyRequestType.Erasure, new DateOnly(2026, 9, 18));

        QueuedRequest held = Assert.Single(_requests.Queue);

        Assert.Equal(Ahmed, held.Subject);
        Assert.Equal("letter", held.Channel);
        Assert.Equal("national identity card seen", held.IdentityConfirmation);
        Assert.Equal(new DateOnly(2026, 9, 18), held.ReceivedAt);
    }

    /// <summary>
    /// PRIV-RIGHT-002, D-136: the clock runs from the date the request reached the
    /// company, not the date a human got round to typing it in.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_TheClockRunsFromTheDateReceivedAsync()
    {
        PrivacyRequestReceipt entered =
            await EnteredAsync(PrivacyRequestType.Restriction, new DateOnly(2026, 9, 16));

        Assert.Equal(
            new DateOnly(2026, 9, 24),
            DateOnly.FromDateTime(entered.DecisionDue.DateTime));
    }

    /// <summary>
    /// D-153: a date later than today in the deployment zone is refused.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC1_ADateLaterThanTodayIsRefusedAsync()
    {
        Result<PrivacyRequestReceipt> refused = await Requests.EnterAsync(
            AccessContext.Of(Mona),
            Entry(PrivacyRequestType.Erasure, new DateOnly(2026, 9, 21)),
            CancellationToken.None);

        Assert.Equal(
            ErrorCodes.RequestReceivedFuture,
            refused.Match(_ => default, error => error.Code));
        Assert.Empty(_requests.Queue);
    }

    /// <summary>
    /// PRIV-RIGHT-001 AC2: entering a request on another subject's behalf is the
    /// authorised human's to do, and nobody else's.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC2_EnteringWithoutThePermissionIsRefusedAsync()
    {
        Result<PrivacyRequestReceipt> refused = await Requests.EnterAsync(
            AccessContext.Of(Ahmed),
            Entry(PrivacyRequestType.Erasure, new DateOnly(2026, 9, 18)),
            CancellationToken.None);

        Assert.Equal(ErrorCodes.Denied, refused.Match(_ => default, error => error.Code));
        Assert.Empty(_requests.Queue);
    }

    /// <summary>
    /// PRIV-RIGHT-001: a second request of a type the subject already has open is a
    /// duplicate, which the deadline of the first one already covers.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC1_ASecondOpenRequestOfATypeIsADuplicateAsync()
    {
        _ = await SubmittedAsync(PrivacyRequestType.Restriction);

        Result<PrivacyRequestReceipt> refused = await Requests.SubmitAsync(
            AccessContext.Of(Ahmed),
            PrivacyRequestType.Restriction,
            "again",
            CancellationToken.None);

        Assert.Equal(
            ErrorCodes.RequestDuplicate,
            refused.Match(_ => default, error => error.Code));
        Assert.Single(_requests.Queue);
    }

    /// <summary>
    /// PRIV-RIGHT-001 AC1, CONV-DESIGN-003: a request of the type queued while this one
    /// waited for the subject's requests makes this one a duplicate, so one is queued
    /// and one receipt is sent.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC1_ARequestQueuedMeanwhileMakesADuplicateAsync()
    {
        _requests.Holding = () =>
        {
            _requests.Holding = null;
            _ = SubmittedAsync(PrivacyRequestType.Restriction);
        };

        Result<PrivacyRequestReceipt> refused = await Requests.SubmitAsync(
            AccessContext.Of(Ahmed),
            PrivacyRequestType.Restriction,
            "again",
            CancellationToken.None);

        Assert.Equal(
            ErrorCodes.RequestDuplicate,
            refused.Match(_ => default, error => error.Code));
        Assert.Single(_requests.Queue);
        Assert.Single(_notices.Told, told => told.Message is MessageKind.PrivacyRequestReceived);
        Assert.False(_work.Open);
        Assert.Equal(1, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// PRIV-RIGHT-004 AC1, 09 section 8a: fulfilling a restriction suspends action by
    /// moving the account to restricted, deletes nothing, and tells the subscribers.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_004_AC1_FulfillingARestrictionRestrictsTheAccountAsync()
    {
        PrivacyRequestReceipt receipt = await SubmittedAsync(PrivacyRequestType.Restriction);

        Result fulfilled = await Requests
            .FulfilAsync(AccessContext.Of(Mona), Browser, receipt.RequestId, CancellationToken.None);

        Assert.Null(fulfilled.Match(() => (Error?)null, error => error));
        Assert.Equal(AccountState.Restricted, _accounts.Of(Ahmed));
        Assert.Equal(
            PrivacyRequestStatus.Fulfilled,
            Assert.Single(_requests.Queue).Status);
        Assert.Equal(
            Janus.Privacy.Outbox.SubjectEventKind.RestrictionChanged,
            Assert.Single(_outbox.Deliveries).Kind);
    }

    /// <summary>
    /// PRIV-RIGHT-004: a restriction fulfilled while the account is suspended is held
    /// for when it comes back, and the subscribers stop acting on it now.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_004_ARestrictionFulfilledWhileSuspendedIsHeldAsync()
    {
        PrivacyRequestReceipt receipt =
            await EnteredAsync(PrivacyRequestType.Restriction, new DateOnly(2026, 9, 18));

        _accounts.Hold(Ahmed, AccountState.Suspended);

        _ = await Requests
            .FulfilAsync(AccessContext.Of(Mona), Browser, receipt.RequestId, CancellationToken.None);

        Assert.Equal(AccountState.Suspended, _accounts.Of(Ahmed));
        Assert.True(_accounts.Holds(Ahmed));
        Assert.True(Assert.Single(_outbox.Deliveries).Restricted);
    }

    /// <summary>
    /// 09 section 8a, IDN-LIFE-003: a fulfilled erasure enters the grace window with
    /// the origin that says a human entered it out of band.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC2_AFulfilledErasureEntersTheGraceWindowAsync()
    {
        PrivacyRequestReceipt receipt =
            await EnteredAsync(PrivacyRequestType.Erasure, new DateOnly(2026, 9, 18));

        _ = await Requests
            .FulfilAsync(AccessContext.Of(Mona), Browser, receipt.RequestId, CancellationToken.None);

        Assert.Equal(AccountState.Deleting, _accounts.Of(Ahmed));
        Assert.Equal(DeletionOrigin.OutOfBandRequest, _accounts.Deleting);
    }

    /// <summary>
    /// PRIV-RIGHT-001, IDN-LIFE-003 (D-166): an active or restricted account the
    /// fulfilled erasure finds enters its window by <c>oob-request</c> from now.
    /// </summary>
    /// <param name="state">Where the account stands.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(AccountState.Active)]
    [InlineData(AccountState.Restricted)]
    public async Task PRIV_RIGHT_001_IDN_LIFE_003_AnActiveOrRestrictedAccountEntersTheWindowAsync(
        AccountState state)
    {
        _accounts.Hold(Ahmed, state);

        Result fulfilled = await ErasedAsync();

        AccountStanding? standing = await _accounts.StandingAsync(Ahmed, CancellationToken.None);

        Assert.Null(fulfilled.Match(() => (Error?)null, error => error));
        Assert.Equal(AccountState.Deleting, standing?.State);
        Assert.Equal(DeletionOrigin.OutOfBandRequest, standing?.DeletingBy);
        Assert.Equal(Noon, standing?.DeletingSince);
        Assert.Equal(PrivacyRequestStatus.Fulfilled, Assert.Single(_requests.Queue).Status);
    }

    /// <summary>
    /// PRIV-RIGHT-001, IDN-LIFE-003 (D-166): a suspended account the fulfilled erasure
    /// finds enters its window by <c>oob-request</c> holding the suspension, and a
    /// cancellation returns it suspended.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_IDN_LIFE_003_ASuspendedAccountEntersTheWindowAndComesBackSuspendedAsync()
    {
        _accounts.Hold(Ahmed, AccountState.Suspended);

        Result fulfilled = await ErasedAsync();

        AccountStanding? standing = await _accounts.StandingAsync(Ahmed, CancellationToken.None);

        _accounts.Cancels(Ahmed);

        Assert.Null(fulfilled.Match(() => (Error?)null, error => error));
        Assert.Equal(AccountState.Deleting, standing?.State);
        Assert.Equal(DeletionOrigin.OutOfBandRequest, standing?.DeletingBy);
        Assert.Equal(Noon, standing?.DeletingSince);
        Assert.Equal(AccountState.Suspended, _accounts.Of(Ahmed));
        Assert.Equal(PrivacyRequestStatus.Fulfilled, Assert.Single(_requests.Queue).Status);
    }

    /// <summary>
    /// PRIV-RIGHT-001, IDN-LIFE-003 (D-166): an account already in its window, by any
    /// origin, has the erasure recorded fulfilled against the window running, which
    /// keeps its origin and its start.
    /// </summary>
    /// <param name="origin">What began the running window.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(DeletionOrigin.Self)]
    [InlineData(DeletionOrigin.OutOfBandRequest)]
    [InlineData(DeletionOrigin.Takedown)]
    public async Task PRIV_RIGHT_001_IDN_LIFE_003_ADeletingAccountKeepsItsRunningWindowAsync(
        DeletionOrigin origin)
    {
        DateTimeOffset began = Noon - TimeSpan.FromDays(3);

        _accounts.Deletes(Ahmed, origin, began);

        Result fulfilled = await ErasedAsync();

        AccountStanding? standing = await _accounts.StandingAsync(Ahmed, CancellationToken.None);

        Assert.Null(fulfilled.Match(() => (Error?)null, error => error));
        Assert.Equal(AccountState.Deleting, standing?.State);
        Assert.Equal(origin, standing?.DeletingBy);
        Assert.Equal(began, standing?.DeletingSince);
        Assert.Equal(PrivacyRequestStatus.Fulfilled, Assert.Single(_requests.Queue).Status);
    }

    /// <summary>
    /// PRIV-RIGHT-001, IDN-LIFE-003, CONV-DESIGN-003: an account that entered its window
    /// while the fulfilment waited for its row has the erasure recorded fulfilled
    /// against that window, as one found already deleting has.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AnAccountDeletingMeanwhileKeepsItsRunningWindowAsync()
    {
        DateTimeOffset began = Noon - TimeSpan.FromMinutes(1);

        _accounts.Holding = subject =>
        {
            _accounts.Holding = null;
            _accounts.Deletes(subject, DeletionOrigin.Self, began);
        };

        Result fulfilled = await ErasedAsync();

        AccountStanding? standing = await _accounts.StandingAsync(Ahmed, CancellationToken.None);

        Assert.Null(fulfilled.Match(() => (Error?)null, error => error));
        Assert.Equal(DeletionOrigin.Self, standing?.DeletingBy);
        Assert.Equal(began, standing?.DeletingSince);
        Assert.Equal(PrivacyRequestStatus.Fulfilled, Assert.Single(_requests.Queue).Status);
    }

    /// <summary>
    /// IDN-LIFE-003 (D-166, message kinds (3)): a fulfilled out-of-band erasure that
    /// starts the window tells the security-notice set with the out-of-band deletion
    /// notice, which carries no cancel link, and never with the self-service one.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AnErasureThatStartsTheWindowSendsTheOutOfBandNoticeAsync()
    {
        _ = await ErasedAsync();

        Assert.Equal(
            [(Ahmed, MessageKind.PrivacyRequestReceived), (Ahmed, MessageKind.OobDeletionNotice)],
            _notices.Told);
    }

    /// <summary>
    /// IDN-LIFE-003 (D-166, message kinds (3)): an erasure fulfilled against a window
    /// already running starts nothing, so it sends no deletion notice of either kind.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AnErasureOnAnAccountAlreadyDeletingSendsNoNoticeAsync()
    {
        _accounts.Deletes(Ahmed, DeletionOrigin.Self, Noon - TimeSpan.FromDays(3));

        _ = await ErasedAsync();

        Assert.Equal([(Ahmed, MessageKind.PrivacyRequestReceived)], _notices.Told);
    }

    /// <summary>
    /// PRIV-RIGHT-001, IDN-LIFE-003 (D-166): an account already erased has the erasure
    /// recorded fulfilled, and nothing further happens to it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_IDN_LIFE_003_ADeletedAccountChangesNothingAsync()
    {
        _accounts.Erases(Ahmed);

        Result fulfilled = await ErasedAsync();

        AccountStanding? standing = await _accounts.StandingAsync(Ahmed, CancellationToken.None);

        Assert.Null(fulfilled.Match(() => (Error?)null, error => error));
        Assert.Equal(AccountState.Deleted, standing?.State);
        Assert.Null(standing?.DeletingBy);
        Assert.Null(_accounts.Deleting);
        Assert.Equal(PrivacyRequestStatus.Fulfilled, Assert.Single(_requests.Queue).Status);
    }

    /// <summary>
    /// PRIV-RIGHT-001, IDN-LIFE-003 (D-183): the fulfilment of an erasure refuses nothing
    /// on the account's existence, which the entry checked, so a request whose subject
    /// bears no account is a fault and no refusal; the request stays open and no
    /// decision is recorded.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_IDN_LIFE_003_AnErasureOfASubjectNoAccountBearsIsAFaultAsync()
    {
        PrivacyRequestReceipt receipt =
            await EnteredAsync(PrivacyRequestType.Erasure, new DateOnly(2026, 9, 18));

        _accounts.Forget(Ahmed);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Requests
            .FulfilAsync(AccessContext.Of(Mona), Browser, receipt.RequestId, CancellationToken.None));

        Assert.Equal(PrivacyRequestStatus.Open, Assert.Single(_requests.Queue).Status);
        Assert.Null(_accounts.Deleting);
        Assert.False(_work.Open);
        Assert.Equal(1, _work.Committed);
        Assert.Equal(0, _work.RolledBack);
    }

    /// <summary>
    /// PRIV-RIGHT-001 AC3: every exercise is audited, the decision with it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC3_EveryExerciseIsAuditedAsync()
    {
        PrivacyRequestReceipt receipt = await SubmittedAsync(PrivacyRequestType.Rectification);

        _ = await Requests.RefuseAsync(
            AccessContext.Of(Mona),
            receipt.RequestId,
            "the record is the one the bank supplied",
            CancellationToken.None);

        Assert.Equal(
            ["privacy.request.submitted", "privacy.request.refused"],
            _audit.Entries.Select(entry => entry.Action.ToString()));
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC5, PRIV-RIGHT-001: a decision is made once, and the second
    /// attempt is told that one already stands rather than that it may not ask.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC5_ARequestIsDecidedOnceAsync()
    {
        PrivacyRequestReceipt receipt = await SubmittedAsync(PrivacyRequestType.Restriction);
        var staff = AccessContext.Of(Mona);

        _ = await Requests.FulfilAsync(staff, Browser, receipt.RequestId, CancellationToken.None);

        Result again = await Requests
            .FulfilAsync(staff, Browser, receipt.RequestId, CancellationToken.None);

        Assert.Equal(ErrorCodes.RequestDecided, again.Match(() => default, error => error.Code));
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC5, CONV-DESIGN-003: an erasure refused while its fulfilment
    /// waited for the request's row stays refused, and the account does not enter the
    /// deletion window.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC5_AnErasureRefusedMeanwhileBeginsNoDeletionAsync()
    {
        PrivacyRequestReceipt receipt =
            await EnteredAsync(PrivacyRequestType.Erasure, new DateOnly(2026, 9, 18));

        _requests.Locking = request =>
        {
            _requests.Locking = null;
            request.Refuse(_clock.GetUtcNow(), "the caller could not be identified");
        };

        Result fulfilled = await Requests
            .FulfilAsync(AccessContext.Of(Mona), Browser, receipt.RequestId, CancellationToken.None);

        Assert.Equal(ErrorCodes.RequestDecided, fulfilled.Match(() => default, error => error.Code));
        Assert.Equal(PrivacyRequestStatus.Refused, Assert.Single(_requests.Queue).Status);
        Assert.Equal(AccountState.Active, _accounts.Of(Ahmed));
        Assert.False(_work.Open);
        Assert.Equal(1, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a refusal of a request decided while it waited for the
    /// request's row is refused under that row's lock, and rolls its unit of work back.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ARefusalOfARequestDecidedMeanwhileRollsBackAsync()
    {
        PrivacyRequestReceipt receipt = await SubmittedAsync(PrivacyRequestType.Rectification);

        _work.Reset();
        _requests.Locking = request =>
        {
            _requests.Locking = null;
            request.Fulfil(_clock.GetUtcNow());
        };

        Result refused = await Requests.RefuseAsync(
            AccessContext.Of(Mona),
            receipt.RequestId,
            "the record is right",
            CancellationToken.None);

        Assert.Equal(ErrorCodes.RequestDecided, refused.Match(() => default, error => error.Code));
        Assert.Equal(PrivacyRequestStatus.Fulfilled, Assert.Single(_requests.Queue).Status);
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// PRIV-RIGHT-001: the three ways a decision is refused are told apart for the
    /// member of staff working the queue: no permission, no such request, and a
    /// decision that already stands. Nothing under the administrative routes is
    /// concealed from somebody whose business it is.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC2_TheThreeWaysADecisionIsRefusedAreToldApartAsync()
    {
        PrivacyRequestReceipt receipt = await SubmittedAsync(PrivacyRequestType.Restriction);

        Result withoutPermission = await Requests
            .FulfilAsync(AccessContext.Of(Ahmed), Browser, receipt.RequestId, CancellationToken.None);

        Result noSuchRequest = await Requests.FulfilAsync(
            AccessContext.Of(Mona),
            Browser,
            new PrivacyRequestId(Guid.Parse("99999999-9999-4999-8999-999999999999")),
            CancellationToken.None);

        Assert.Equal(
            ErrorCodes.Denied,
            withoutPermission.Match(() => default, error => error.Code));
        Assert.Equal(
            ErrorCodes.RequestNotFound,
            noSuchRequest.Match(() => default, error => error.Code));
    }

    /// <summary>
    /// PRIV-RIGHT-001 AC5, 10 section 5a: fulfilling a request of any type asks the
    /// caller's session for <c>privacyrequest:fulfil</c>, and a session that has not
    /// proved it changes nothing: the account, the request and the trail stay as they
    /// were.
    /// </summary>
    /// <param name="type">What the request asks for.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(PrivacyRequestType.Restriction)]
    [InlineData(PrivacyRequestType.Erasure)]
    [InlineData(PrivacyRequestType.Rectification)]
    public async Task PRIV_RIGHT_001_AC5_AFulfilmentWithoutStepUpChangesNothingAsync(
        PrivacyRequestType type)
    {
        PrivacyRequestReceipt receipt = await EnteredAsync(type, new DateOnly(2026, 9, 18));

        _stepUp.Closed = Error.From(ErrorCodes.StepUpRequired);

        Result refused = await Requests
            .FulfilAsync(AccessContext.Of(Mona), Browser, receipt.RequestId, CancellationToken.None);

        Assert.Equal(ErrorCodes.StepUpRequired, refused.Match(() => default, error => error.Code));
        Assert.Equal((Mona, Browser, StepUpAction.PrivacyRequestFulfil), Assert.Single(_stepUp.Asked));
        Assert.Equal(AccountState.Active, _accounts.Of(Ahmed));
        Assert.Empty(_outbox.Deliveries);
        Assert.Equal(PrivacyRequestStatus.Open, Assert.Single(_requests.Queue).Status);
        Assert.Equal(["privacy.request.entered"], _audit.Entries.Select(entry => entry.Action.ToString()));
    }

    /// <summary>
    /// PRIV-RIGHT-001 AC5, D-166 X8: the step-up is judged after every other refusal,
    /// so a caller without the permission, an identifier naming no request and a
    /// decision already standing are told as themselves and the session is not asked.
    /// Refusing a request asks for no step-up.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC5_TheStepUpIsAskedAfterEveryOtherRefusalAsync()
    {
        PrivacyRequestReceipt receipt = await SubmittedAsync(PrivacyRequestType.Rectification);
        var staff = AccessContext.Of(Mona);

        _stepUp.Closed = Error.From(ErrorCodes.StepUpRequired);

        Result withoutPermission = await Requests
            .FulfilAsync(AccessContext.Of(Ahmed), Browser, receipt.RequestId, CancellationToken.None);
        Result noSuchRequest = await Requests.FulfilAsync(
            staff,
            Browser,
            new PrivacyRequestId(Guid.Parse("99999999-9999-4999-8999-999999999999")),
            CancellationToken.None);
        Result refusal = await Requests
            .RefuseAsync(staff, receipt.RequestId, "the record is right", CancellationToken.None);
        Result decided = await Requests
            .FulfilAsync(staff, Browser, receipt.RequestId, CancellationToken.None);

        Assert.Equal(ErrorCodes.Denied, withoutPermission.Match(() => default, error => error.Code));
        Assert.Equal(ErrorCodes.RequestNotFound, noSuchRequest.Match(() => default, error => error.Code));
        Assert.Null(refusal.Match(() => (Error?)null, error => error));
        Assert.Equal(ErrorCodes.RequestDecided, decided.Match(() => default, error => error.Code));
        Assert.Empty(_stepUp.Asked);
    }

    /// <summary>
    /// 09 section 8a: the queue is read by the human who works it, and by nobody else.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC2_TheQueueIsReadByTheHumanWhoWorksItAsync()
    {
        _ = await SubmittedAsync(PrivacyRequestType.Restriction);

        Result<IReadOnlyList<PrivacyRequest>> queue = await Requests
            .QueueAsync(AccessContext.Of(Mona), CancellationToken.None);

        Result<IReadOnlyList<PrivacyRequest>> refused = await Requests
            .QueueAsync(AccessContext.Of(Ahmed), CancellationToken.None);

        Assert.Single(queue.Match(value => value, error => throw new XunitException(error.Code.ToString())));
        Assert.Equal(ErrorCodes.Denied, refused.Match(_ => default, error => error.Code));
    }

    /// <summary>
    /// API-CONV-002 AC3, CONV-CODE-006 AC3: a detail that is blank, or longer than 1024
    /// characters after trimming, is refused naming it by the service as by the
    /// endpoint, and the queue takes nothing.
    /// </summary>
    /// <param name="character">What the detail is written of.</param>
    /// <param name="length">How many of it.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("d", 0)]
    [InlineData(" ", 3)]
    [InlineData("d", 1025)]
    public async Task API_CONV_002_ABlankOrOverlongDetailIsMalformedAsync(string character, int length)
    {
        Result<PrivacyRequestReceipt> refused = await Requests.SubmitAsync(
            AccessContext.Of(Ahmed),
            PrivacyRequestType.Restriction,
            Written(character, length),
            CancellationToken.None);

        Assert.Equal(ErrorCodes.RequestMalformed, refused.Match(_ => default, error => error.Code));
        Assert.Equal("detail", Member(refused));
        Assert.Empty(_requests.Queue);
    }

    /// <summary>
    /// API-CONV-002: a detail is held as it reads once trimmed, and one of 1024
    /// characters inside its spaces is within the bound.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task API_CONV_002_ADetailIsHeldTrimmedAsync()
    {
        string longest = Written("d", 1024);

        _ = await Requests.SubmitAsync(
            AccessContext.Of(Ahmed),
            PrivacyRequestType.Restriction,
            "  " + longest + "  ",
            CancellationToken.None);

        Assert.Equal(longest, Assert.Single(_requests.Queue).Detail);
    }

    /// <summary>
    /// API-CONV-002 AC3, CONV-CODE-006 AC3: an entry whose channel or identity
    /// confirmation is blank, or longer than 1024 characters after trimming, is
    /// refused naming the member, and the queue takes nothing.
    /// </summary>
    /// <param name="member">The member written wrongly.</param>
    /// <param name="character">What it is written of.</param>
    /// <param name="length">How many of it.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("channel", "c", 0)]
    [InlineData("channel", " ", 2)]
    [InlineData("channel", "c", 1025)]
    [InlineData("identityConfirmation", "c", 0)]
    [InlineData("identityConfirmation", " ", 1)]
    [InlineData("identityConfirmation", "c", 1025)]
    public async Task API_CONV_002_AnEntryWithABlankChannelOrConfirmationIsMalformedAsync(
        string member,
        string character,
        int length)
    {
        PrivacyRequestEntry entry = Entry(PrivacyRequestType.Erasure, new DateOnly(2026, 9, 18));
        string text = Written(character, length);

        Result<PrivacyRequestReceipt> refused = await Requests.EnterAsync(
            AccessContext.Of(Mona),
            member == "channel" ? entry with { Channel = text } : entry with { IdentityConfirmation = text },
            CancellationToken.None);

        Assert.Equal(ErrorCodes.RequestMalformed, refused.Match(_ => default, error => error.Code));
        Assert.Equal(member, Member(refused));
        Assert.Empty(_requests.Queue);
    }

    /// <summary>
    /// API-CONV-002 AC3, CONV-CODE-006 AC3: an entry's detail is optional, and one given
    /// blank or longer than 1024 characters after trimming is malformed; one within the
    /// bound is kept trimmed.
    /// </summary>
    /// <param name="character">What the detail is written of.</param>
    /// <param name="length">How many of it.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("d", 0)]
    [InlineData(" ", 2)]
    [InlineData("d", 1025)]
    public async Task API_CONV_002_AnEntryWithABlankOrOverlongDetailIsMalformedAsync(string character, int length)
    {
        PrivacyRequestEntry entry = Entry(PrivacyRequestType.Erasure, new DateOnly(2026, 9, 18));

        Result<PrivacyRequestReceipt> refused = await Requests.EnterAsync(
            AccessContext.Of(Mona),
            entry with { Detail = Written(character, length) },
            CancellationToken.None);

        Assert.Equal(ErrorCodes.RequestMalformed, refused.Match(_ => default, error => error.Code));
        Assert.Equal("detail", Member(refused));
        Assert.Empty(_requests.Queue);

        _ = await Requests.EnterAsync(
            AccessContext.Of(Mona),
            entry with { Detail = " a letter " },
            CancellationToken.None);

        Assert.Equal("a letter", Assert.Single(_requests.Queue).Detail);
    }

    /// <summary>
    /// API-CONV-002 AC3: the reason a refusal records is free text, and one that is
    /// blank or longer than 1024 characters after trimming decides nothing.
    /// </summary>
    /// <param name="character">What the reason is written of.</param>
    /// <param name="length">How many of it.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("r", 0)]
    [InlineData(" ", 2)]
    [InlineData("r", 1025)]
    public async Task API_CONV_002_ABlankOrOverlongRefusalReasonIsMalformedAsync(string character, int length)
    {
        PrivacyRequestReceipt receipt = await SubmittedAsync(PrivacyRequestType.Rectification);

        Result refused = await Requests.RefuseAsync(
            AccessContext.Of(Mona),
            receipt.RequestId,
            Written(character, length),
            CancellationToken.None);

        Assert.Equal(ErrorCodes.RequestMalformed, refused.Match(() => default, error => error.Code));
        Assert.Equal(
            "reason",
            refused.Match(() => null, error => error.Details["member"].GetString()));
        Assert.Equal(PrivacyRequestStatus.Open, Assert.Single(_requests.Queue).Status);
    }

    /// <summary>
    /// API-CONV-002 AC3, CONV-CODE-006 AC3: an entry that gives no detail records none,
    /// never an empty text.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task API_CONV_002_AnEntryWithNoDetailRecordsNoneAsync()
    {
        PrivacyRequestEntry entry = Entry(PrivacyRequestType.Erasure, new DateOnly(2026, 9, 18));

        Result<PrivacyRequestReceipt> entered = await Requests.EnterAsync(
            AccessContext.Of(Mona),
            entry with { Detail = null },
            CancellationToken.None);

        Assert.True(entered.Match(_ => true, _ => false));
        Assert.Null(Assert.Single(_requests.Queue).Detail);
    }

    /// <summary>
    /// PRIV-RIGHT-001, 09 section 8a (D-183): a request entered for a subject no account
    /// bears is refused naming the member, whatever its type, and the queue takes
    /// nothing.
    /// </summary>
    /// <param name="type">What the entry asks for.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(PrivacyRequestType.Erasure)]
    [InlineData(PrivacyRequestType.Restriction)]
    [InlineData(PrivacyRequestType.Rectification)]
    public async Task PRIV_RIGHT_001_AnEntryForASubjectNoAccountBearsIsInvalidAsync(PrivacyRequestType type)
    {
        _accounts.Forget(Ahmed);

        Result<PrivacyRequestReceipt> refused = await Requests.EnterAsync(
            AccessContext.Of(Mona),
            Entry(type, new DateOnly(2026, 9, 18)),
            CancellationToken.None);

        Assert.Equal(ErrorCodes.RequestInvalid, refused.Match(_ => default, error => error.Code));
        Assert.Equal("subject", Member(refused));
        Assert.Empty(_requests.Queue);
        Assert.False(_work.Open);
    }

    /// <summary>
    /// OPS-BOOT-002, 09 section 8a (D-183): an erasure of the reserved emergency account
    /// is refused as a missing permission once the account is read, before the session
    /// is asked for a step-up, and the request stays open.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_BOOT_002_AnErasureOfTheReservedAccountIsDeniedBeforeTheStepUpAsync()
    {
        PrivacyRequestReceipt receipt =
            await EnteredAsync(PrivacyRequestType.Erasure, new DateOnly(2026, 9, 18));

        _accounts.Reserve(Ahmed);
        _stepUp.Closed = Error.From(ErrorCodes.StepUpRequired);

        Result fulfilled = await Requests
            .FulfilAsync(AccessContext.Of(Mona), Browser, receipt.RequestId, CancellationToken.None);

        Assert.Equal(ErrorCodes.Denied, fulfilled.Match(() => default, error => error.Code));
        Assert.Empty(_stepUp.Asked);
        Assert.Equal(AccountState.Active, _accounts.Of(Ahmed));
        Assert.Equal(PrivacyRequestStatus.Open, Assert.Single(_requests.Queue).Status);
    }

    /// <summary>
    /// PRIV-RIGHT-001, IDN-LIFE-003 (D-183): a deletion that will not begin once the
    /// fulfilment has found the account in a state that begins one is a fault, never a
    /// refusal and never a request recorded fulfilled that erased nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_IDN_LIFE_003_ADeletionThatWillNotBeginIsAFaultAsync()
    {
        PrivacyRequestReceipt receipt =
            await EnteredAsync(PrivacyRequestType.Erasure, new DateOnly(2026, 9, 18));

        _accounts.Holding = subject =>
        {
            _accounts.Holding = null;
            _accounts.Forget(subject);
        };

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Requests
            .FulfilAsync(AccessContext.Of(Mona), Browser, receipt.RequestId, CancellationToken.None));

        Assert.Equal(PrivacyRequestStatus.Open, Assert.Single(_requests.Queue).Status);
    }

    private static PrivacyRequestEntry Entry(PrivacyRequestType type, DateOnly receivedAt) =>
        new(Ahmed, type, "please act on this", receivedAt, "letter", "national identity card seen");

    private static string Written(string character, int length) =>
        string.Concat(Enumerable.Repeat(character, length));

    private static string? Member(Result<PrivacyRequestReceipt> refused) =>
        refused.Match(_ => null, error => error.Details["member"].GetString());

    private async Task<PrivacyRequestReceipt> SubmittedAsync(PrivacyRequestType type)
    {
        Result<PrivacyRequestReceipt> submitted = await Requests.SubmitAsync(
            AccessContext.Of(Ahmed),
            type,
            "please act on this",
            CancellationToken.None);

        return submitted.Match(
            receipt => receipt,
            error => throw new XunitException(error.Code.ToString()));
    }

    // An erasure entered out of band and fulfilled by the member of staff.
    private async Task<Result> ErasedAsync()
    {
        PrivacyRequestReceipt receipt =
            await EnteredAsync(PrivacyRequestType.Erasure, new DateOnly(2026, 9, 18));

        return await Requests
            .FulfilAsync(AccessContext.Of(Mona), Browser, receipt.RequestId, CancellationToken.None);
    }

    private async Task<PrivacyRequestReceipt> EnteredAsync(
        PrivacyRequestType type,
        DateOnly receivedAt)
    {
        Result<PrivacyRequestReceipt> entered = await Requests.EnterAsync(
            AccessContext.Of(Mona),
            Entry(type, receivedAt),
            CancellationToken.None);

        return entered.Match(
            receipt => receipt,
            error => throw new XunitException(error.Code.ToString()));
    }
}
