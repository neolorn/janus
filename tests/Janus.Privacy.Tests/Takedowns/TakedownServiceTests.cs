using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Erasures;
using Janus.Privacy.Outbox;
using Janus.Privacy.Policies;
using Janus.Privacy.Requests;
using Janus.Privacy.Takedowns;
using Janus.Privacy.Tests.Erasures;
using Janus.Privacy.Tests.Exports;
using Janus.Privacy.Tests.Outbox;
using Janus.Privacy.Tests.Requests;
using Xunit;

namespace Janus.Privacy.Tests.Takedowns;

/// <summary>
/// The minor takedown: what the trigger does in its one transaction, what the hosts'
/// progress reads as from that moment, and the reversal inside the window
/// (IDN-LIFE-003, PRIV-MINOR-002, AUTH-SESS-010).
/// </summary>
[Trait("kind", "unit")]
public sealed class TakedownServiceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private static readonly SubjectId Mona =
        new(Guid.Parse("22222222-2222-4222-8222-222222222222"));

    private static readonly OrganizationId Company =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly SessionId Browser =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly StepUpGateInMemory _stepUp = new();
    private readonly AccountStatesInMemory _accounts = new();
    private readonly OutboxStoreInMemory _outbox = new();
    private readonly SubscriberInMemory _records = new("records", required: true);
    private readonly SubscriberInMemory _newsletter = new("newsletter", required: false);
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events;
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// A deployment with one customer and one member of staff who holds
    /// <c>takedown:execute</c>.
    /// </summary>
    public TakedownServiceTests()
    {
        _events = new EventsInMemory { Work = _work };
        _accounts.Hold(Ahmed, AccountState.Active);
        _accounts.Hold(Mona, AccountState.Active);
        _administrative.Organization = Company;
        _gate.Grant(Mona, Company, Permissions.TakedownExecute);
    }

    private TakedownService Takedowns =>
        new(
            new AdministrativeScope(_gate, _administrative),
            _stepUp,
            _accounts,
            _outbox,
            [_records, _newsletter],
            _audit,
            _events,
            _configuration,
            _work,
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// IDN-LIFE-003 AC4, AUTH-SESS-010 AC2: the trigger takes the account into its
    /// window with <c>deletingBy = takedown</c>, ends its sessions and writes the
    /// hosts' delivery, and all of it is one transaction.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AC4_TheTriggerTakesTheAccountIntoItsWindowInOneTransactionAsync()
    {
        ExecutedTakedown takedown = Held(await ExecutedAsync());

        Delivery delivery = Assert.Single(_outbox.Deliveries);

        Assert.Equal(AccountState.Deleting, _accounts.Of(Ahmed));
        Assert.Equal(DeletionOrigin.Takedown, _accounts.Deleting);
        Assert.Equal(Noon, _accounts.SessionsEndedAt(Ahmed));
        Assert.Equal(SubjectEventKind.TakedownExecuted, delivery.Kind);
        Assert.Equal(Ahmed, delivery.Subject);
        Assert.Equal(delivery.Id.Value, takedown.Id.Value);
        Assert.Equal(Noon + Settings.TakedownGrace.Default, takedown.ErasureDue);
        Assert.Equal(1, _work.Opened);
        Assert.Equal(1, _work.Committed);
    }

    /// <summary>
    /// IDN-LIFE-003 AC4: no erasure is requested at the trigger, so the only delivery
    /// the hosts are sent is the cancellation and it carries <c>TakedownExecuted</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AC4_NoErasureIsRequestedAtTheTriggerAsync()
    {
        _ = Held(await ExecutedAsync());

        Delivery delivery = Assert.Single(_outbox.Deliveries);

        Assert.DoesNotContain(_outbox.Deliveries, held => held.Kind is SubjectEventKind.ErasureRequested);
        Assert.Equal(Ahmed, Assert.IsType<TakedownExecuted>(delivery.Raised()).Subject);
    }

    /// <summary>
    /// IDN-LIFE-003a AC1: the trigger's identity change and its outbox record are one
    /// transaction, and it writes no erasures row: the operation is handed nothing that
    /// could write one, and the account is left in its window, not erased.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_AC1_TheTriggerWritesItsDeliveryAndNoErasureInOneTransactionAsync()
    {
        _ = Held(await ExecutedAsync());

        Assert.Equal((1, 1), (_work.Opened, _work.Committed));
        Assert.Equal(SubjectEventKind.TakedownExecuted, Assert.Single(_outbox.Deliveries).Kind);
        Assert.Equal(AccountState.Deleting, _accounts.Of(Ahmed));
        Assert.DoesNotContain(
            typeof(TakedownService).GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType == typeof(ISubjectEraser)
                || parameter.ParameterType == typeof(IErasureStore));
    }

    /// <summary>
    /// IDN-LIFE-003 AC3: the execution is written down against the account with the
    /// reason, the trigger in the spelling of 10 section 5.12d, and when the erasure
    /// runs.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AC3_TheTriggerIsAuditedWithItsReasonAsync()
    {
        ExecutedTakedown takedown = Held(await ExecutedAsync(reason: "  a parent wrote in  "));

        PrivacyAuditEntry entry = Assert.Single(_audit.Entries);

        Assert.Equal(AuditActions.TakedownExecuted, entry.Action);
        Assert.Equal(Mona, entry.Acting);
        Assert.Equal(Ahmed, entry.Subject);
        Assert.Equal(Noon, entry.At);
        Assert.Equal("a parent wrote in", entry.Details["reason"].GetString());
        Assert.Equal("customer-report", entry.Details["trigger"].GetString());
        Assert.Equal(takedown.Id.ToString(), entry.Details["takedown"].GetString());
        Assert.Equal(takedown.ErasureDue, entry.Details["erasureDue"].GetDateTimeOffset());
    }

    /// <summary>
    /// IDN-LIFE-003 AC2, PRIV-MINOR-002 AC2: the hosts' progress is readable from the
    /// moment of the trigger, one line per registered subscriber, and a confirmation
    /// shows against the subscriber that gave it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AC2_TheHostsProgressIsReadableFromTheTriggerAsync()
    {
        ExecutedTakedown takedown = Held(await ExecutedAsync());

        TakedownProgress before = Held(await ReadAsync(Mona));

        Assert.Equal(takedown.Id, before.Id);
        Assert.Equal(Ahmed, before.Subject);
        Assert.Equal(Noon, before.TriggeredAt);
        Assert.Equal(takedown.ErasureDue, before.ErasureDue);
        Assert.Equal(ErasureStatus.AwaitingSubscribers, before.Status);
        Assert.Equal(
            [new SubscriberConfirmation("records", true, null), new SubscriberConfirmation("newsletter", false, null)],
            before.Subscribers);

        _outbox.Confirms(Assert.Single(_outbox.Deliveries), "records", Noon + TimeSpan.FromMinutes(5));

        TakedownProgress after = Held(await ReadAsync(Mona));

        Assert.Equal(
            Noon + TimeSpan.FromMinutes(5),
            Assert.Single(after.Subscribers, subscriber => subscriber.Name == "records").ConfirmedAt);
        Assert.Null(Assert.Single(after.Subscribers, subscriber => subscriber.Name == "newsletter").ConfirmedAt);
    }

    /// <summary>
    /// IDN-LIFE-003 AC2, IDN-LIFE-003a: a takedown whose retries were spent and which an
    /// operator completed by hand, under <c>privacyrequest:manage</c>, reads
    /// <c>complete</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AC2_ATakedownCompletedByHandReadsCompleteAsync()
    {
        ExecutedTakedown takedown = Held(await ExecutedAsync());

        Assert.Single(_outbox.Deliveries).Fail();
        _gate.Grant(Mona, Company, Permissions.PrivacyRequestManage);

        var erasures = new ErasureService(
            new AdministrativeScope(_gate, _administrative),
            _stepUp,
            _outbox,
            new ErasureStoreInMemory(),
            [_records, _newsletter],
            ledger: null,
            _audit,
            _work,
            _clock);

        Held(await erasures.CompleteAsync(
            AccessContext.Of(Mona),
            Browser,
            new ErasureId(takedown.Id.Value),
            TestContext.Current.CancellationToken));

        Assert.Equal(ErasureStatus.Complete, Held(await ReadAsync(Mona)).Status);
    }

    /// <summary>
    /// IDN-LIFE-003 AC6: the suspension is announced at the trigger with the
    /// administrator as its origin, and no deletion is, because the subject is sent
    /// nothing that would let them cancel it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AC6_TheSuspensionIsAnnouncedAndNoDeletionIsAsync()
    {
        _ = Held(await ExecutedAsync());

        AccountSuspended suspended = Assert.Single(_events.Of<AccountSuspended>());

        Assert.Equal(SuspensionOrigin.Administrator, suspended.By);
        Assert.Equal(Ahmed, suspended.Subject);
        Assert.Equal(Mona, suspended.Actor);
        Assert.Empty(_events.Of<AccountDeletionRequested>());
    }

    /// <summary>
    /// IDN-LIFE-003: an account restricted or suspended before the indication arrived
    /// is taken down as an active one is.
    /// </summary>
    /// <param name="state">Where the account stood.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(AccountState.Active)]
    [InlineData(AccountState.Restricted)]
    [InlineData(AccountState.Suspended)]
    public async Task IDN_LIFE_003_AnAccountIsTakenDownFromWhereverItStandsAsync(AccountState state)
    {
        _accounts.Hold(Ahmed, state);

        _ = Held(await ExecutedAsync());

        Assert.Equal(AccountState.Deleting, _accounts.Of(Ahmed));
        Assert.Equal(DeletionOrigin.Takedown, _accounts.Deleting);
    }

    /// <summary>
    /// IDN-LIFE-003: a second trigger on an account already taken down is refused as
    /// the takedown already running, and writes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_ASecondTriggerAnswersTakedownActiveAsync()
    {
        _ = Held(await ExecutedAsync());

        Assert.Equal(ErrorCodes.TakedownActive, Refused(await ExecutedAsync()).Code);
        Assert.Single(_outbox.Deliveries);
        Assert.Single(_audit.Entries);
    }

    /// <summary>
    /// IDN-LIFE-003 (D-166): an account already in its own deletion, begun by itself or
    /// by an out-of-band request, is taken down, holding that deletion; the erasure falls
    /// due at the earlier of its own window's end and the takedown's, and the reversal
    /// returns the account to its deletion and its clock.
    /// </summary>
    /// <param name="origin">What began the deletion the takedown found.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(DeletionOrigin.Self)]
    [InlineData(DeletionOrigin.OutOfBandRequest)]
    public async Task IDN_LIFE_003_ARunningDeletionIsTakenDownAsync(DeletionOrigin origin)
    {
        DateTimeOffset began = Noon - Settings.AccountDeletionGrace.Default + TimeSpan.FromDays(1);

        _accounts.Deletes(Ahmed, origin, began);

        ExecutedTakedown takedown = Held(await ExecutedAsync());

        Assert.Equal(began + Settings.AccountDeletionGrace.Default, takedown.ErasureDue);
        Assert.Equal(AccountState.Deleting, _accounts.Of(Ahmed));
        Assert.Equal(DeletionOrigin.Takedown, _accounts.Deleting);
        Assert.Equal(Noon, _accounts.SessionsEndedAt(Ahmed));
        Assert.Single(_events.Of<AccountSuspended>());
        Assert.Equal(takedown.ErasureDue, Held(await ReadAsync(Mona)).ErasureDue);

        Held(await ReversedAsync());

        AccountStanding standing = Assert.IsType<AccountStanding>(
            await _accounts.StandingAsync(Ahmed, TestContext.Current.CancellationToken));

        Assert.Equal((AccountState.Deleting, origin, began), (standing.State, standing.DeletingBy, standing.DeletingSince));
    }

    /// <summary>
    /// IDN-LIFE-003: an account already erased has nothing a takedown stops, which is a
    /// conflict with its state, and the refusal writes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AnErasedAccountIsNotTakenDownAsync()
    {
        _accounts.Deletes(Ahmed, DeletionOrigin.Self, Noon - Settings.AccountDeletionGrace.Default);
        _accounts.Erases(Ahmed);

        Error refused = Refused(await ExecutedAsync());

        Assert.Equal(ErrorCodes.AccountStateConflict, refused.Code);
        Assert.Equal("deleted", refused.Details["state"].GetString());
        Assert.Empty(_outbox.Deliveries);
        Assert.Empty(_audit.Entries);
        Assert.Empty(_events.Of<AccountSuspended>());
        Assert.Null(_accounts.SessionsEndedAt(Ahmed));
    }

    /// <summary>
    /// IDN-LIFE-003, CONV-DESIGN-003 AC6: a state another transaction committed while
    /// the trigger waited for the account's row is the one it answers for, as it would
    /// have been found before, and nothing is written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AStateCommittedMeanwhileIsTheOneAnsweredAsync()
    {
        _accounts.Holding = subject =>
        {
            _accounts.Holding = null;
            _accounts.Erases(subject);
        };

        Error erased = Refused(await ExecutedAsync());

        _accounts.Hold(Ahmed, AccountState.Active);
        _accounts.Holding = subject =>
        {
            _accounts.Holding = null;
            _accounts.Deletes(subject, DeletionOrigin.Takedown, Noon);
        };

        Error taken = Refused(await ExecutedAsync());

        Assert.Equal(ErrorCodes.AccountStateConflict, erased.Code);
        Assert.Equal("deleted", erased.Details["state"].GetString());
        Assert.Equal(ErrorCodes.TakedownActive, taken.Code);
        Assert.Empty(_outbox.Deliveries);
        Assert.Empty(_audit.Entries);
    }

    /// <summary>
    /// IDN-LIFE-003, 09 section 8a: the trigger, the reading and the reversal each
    /// answer a subject no account bears as not found, and nothing is written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_ASubjectWithNoAccountIsNotFoundAsync()
    {
        var nobody = new SubjectId(Guid.Parse("33333333-3333-4333-8333-333333333333"));
        CancellationToken cancellation = TestContext.Current.CancellationToken;

        Assert.Equal(
            ErrorCodes.AccountNotFound,
            Refused(await Takedowns.ExecuteAsync(
                AccessContext.Of(Mona),
                Browser,
                nobody,
                TakedownTrigger.CustomerReport,
                "a parent wrote in",
                cancellation)).Code);
        Assert.Equal(
            ErrorCodes.AccountNotFound,
            Refused(await Takedowns.ReadAsync(AccessContext.Of(Mona), nobody, cancellation)).Code);
        Assert.Equal(
            ErrorCodes.AccountNotFound,
            Refused(await Takedowns.ReverseAsync(
                AccessContext.Of(Mona),
                Browser,
                nobody,
                "an adult, misjudged",
                cancellation)).Code);
        Assert.Empty(_outbox.Deliveries);
        Assert.Empty(_audit.Entries);
    }

    /// <summary>
    /// IDN-LIFE-003 AC2: a takedown that was reversed reads as reversed, with no erasure
    /// due, and not as one still running.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AC2_AReversedTakedownReadsAsReversedAsync()
    {
        ExecutedTakedown takedown = Held(await ExecutedAsync());

        TakedownProgress standing = Held(await ReadAsync(Mona));

        Held(await ReversedAsync());

        TakedownProgress reversed = Held(await ReadAsync(Mona));

        Assert.False(standing.Reversed);
        Assert.Equal(takedown.ErasureDue, standing.ErasureDue);
        Assert.True(reversed.Reversed);
        Assert.Null(reversed.ErasureDue);
        Assert.Equal(takedown.Id, reversed.Id);
        Assert.Equal(Noon, reversed.TriggeredAt);
    }

    /// <summary>
    /// IDN-LIFE-003: a takedown is recorded with a reason, so one without is malformed
    /// and changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_ATriggerWithoutAReasonIsMalformedAsync()
    {
        Error refused = Refused(await ExecutedAsync(reason: "   "));

        Assert.Equal(ErrorCodes.RequestMalformed, refused.Code);
        Assert.Equal("reason", refused.Details["member"].GetString());
        Assert.Equal(AccountState.Active, _accounts.Of(Ahmed));
    }

    /// <summary>
    /// IDN-LIFE-003: the trigger, the reading and the reversal each ask the gate for
    /// <c>takedown:execute</c> before anything of the account is read, and a caller
    /// without it moves nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_TakedownExecuteIsRequiredForEveryOperationAsync()
    {
        _ = Held(await ExecutedAsync());

        Assert.Equal(ErrorCodes.Denied, Refused(await ExecutedAsync(by: Ahmed)).Code);
        Assert.Equal(ErrorCodes.Denied, Refused(await ReadAsync(Ahmed)).Code);
        Assert.Equal(ErrorCodes.Denied, Refused(await ReversedAsync(by: Ahmed)).Code);
        Assert.Equal(AccountState.Deleting, _accounts.Of(Ahmed));
        Assert.DoesNotContain(_stepUp.Asked, asked => asked.Subject == Ahmed);
    }

    /// <summary>
    /// 09 section 8a: the trigger and the reversal each require step-up on the
    /// caller's own session, and a session that has not proved it moves nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_TheTriggerAndTheReversalRequireStepUpAsync()
    {
        _stepUp.Closed = Error.From(ErrorCodes.StepUpRequired);

        Assert.Equal(ErrorCodes.StepUpRequired, Refused(await ExecutedAsync()).Code);
        Assert.Equal(AccountState.Active, _accounts.Of(Ahmed));

        _stepUp.Closed = null;
        _ = Held(await ExecutedAsync());
        _stepUp.Closed = Error.From(ErrorCodes.StepUpRequired);

        Assert.Equal(ErrorCodes.StepUpRequired, Refused(await ReversedAsync()).Code);
        Assert.Equal(AccountState.Deleting, _accounts.Of(Ahmed));
        Assert.Equal(
            [
                (Mona, Browser, StepUpAction.AccountTakedown),
                (Mona, Browser, StepUpAction.AccountTakedown),
                (Mona, Browser, StepUpAction.AccountTakedownReverse),
            ],
            _stepUp.Asked);
    }

    /// <summary>
    /// IDN-LIFE-003 AC5: a reversal inside the window restores the account to active,
    /// is written down with its reason and is announced.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AC5_AReversalInsideTheWindowRestoresActiveAsync()
    {
        _ = Held(await ExecutedAsync());

        _clock.Advance(Settings.TakedownGrace.Default - TimeSpan.FromMinutes(1));

        Held(await ReversedAsync(reason: "an adult, misjudged"));

        PrivacyAuditEntry entry = _audit.Entries[^1];
        TakedownReversed reversed = Assert.Single(_events.Of<TakedownReversed>());

        Assert.Equal(AccountState.Active, _accounts.Of(Ahmed));
        Assert.Equal(AuditActions.TakedownReversed, entry.Action);
        Assert.Equal(Mona, entry.Acting);
        Assert.Equal(Ahmed, entry.Subject);
        Assert.Equal("an adult, misjudged", entry.Details["reason"].GetString());
        Assert.Equal(Ahmed, reversed.Subject);
        Assert.Equal(Mona, reversed.Actor);
    }

    /// <summary>
    /// IDN-LIFE-003 AC5: once the window has closed the reversal is refused, whether
    /// or not the sweep has reached the account yet.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AC5_AReversalAfterTheWindowIsRefusedAsync()
    {
        _ = Held(await ExecutedAsync());

        _clock.Advance(Settings.TakedownGrace.Default);

        Assert.Equal(ErrorCodes.TakedownWindowElapsed, Refused(await ReversedAsync()).Code);
        Assert.Equal(AccountState.Deleting, _accounts.Of(Ahmed));

        _accounts.Erases(Ahmed);

        Assert.Equal(ErrorCodes.TakedownWindowElapsed, Refused(await ReversedAsync()).Code);
        Assert.Empty(_events.Of<TakedownReversed>());
    }

    /// <summary>
    /// IDN-LIFE-003: only a takedown is reversed, so an account in its own deletion
    /// window, or not leaving at all, holds no takedown to reverse.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_OnlyATakedownIsReversedAsync()
    {
        Assert.Equal(ErrorCodes.TakedownNotFound, Refused(await ReversedAsync()).Code);

        _accounts.Deletes(Ahmed, DeletionOrigin.Self, Noon);

        Assert.Equal(ErrorCodes.TakedownNotFound, Refused(await ReversedAsync()).Code);
        Assert.Equal(AccountState.Deleting, _accounts.Of(Ahmed));
    }

    /// <summary>
    /// IDN-LIFE-003: a reversal is recorded with a reason, so one without is
    /// malformed and changes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AReversalWithoutAReasonIsMalformedAsync()
    {
        _ = Held(await ExecutedAsync());

        Error refused = Refused(await ReversedAsync(reason: ""));

        Assert.Equal(ErrorCodes.RequestMalformed, refused.Code);
        Assert.Equal("reason", refused.Details["member"].GetString());
        Assert.Equal(AccountState.Deleting, _accounts.Of(Ahmed));
    }

    /// <summary>
    /// API-CONV-002 (D-166): a reason is free text of 1 to 1024 characters after
    /// trimming, so a trigger or a reversal whose reason runs past that is malformed,
    /// naming it, and changes nothing, while 1024 characters inside spaces are taken.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_002_AReasonPastTheLimitIsMalformedAsync()
    {
        string longest = new('r', 1024);
        string past = new('r', 1025);

        Error trigger = Refused(await ExecutedAsync(reason: past));

        Assert.Equal((ErrorCodes.RequestMalformed, "reason"), (trigger.Code, trigger.Details["member"].GetString()));
        Assert.Equal(AccountState.Active, _accounts.Of(Ahmed));

        _ = Held(await ExecutedAsync(reason: $"  {longest}  "));

        Error reversal = Refused(await ReversedAsync(reason: past));

        Assert.Equal((ErrorCodes.RequestMalformed, "reason"), (reversal.Code, reversal.Details["member"].GetString()));
        Assert.Equal(AccountState.Deleting, _accounts.Of(Ahmed));
    }

    /// <summary>
    /// 09 section 8a (D-166): the session's proof is judged after every other refusal,
    /// so a request refused on its reason, on its subject or on the takedown it names is
    /// answered with that refusal and never asks the caller to step up.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_TheStepUpIsJudgedAfterEveryOtherRefusalAsync()
    {
        var nobody = new SubjectId(Guid.Parse("33333333-3333-4333-8333-333333333333"));

        _stepUp.Closed = Error.From(ErrorCodes.StepUpRequired);

        Assert.Equal(ErrorCodes.RequestMalformed, Refused(await ExecutedAsync(reason: " ")).Code);
        Assert.Equal(ErrorCodes.RequestMalformed, Refused(await ReversedAsync(reason: " ")).Code);
        Assert.Equal(ErrorCodes.TakedownNotFound, Refused(await ReversedAsync()).Code);
        Assert.Equal(
            ErrorCodes.AccountNotFound,
            Refused(await Takedowns.ExecuteAsync(
                AccessContext.Of(Mona),
                Browser,
                nobody,
                TakedownTrigger.CustomerReport,
                "a parent wrote in",
                TestContext.Current.CancellationToken)).Code);
        Assert.Empty(_stepUp.Asked);
    }

    /// <summary>
    /// 09 section 8a: an account that was never taken down has no progress to read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AnAccountNeverTakenDownHasNoProgressAsync() =>
        Assert.Equal(ErrorCodes.TakedownNotFound, Refused(await ReadAsync(Mona)).Code);

    /// <summary>
    /// IDN-LIFE-003 AC4, CONV-DESIGN-002: <c>AccountSuspended</c> is written in the
    /// trigger's transaction, before it commits, and never after.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AC4_TheSuspensionIsWrittenInTheTriggerTransactionAsync()
    {
        _ = Held(await ExecutedAsync());

        Assert.IsType<AccountSuspended>(Assert.Single(_events.PublishedInTransaction));
        Assert.Equal((1, 1), (_work.Opened, _work.Committed));
    }

    /// <summary>
    /// CONV-DESIGN-002: a trigger whose announcement is refused fails with that refusal
    /// and commits nothing, so the takedown and its delivery roll back with it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_ARefusedAnnouncementLeavesNothingAsync()
    {
        _events.Refusal = Error.From(ErrorCodes.SystemFault);

        Assert.Equal(ErrorCodes.SystemFault, Refused(await ExecutedAsync()).Code);
        Assert.Equal((1, 0), (_work.Opened, _work.Committed));
        Assert.Empty(_events.Published);
    }

    /// <summary>
    /// IDN-LIFE-003 AC5, CONV-DESIGN-002: <c>TakedownReversed</c> is written in the
    /// reversal's transaction, before it commits, and never after.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AC5_TheReversalIsWrittenInItsTransactionAsync()
    {
        _ = Held(await ExecutedAsync());
        _work.Reset();

        Held(await ReversedAsync());

        Assert.IsType<TakedownReversed>(_events.PublishedInTransaction[^1]);
        Assert.Equal((1, 1), (_work.Opened, _work.Committed));
    }

    /// <summary>
    /// CONV-DESIGN-002: a reversal whose announcement is refused fails with that refusal
    /// and commits nothing, so the account stays taken down.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_ARefusedReversalAnnouncementLeavesNothingAsync()
    {
        _ = Held(await ExecutedAsync());
        _work.Reset();
        _events.Refusal = Error.From(ErrorCodes.SystemFault);

        Assert.Equal(ErrorCodes.SystemFault, Refused(await ReversedAsync()).Code);
        Assert.Equal((1, 0), (_work.Opened, _work.Committed));
        Assert.Empty(_events.Of<TakedownReversed>());
    }

    /// <summary>
    /// IDN-LIFE-003 AC4 (D-166): <c>AccountSuspended</c> announces that access stopped,
    /// so a trigger on an account already suspended, or already in its own deletion,
    /// writes one as a trigger on an active account does.
    /// </summary>
    /// <param name="state">Where the account stood.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(AccountState.Suspended)]
    [InlineData(AccountState.Deleting)]
    public async Task IDN_LIFE_003_AC4_EveryTriggerAnnouncesTheSuspensionAsync(AccountState state)
    {
        if (state is AccountState.Deleting)
        {
            _accounts.Deletes(Ahmed, DeletionOrigin.Self, Noon - TimeSpan.FromDays(1));
        }
        else
        {
            _accounts.Hold(Ahmed, state);
        }

        _ = Held(await ExecutedAsync());

        Assert.Equal(Ahmed, Assert.Single(_events.Of<AccountSuspended>()).Subject);
        Assert.Single(_events.PublishedInTransaction);
    }

    private static TValue Held<TValue>(Result<TValue> outcome) =>
        outcome.Match(
            value => value,
            error => throw new InvalidOperationException($"The operation was refused with {error.Code}."));

    private static void Held(Result outcome) =>
        outcome.Switch(
            () => { },
            error => throw new InvalidOperationException($"The operation was refused with {error.Code}."));

    private static Error Refused<TValue>(Result<TValue> outcome) =>
        outcome.Match(
            _ => throw new InvalidOperationException("The operation succeeded."),
            error => error);

    private static Error Refused(Result outcome) =>
        outcome.Match(
            () => throw new InvalidOperationException("The operation succeeded."),
            error => error);

    private ValueTask<Result<ExecutedTakedown>> ExecutedAsync(
        SubjectId? by = null,
        string reason = "a parent wrote in") =>
        Takedowns.ExecuteAsync(
            AccessContext.Of(by ?? Mona),
            Browser,
            Ahmed,
            TakedownTrigger.CustomerReport,
            reason,
            TestContext.Current.CancellationToken);

    private ValueTask<Result<TakedownProgress>> ReadAsync(SubjectId by) =>
        Takedowns.ReadAsync(AccessContext.Of(by), Ahmed, TestContext.Current.CancellationToken);

    private ValueTask<Result> ReversedAsync(SubjectId? by = null, string reason = "an adult, misjudged") =>
        Takedowns.ReverseAsync(
            AccessContext.Of(by ?? Mona),
            Browser,
            Ahmed,
            reason,
            TestContext.Current.CancellationToken);
}
