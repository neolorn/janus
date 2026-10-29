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

    private static readonly SubjectId Guest =
        new(Guid.Parse("55555555-5555-4555-8555-555555555555"));

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
    /// IDN-LIFE-003a AC9, IDN-LIFE-003 AC2: a takedown whose retries were spent and which an
    /// operator completed by hand, under <c>privacyrequest:manage</c>, reads
    /// <c>complete</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_AC9_ATakedownCompletedByHandReadsCompleteAsync()
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
            [],
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
    public async Task IDN_LIFE_003_AC7_ARunningDeletionIsTakenDownAsync(DeletionOrigin origin)
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
    /// OPS-BOOT-002 AC9: a takedown naming the reserved account is refused with
    /// <c>authz.denied</c> once the account has been read under its lock, and writes
    /// nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_BOOT_002_AC9_ATakedownNamingTheReservedAccountIsDeniedAsync()
    {
        _accounts.Reserve(Ahmed);

        Error refused = Refused(await ExecutedAsync());

        Assert.Equal(ErrorCodes.Denied, refused.Code);
        Assert.Equal(AccountState.Active, _accounts.Of(Ahmed));
        Assert.Empty(_outbox.Deliveries);
        Assert.Empty(_audit.Entries);
        Assert.Empty(_events.Of<AccountSuspended>());
        Assert.Null(_accounts.SessionsEndedAt(Ahmed));
        Assert.False(_work.Open);
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
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(2, _work.RolledBack);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5: a reversal refused under the account's lock, because an
    /// erasure committed while it waited for the row, rolls its unit of work back.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_AReversalRefusedUnderTheLockRollsBackAsync()
    {
        _ = Held(await ExecutedAsync());
        _work.Reset();

        _accounts.Holding = subject =>
        {
            _accounts.Holding = null;
            _accounts.Erases(subject);
        };

        Assert.Equal(ErrorCodes.TakedownWindowElapsed, Refused(await ReversedAsync()).Code);
        Assert.Empty(_events.Of<TakedownReversed>());
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
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
        Assert.Equal((false, 1), (_work.Open, _work.RolledBack));
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
        Assert.Equal((false, 1), (_work.Open, _work.RolledBack));
        Assert.Empty(_events.Of<TakedownReversed>());
    }

    /// <summary>
    /// IDN-LIFE-003 AC9 (D-166): <c>AccountSuspended</c> announces that access stopped,
    /// so a trigger on an account restricted, already suspended, or already in its own
    /// deletion, writes one as a trigger on an active account does.
    /// </summary>
    /// <param name="state">Where the account stood.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(AccountState.Restricted)]
    [InlineData(AccountState.Suspended)]
    [InlineData(AccountState.Deleting)]
    public async Task IDN_LIFE_003_AC9_EveryTriggerAnnouncesTheSuspensionAsync(AccountState state)
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

    /// <summary>
    /// AUTHZ-IMP-001 AC5: a takedown's suspension is announced with the acting and the
    /// effective identity of the context that executed it, each as the context gives it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_IMP_001_AC5_ATakedownsSuspensionCarriesBothIdentitiesOfItsContextAsync()
    {
        var context = AccessContext.Of(Mona);

        _ = Held(await Takedowns.ExecuteAsync(
            context,
            Browser,
            Ahmed,
            TakedownTrigger.CustomerReport,
            "a parent wrote in",
            TestContext.Current.CancellationToken));

        AccountSuspended suspended = Assert.Single(_events.Of<AccountSuspended>());

        Assert.Equal((context.Acting, context.Effective), (suspended.Actor, suspended.Effective));
    }

    /// <summary>
    /// AUTHZ-IMP-001 AC5: a takedown's reversal is announced with the acting and the
    /// effective identity of the context that reversed it, each as the context gives it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_IMP_001_AC5_AReversalCarriesBothIdentitiesOfItsContextAsync()
    {
        var context = AccessContext.Of(Mona);

        _ = Held(await ExecutedAsync());
        Held(await Takedowns.ReverseAsync(
            context,
            Browser,
            Ahmed,
            "an adult, misjudged",
            TestContext.Current.CancellationToken));

        TakedownReversed reversed = Assert.Single(_events.Of<TakedownReversed>());

        Assert.Equal((context.Acting, context.Effective), (reversed.Actor, reversed.Effective));
    }

    /// <summary>
    /// PRIV-MINOR-002 AC2: one takedown runs through its four parts apart, each leaving
    /// its mark. The trigger's transaction suspends the account into its window, writes
    /// the delivery the hosts' <c>TakedownExecuted</c> work answers and records the
    /// reason; each subscriber's completion shows as it confirms; and the erasure runs
    /// when the window ends, in a transaction of its own, with its own delivery and its
    /// own record.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_MINOR_002_AC2_ATakedownRunsItsPartsApartWithEachSubscribersCompletionVisibleAsync()
    {
        var eraser = new SubjectEraserInMemory();
        var sweep = new DeletionSweep(_accounts, eraser, _outbox, _audit, _configuration, _work, _clock);

        ExecutedTakedown takedown = Held(await ExecutedAsync());
        Delivery executed = Assert.Single(_outbox.Deliveries);

        Assert.Equal(AccountState.Deleting, _accounts.Of(Ahmed));
        Assert.Equal((Ahmed, SubjectEventKind.TakedownExecuted), (executed.Subject, executed.Kind));
        Assert.Equal(AuditActions.TakedownExecuted, Assert.Single(_audit.Entries).Action);
        Assert.Equal((1, 1), (_work.Opened, _work.Committed));
        Assert.Empty(eraser.Erased);

        _outbox.Confirms(executed, "records", Noon + TimeSpan.FromMinutes(5));

        TakedownProgress progress = Held(await ReadAsync(Mona));

        Assert.Equal(
            [
                new SubscriberConfirmation("records", true, Noon + TimeSpan.FromMinutes(5)),
                new SubscriberConfirmation("newsletter", false, null),
            ],
            progress.Subscribers);

        _clock.Advance(takedown.ErasureDue - Noon);

        Assert.Equal(
            1,
            await sweep.SweepAsync(
                AccessContext.Of(
                    SystemPrincipal.ForDeployment("expiry-sweep", "OPS-OBS-003", SystemOperation.ExpirySweep)),
                TestContext.Current.CancellationToken));

        Erasure erased = Assert.Single(eraser.Erased);

        Assert.Equal((Ahmed, ErasureReason.MinorTakedown), (erased.Subject, erased.Reason));
        Assert.Equal((2, 2), (_work.Opened, _work.Committed));
        Assert.Equal(
            [SubjectEventKind.TakedownExecuted, SubjectEventKind.ErasureRequested],
            _outbox.Deliveries.Select(delivery => delivery.Kind));
        Assert.Equal(
            [AuditActions.TakedownExecuted, AuditActions.ErasureExecuted],
            _audit.Entries.Select(entry => entry.Action));
    }

    /// <summary>
    /// REC-TKD-001 AC1: a record is taken into its window by takedown, its access
    /// sessions ended, <c>TakedownExecuted</c> written naming it and the trigger audited,
    /// and no suspension is announced, a record having no access to suspend.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_TKD_001_AC1_ARecordIsTakenDownWithNoSuspensionAsync()
    {
        _accounts.HoldRecord(Guest, AccountState.Active);

        ExecutedTakedown? takedown = await InWorkAsync(token => Takedowns.TakeDownRecordAsync(
            AccessContext.Of(Mona), Guest, TakedownTrigger.StaffReport, "seen at the counter", token));

        Delivery delivery = Assert.Single(_outbox.Deliveries);

        Assert.NotNull(takedown);
        Assert.Equal(
            (AccountState.Deleting, (DateTimeOffset?)Noon, Noon + Settings.TakedownGrace.Default),
            (_accounts.Of(Guest), _accounts.SessionsEndedAt(Guest), takedown.ErasureDue));
        Assert.Equal(
            (SubjectEventKind.TakedownExecuted, Guest, takedown.Id.Value),
            (delivery.Kind, delivery.Subject, delivery.Id.Value));
        Assert.Equal(
            (AuditActions.TakedownExecuted, Mona, Guest),
            (_audit.Entries[^1].Action, _audit.Entries[^1].Acting, _audit.Entries[^1].Subject));
        Assert.Empty(_events.Of<AccountSuspended>());
    }

    /// <summary>
    /// REC-TKD-001: a record taken down already, erased, or attached to an account is not
    /// taken down, and nothing is written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_TKD_001_ARecordTakenDownErasedOrAttachedIsNotTakenDownAsync()
    {
        SubjectId erased = new(Guid.Parse("66666666-6666-4666-8666-666666666666"));
        SubjectId attached = new(Guid.Parse("77777777-7777-4777-8777-777777777777"));
        _accounts.HoldRecord(Guest, AccountState.Active);
        _accounts.HoldRecord(erased, AccountState.Deleted);
        _accounts.HoldRecord(attached, AccountState.Active, attachedTo: Ahmed);

        _ = await InWorkAsync(token => Takedowns.TakeDownRecordAsync(
            AccessContext.Of(Mona), Guest, TakedownTrigger.StaffReport, "seen", token));

        foreach (SubjectId record in new[] { Guest, erased, attached })
        {
            Assert.Null(await InWorkAsync(token => Takedowns.TakeDownRecordAsync(
                AccessContext.Of(Mona), record, TakedownTrigger.StaffReport, "seen", token)));
        }

        Assert.Single(_outbox.Deliveries);
        Assert.Equal(AccountState.Active, _accounts.Of(attached));
    }

    /// <summary>
    /// REC-TKD-001 AC2: reversed inside its window, a record returns to what it held at
    /// the trigger, restricted here, the reversal audited with its reason and announced.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_TKD_001_AC2_AReversedRecordReturnsToWhatItHeldAsync()
    {
        _accounts.HoldRecord(Guest, AccountState.Restricted);
        _ = await InWorkAsync(token => Takedowns.TakeDownRecordAsync(
            AccessContext.Of(Mona), Guest, TakedownTrigger.CustomerReport, "a parent wrote in", token));

        _clock.Advance(Settings.TakedownGrace.Default - TimeSpan.FromMinutes(1));

        Error? refused = await Takedowns.RecordReversalRefusedAsync(Guest, TestContext.Current.CancellationToken);
        Error? reversed = await InWorkAsync(token => Takedowns.ReverseRecordTakedownAsync(
            AccessContext.Of(Mona), Guest, "an adult, misjudged", token));

        TakedownReversed announced = Assert.Single(_events.Of<TakedownReversed>());

        Assert.Equal((null, null), (refused, reversed));
        Assert.Equal(AccountState.Restricted, _accounts.Of(Guest));
        Assert.Equal(
            (AuditActions.TakedownReversed, "an adult, misjudged"),
            (_audit.Entries[^1].Action, _audit.Entries[^1].Details["reason"].GetString()));
        Assert.Equal((Guest, Mona), (announced.Subject, announced.Actor));
    }

    /// <summary>
    /// REC-TKD-001 AC2: once the takedown's window closes the reversal is refused
    /// <c>identity.takedown.windowelapsed</c>, and a record holding no takedown is
    /// answered <c>identity.takedown.notfound</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_TKD_001_AC2_AReversalAfterTheWindowIsRefusedAsync()
    {
        SubjectId untaken = new(Guid.Parse("66666666-6666-4666-8666-666666666666"));
        _accounts.HoldRecord(Guest, AccountState.Active);
        _accounts.HoldRecord(untaken, AccountState.Active);
        _ = await InWorkAsync(token => Takedowns.TakeDownRecordAsync(
            AccessContext.Of(Mona), Guest, TakedownTrigger.CustomerReport, "a parent wrote in", token));

        _clock.Advance(Settings.TakedownGrace.Default);

        Assert.Equal(
            (ErrorCodes.TakedownWindowElapsed, ErrorCodes.TakedownWindowElapsed, ErrorCodes.TakedownNotFound),
            ((await Takedowns.RecordReversalRefusedAsync(Guest, TestContext.Current.CancellationToken))?.Code,
                (await InWorkAsync(token => Takedowns.ReverseRecordTakedownAsync(
                    AccessContext.Of(Mona), Guest, "too late", token)))?.Code,
                (await Takedowns.RecordReversalRefusedAsync(untaken, TestContext.Current.CancellationToken))?.Code));
        Assert.Equal(AccountState.Deleting, _accounts.Of(Guest));
        Assert.Empty(_events.Of<TakedownReversed>());
    }

    /// <summary>
    /// REC-TKD-001: a record's takedown is read as an account's is, and one never taken
    /// down holds none to read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REC_TKD_001_ARecordsTakedownIsReadAsAnAccountsIsAsync()
    {
        SubjectId untaken = new(Guid.Parse("66666666-6666-4666-8666-666666666666"));
        _accounts.HoldRecord(Guest, AccountState.Active);
        _accounts.HoldRecord(untaken, AccountState.Active);
        ExecutedTakedown? takedown = await InWorkAsync(token => Takedowns.TakeDownRecordAsync(
            AccessContext.Of(Mona), Guest, TakedownTrigger.CustomerReport, "a parent wrote in", token));

        TakedownProgress? progress = await Takedowns.RecordProgressAsync(Guest, TestContext.Current.CancellationToken);

        Assert.Equal(takedown?.Id, progress?.Id);
        Assert.Equal(takedown?.ErasureDue, progress?.ErasureDue);
        Assert.Null(await Takedowns.RecordProgressAsync(untaken, TestContext.Current.CancellationToken));
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
    // The record's operations run in the unit of work the records area opens.
    private async ValueTask<TValue> InWorkAsync<TValue>(Func<CancellationToken, ValueTask<TValue>> operation)
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        _ = await _work.BeginAsync(token);

        TValue outcome = await operation(token);

        _ = await _work.CommitAsync(token);

        return outcome;
    }
}
