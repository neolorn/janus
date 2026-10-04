using System;
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
/// What the clock does without a human: the warning, the escalation, the restriction
/// granted by lapse and the erasure deemed refused by it.
/// </summary>
[Trait("kind", "unit")]
public sealed class DeadlineSweepTests : IAsyncDisposable
{
    private static readonly AccessContext Sweeper = AccessContext.Of(
        SystemPrincipal.ForDeployment("expiry-sweep", "OPS-OBS-003", SystemOperation.ExpirySweep));

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
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// A deployment in Cairo with one member of staff who works the queue.
    /// </summary>
    public DeadlineSweepTests()
    {
        _configuration.Set(Settings.PrivacyCalendarTimeZone, "Africa/Cairo");
        _accounts.Hold(Ahmed, AccountState.Active);
        _administrative.Organization = Company;
        _gate.Grant(Mona, Company, Permissions.PrivacyRequestManage);
    }

    private PrivacyRequestService Requests =>
        new(
            _requests,
            new WorkingCalendar(_configuration),
            new AdministrativeScope(_gate, _administrative),
            new StepUpGateInMemory(),
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
    /// INF-BG-002 AC1, IDN-PRIN-001 AC3: the pass runs as a named principal that may
    /// sweep what has expired, and is refused to a person and to a principal named for
    /// other work.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INF_BG_002_AC1_TheSweepNeverRunsAsNobodyAsync()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () => await Sweep.SweepAsync(
            AccessContext.Of(new SubjectId(Guid.CreateVersion7())),
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(async () => await Sweep.SweepAsync(
            AccessContext.Of(SystemPrincipal.ForDeployment(
                "mail-reconciliation",
                "INT-MAIL-007",
                SystemOperation.Reconciliation)),
            TestContext.Current.CancellationToken));
    }

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

        Assert.Equal(1, await Sweep.SweepAsync(Sweeper, CancellationToken.None));

        PrivacyAlertRaised raised = Assert.Single(_alerts.Raised);

