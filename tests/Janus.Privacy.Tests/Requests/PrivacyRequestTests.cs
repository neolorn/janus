using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Policies;
using Janus.Privacy.Requests;
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

    private readonly PrivacyRequestStoreInMemory _requests = new();
    private readonly AccountStatesInMemory _accounts = new();
    private readonly OutboxStoreInMemory _outbox = new();
    private readonly SubjectNoticesInMemory _notices = new();
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly MembershipLookupInMemory _memberships = new();
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
        _memberships.Add(Mona, Company);
        _gate.Grant(Mona, Company, Permissions.PrivacyRequestManage);
    }

    private PrivacyRequestService Requests =>
        new(
            _requests,
            new WorkingCalendar(_configuration),
            new AdministrativeScope(_gate, _memberships),
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
    /// PRIV-RIGHT-004 AC1, 09 section 8a: fulfilling a restriction suspends action by
    /// moving the account to restricted, deletes nothing, and tells the subscribers.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_004_AC1_FulfillingARestrictionRestrictsTheAccountAsync()
    {
        PrivacyRequestReceipt receipt = await SubmittedAsync(PrivacyRequestType.Restriction);

        Result fulfilled = await Requests
            .FulfilAsync(AccessContext.Of(Mona), receipt.RequestId, CancellationToken.None);

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
            .FulfilAsync(AccessContext.Of(Mona), receipt.RequestId, CancellationToken.None);

        Assert.Equal(AccountState.Deleting, _accounts.Of(Ahmed));
        Assert.Equal(DeletionOrigin.OutOfBandRequest, _accounts.Deleting);
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

        _ = await Requests.FulfilAsync(staff, receipt.RequestId, CancellationToken.None);

        Result again = await Requests
            .FulfilAsync(staff, receipt.RequestId, CancellationToken.None);

        Assert.Equal(ErrorCodes.RequestDecided, again.Match(() => default, error => error.Code));
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
            .FulfilAsync(AccessContext.Of(Ahmed), receipt.RequestId, CancellationToken.None);

        Result noSuchRequest = await Requests.FulfilAsync(
            AccessContext.Of(Mona),
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

    private static PrivacyRequestEntry Entry(PrivacyRequestType type, DateOnly receivedAt) =>
        new(Ahmed, type, "please act on this", receivedAt, "letter", "national identity card seen");

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
