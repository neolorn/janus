using System;
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
/// What the clock does without a human: the warning, the escalation, the restriction
/// granted by lapse and the erasure deemed refused by it.
/// </summary>
[Trait("kind", "unit")]
public sealed class DeadlineSweepTests : IAsyncDisposable
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
    private readonly PrivacyAlertsInMemory _alerts = new();
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly AccessGateInMemory _gate = new();
    private readonly MembershipLookupInMemory _memberships = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// A deployment in Cairo with one member of staff who works the queue.
    /// </summary>
    public DeadlineSweepTests()
    {
        _configuration.Set(Settings.PrivacyCalendarTimeZone, "Africa/Cairo");
        _accounts.Hold(Ahmed, AccountState.Active);
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

    private DeadlineSweep Sweep =>
        new(
            _requests,
            new RestrictionGrant(_accounts, _outbox),
            _notices,
            _alerts,
            _audit,
            _work,
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// PRIV-RIGHT-002 AC2: the Normal alert fires the warning lead before the
    /// deadline, and nothing is asked of a human for it to happen.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC2_TheNormalAlertFiresTwoWorkingDaysBeforeAsync()
    {
        PrivacyRequestReceipt receipt = await SubmittedAsync(PrivacyRequestType.Restriction);

        _clock.Advance(new DateTimeOffset(2026, 9, 23, 22, 0, 0, TimeSpan.Zero) - Noon);

        Assert.Equal(1, await Sweep.SweepAsync(CancellationToken.None));

        PrivacyAlertRaised raised = Assert.Single(_alerts.Raised);

        Assert.Equal(AlertCondition.PrivacyDeadlineApproaching, raised.Condition);
        Assert.Equal(receipt.RequestId.ToString(), raised.Scope);
        Assert.Equal(PrivacyRequestStatus.Open, Assert.Single(_requests.Queue).Status);
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC2: the alert is raised once, however often the sweep runs.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC2_TheNormalAlertIsRaisedOnceAsync()
    {
        _ = await SubmittedAsync(PrivacyRequestType.Restriction);

        _clock.Advance(new DateTimeOffset(2026, 9, 23, 22, 0, 0, TimeSpan.Zero) - Noon);

        _ = await Sweep.SweepAsync(CancellationToken.None);
        _ = await Sweep.SweepAsync(CancellationToken.None);

        Assert.Single(_alerts.Raised);
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC2: the High alert fires at midnight at the head of the
    /// deadline day, which is before the deadline itself.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC2_TheHighAlertFiresOnTheDeadlineDayAsync()
    {
        _ = await SubmittedAsync(PrivacyRequestType.Restriction);

        _clock.Advance(new DateTimeOffset(2026, 9, 28, 1, 0, 0, TimeSpan.FromHours(3)) - Noon);

        _ = await Sweep.SweepAsync(CancellationToken.None);

        Assert.Equal(
            [AlertCondition.PrivacyDeadlineApproaching, AlertCondition.PrivacyDeadlineReached],
            _alerts.Raised.Select(raised => raised.Condition));
        Assert.Equal(PrivacyRequestStatus.Open, Assert.Single(_requests.Queue).Status);
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC3: a restriction undecided at the deadline moves the account
    /// to restricted and is recorded granted by lapse.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC3_ARestrictionUndecidedAtTheDeadlineIsGrantedAsync()
    {
        _ = await SubmittedAsync(PrivacyRequestType.Restriction);

        _clock.Advance(new DateTimeOffset(2026, 9, 29, 1, 0, 0, TimeSpan.FromHours(3)) - Noon);

        _ = await Sweep.SweepAsync(CancellationToken.None);

        Assert.Equal(
            PrivacyRequestStatus.GrantedByLapse,
            Assert.Single(_requests.Queue).Status);
        Assert.Equal(AccountState.Restricted, _accounts.Of(Ahmed));
        Assert.Equal(
            Janus.Privacy.Outbox.SubjectEventKind.RestrictionChanged,
            Assert.Single(_outbox.Deliveries).Kind);
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC4: an out-of-band erasure undecided at the deadline is
    /// recorded deemed refused by lapse, the subject is told, and the record persists.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC4_AnErasureUndecidedAtTheDeadlineIsDeemedRefusedAsync()
    {
        _ = await EnteredAsync(PrivacyRequestType.Erasure);

        _clock.Advance(new DateTimeOffset(2026, 9, 29, 1, 0, 0, TimeSpan.FromHours(3)) - Noon);

        _ = await Sweep.SweepAsync(CancellationToken.None);

        QueuedRequest held = Assert.Single(_requests.Queue);

        Assert.Equal(PrivacyRequestStatus.DeemedRefusedByLapse, held.Status);
        Assert.Equal(AccountState.Active, _accounts.Of(Ahmed));
        Assert.Contains(
            _notices.Told,
            told => told.Message is MessageKind.PrivacyRequestLapsed && told.Subject == Ahmed);
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC4: the system never erases on its own, because erasure cannot
    /// run without a human confirming identity.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC4_TheLapseOfAnErasureErasesNothingAsync()
    {
        _ = await EnteredAsync(PrivacyRequestType.Erasure);

        _clock.Advance(new DateTimeOffset(2026, 9, 29, 1, 0, 0, TimeSpan.FromHours(3)) - Noon);

        _ = await Sweep.SweepAsync(CancellationToken.None);

        Assert.Null(_accounts.Deleting);
        Assert.Empty(_outbox.Deliveries);
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC5: a decision made before the deadline cancels both alerts,
    /// because a decided request is not one the clock reaches.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC5_ADecisionBeforeTheDeadlineCancelsBothAlertsAsync()
    {
        PrivacyRequestReceipt receipt = await SubmittedAsync(PrivacyRequestType.Rectification);

        _ = await Requests.RefuseAsync(
            AccessContext.Of(Mona),
            receipt.RequestId,
            "the record is the one the bank supplied",
            CancellationToken.None);

        _clock.Advance(new DateTimeOffset(2026, 9, 29, 1, 0, 0, TimeSpan.FromHours(3)) - Noon);

        Assert.Equal(0, await Sweep.SweepAsync(CancellationToken.None));
        Assert.Empty(_alerts.Raised);
        Assert.Equal(PrivacyRequestStatus.Refused, Assert.Single(_requests.Queue).Status);
    }

    /// <summary>
    /// PRIV-RIGHT-001 AC3: the lapse is a decision, so it is audited like one.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_001_AC3_ALapseIsAuditedAsync()
    {
        _ = await SubmittedAsync(PrivacyRequestType.Restriction);

        _clock.Advance(new DateTimeOffset(2026, 9, 29, 1, 0, 0, TimeSpan.FromHours(3)) - Noon);

        _ = await Sweep.SweepAsync(CancellationToken.None);

        Assert.Contains(
            _audit.Entries,
            entry => entry.Action.ToString() is "privacy.request.lapsed");
    }

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

    private async Task<PrivacyRequestReceipt> EnteredAsync(PrivacyRequestType type)
    {
        Result<PrivacyRequestReceipt> entered = await Requests.EnterAsync(
            AccessContext.Of(Mona),
            new PrivacyRequestEntry(
                Ahmed,
                type,
                "please act on this",
                new DateOnly(2026, 9, 20),
                "letter",
                "national identity card seen"),
            CancellationToken.None);

        return entered.Match(
            receipt => receipt,
            error => throw new XunitException(error.Code.ToString()));
    }
}