        Assert.Equal(AlertCondition.PrivacyDeadlineApproaching, raised.Condition);
        Assert.Equal(receipt.RequestId.ToString(), raised.Scope);
        Assert.Equal(PrivacyRequestStatus.Open, Assert.Single(_requests.Queue).Status);
    }

    /// <summary>
    /// CONV-DESIGN-002, PRIV-RIGHT-002 AC2: the warning's row is written in the
    /// transaction that marks the request warned, so a row that cannot be written fails
    /// the pass and the request is not marked.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CONV_DESIGN_002_AWarningThatCannotBeWrittenFailsThePassAsync()
    {
        _ = await SubmittedAsync(PrivacyRequestType.Restriction);

        _clock.Advance(new DateTimeOffset(2026, 9, 23, 22, 0, 0, TimeSpan.Zero) - Noon);
        _alerts.Refusal = Error.From(ErrorCodes.SystemFault);
        _work.Reset();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Sweep.SweepAsync(Sweeper, CancellationToken.None));

        Assert.Equal(0, _work.Committed);
    }

    /// <summary>
    /// INT-SMS-003: the alert names the request's type and status as chapter 10 section
    /// 5.12c spells them, which is what the operator's message is measured and filled
    /// with.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task INT_SMS_003_AnAlertCarriesTheTypeAndStatusAsTheChapterSpellsThemAsync()
    {
        _ = await EnteredAsync(PrivacyRequestType.Rectification);

        _clock.Advance(new DateTimeOffset(2026, 9, 23, 22, 0, 0, TimeSpan.Zero) - Noon);

        _ = await Sweep.SweepAsync(Sweeper, CancellationToken.None);

        PrivacyAlertRaised raised = Assert.Single(_alerts.Raised);

        Assert.Equal("rectification", raised.Details["type"].GetString());
        Assert.Equal("open", raised.Details["status"].GetString());
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

        _ = await Sweep.SweepAsync(Sweeper, CancellationToken.None);
        _ = await Sweep.SweepAsync(Sweeper, CancellationToken.None);

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

        _ = await Sweep.SweepAsync(Sweeper, CancellationToken.None);

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

        _ = await Sweep.SweepAsync(Sweeper, CancellationToken.None);

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

        _ = await Sweep.SweepAsync(Sweeper, CancellationToken.None);

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

        _ = await Sweep.SweepAsync(Sweeper, CancellationToken.None);

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

        Assert.Equal(0, await Sweep.SweepAsync(Sweeper, CancellationToken.None));
        Assert.Empty(_alerts.Raised);
        Assert.Equal(PrivacyRequestStatus.Refused, Assert.Single(_requests.Queue).Status);
    }

    /// <summary>
    /// PRIV-RIGHT-002 AC5, CONV-DESIGN-003: a request fulfilled while the pass waited
    /// for its row is left as decided: it does not lapse and the subject is not told
    /// it was refused.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_002_AC5_ARequestDecidedMeanwhileDoesNotLapseAsync()
    {
        _ = await EnteredAsync(PrivacyRequestType.Erasure);

        _clock.Advance(new DateTimeOffset(2026, 9, 29, 1, 0, 0, TimeSpan.FromHours(3)) - Noon);

        _requests.Locking = request =>
        {
            _requests.Locking = null;
            request.Fulfil(_clock.GetUtcNow());
        };

        Assert.Equal(0, await Sweep.SweepAsync(Sweeper, CancellationToken.None));
        Assert.Equal(PrivacyRequestStatus.Fulfilled, Assert.Single(_requests.Queue).Status);
        Assert.DoesNotContain(_notices.Told, told => told.Message is MessageKind.PrivacyRequestLapsed);
        Assert.Empty(_alerts.Raised);
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

        _ = await Sweep.SweepAsync(Sweeper, CancellationToken.None);

        Assert.Contains(
            _audit.Entries,
            entry => entry.Action.ToString() is "privacy.request.lapsed");
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10: a request decided while the pass waited for its row is
    /// answered with nothing written, so the pass rolls its unit of work back and
    /// commits nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_ARequestDecidedMeanwhileRollsThePassBackAsync()
    {
        _ = await EnteredAsync(PrivacyRequestType.Erasure);

        _clock.Advance(new DateTimeOffset(2026, 9, 29, 1, 0, 0, TimeSpan.FromHours(3)) - Noon);

        _requests.Locking = request =>
        {
            _requests.Locking = null;
            request.Fulfil(_clock.GetUtcNow());
        };

        _work.Reset();

        Assert.Equal(0, await Sweep.SweepAsync(Sweeper, CancellationToken.None));
        Assert.False(_work.Open);
        Assert.Equal(1, _work.Opened);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC10: a request the pass reaches again between its warning and
    /// its escalation is changed in nothing, so the pass rolls its unit of work back and
    /// commits nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC10_ARequestThePassChangesNothingOfRollsBackAsync()
    {
        _ = await SubmittedAsync(PrivacyRequestType.Restriction);

        _clock.Advance(new DateTimeOffset(2026, 9, 23, 22, 0, 0, TimeSpan.Zero) - Noon);

        Assert.Equal(1, await Sweep.SweepAsync(Sweeper, CancellationToken.None));

        _work.Reset();

        Assert.Equal(0, await Sweep.SweepAsync(Sweeper, CancellationToken.None));
        Assert.False(_work.Open);
        Assert.Equal(1, _work.Opened);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
        _ = Assert.Single(_alerts.Raised);
    }

    /// <summary>
    /// AUTH-ABUSE-004 AC18, PRIV-RIGHT-002 AC4: the notice of a lapse that every channel
    /// refuses fails nothing. The request is recorded deemed refused by lapse and
    /// audited, and the pass commits both.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTH_ABUSE_004_AC18_ALapseWhoseNoticeIsRefusedIsRecordedAndCommittedAsync()
    {
        _ = await EnteredAsync(PrivacyRequestType.Erasure);

        _clock.Advance(new DateTimeOffset(2026, 9, 29, 1, 0, 0, TimeSpan.FromHours(3)) - Noon);

        _notices.Refuses = true;
        _work.Reset();

        Assert.Equal(1, await Sweep.SweepAsync(Sweeper, CancellationToken.None));
        Assert.False(_work.Open);
        Assert.Equal(1, _work.Committed);
        Assert.Equal(0, _work.RolledBack);
        Assert.Equal(PrivacyRequestStatus.DeemedRefusedByLapse, Assert.Single(_requests.Queue).Status);
        Assert.DoesNotContain(_notices.Told, told => told.Message is MessageKind.PrivacyRequestLapsed);
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
