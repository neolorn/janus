using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Erasures;
using Janus.Privacy.Outbox;
using Janus.Privacy.Policies;
using Janus.Privacy.Takedowns;
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
    private readonly SubscriberInMemory _orders = new("orders", required: true);
    private readonly SubscriberInMemory _newsletter = new("newsletter", required: false);
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly EventsInMemory _events = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// A deployment with one customer and one member of staff who holds
    /// <c>takedown:execute</c>.
    /// </summary>
    public TakedownServiceTests()
    {
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
            [_orders, _newsletter],
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
            [new SubscriberConfirmation("orders", true, null), new SubscriberConfirmation("newsletter", false, null)],
            before.Subscribers);

        _outbox.Confirms(Assert.Single(_outbox.Deliveries), "orders", Noon + TimeSpan.FromMinutes(5));

        TakedownProgress after = Held(await ReadAsync(Mona));

        Assert.Equal(
            Noon + TimeSpan.FromMinutes(5),
            Assert.Single(after.Subscribers, subscriber => subscriber.Name == "orders").ConfirmedAt);
        Assert.Null(Assert.Single(after.Subscribers, subscriber => subscriber.Name == "newsletter").ConfirmedAt);
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
    /// IDN-LIFE-003: an account already in a deletion window of another origin, or
    /// already erased, is not taken down.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AnAccountAlreadyLeavingIsNotTakenDownAsync()
    {
        _accounts.Deletes(Ahmed, DeletionOrigin.Self, Noon - TimeSpan.FromDays(1));

        Assert.Equal(ErrorCodes.Denied, Refused(await ExecutedAsync()).Code);

        _accounts.Erases(Ahmed);

        Assert.Equal(ErrorCodes.Denied, Refused(await ExecutedAsync()).Code);
        Assert.Empty(_outbox.Deliveries);
        Assert.Empty(_audit.Entries);
        Assert.Null(_accounts.SessionsEndedAt(Ahmed));
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
    /// window, or not leaving at all, is refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_OnlyATakedownIsReversedAsync()
    {
        Assert.Equal(ErrorCodes.Denied, Refused(await ReversedAsync()).Code);

        _accounts.Deletes(Ahmed, DeletionOrigin.Self, Noon);

        Assert.Equal(ErrorCodes.Denied, Refused(await ReversedAsync()).Code);
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
    /// 09 section 8a: an account that was never taken down has no progress to read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AnAccountNeverTakenDownHasNoProgressAsync() =>
        Assert.Equal(ErrorCodes.TakedownNotFound, Refused(await ReadAsync(Mona)).Code);

    /// <summary>
    /// CONV-DESIGN-002: the announcement follows the commit, so a consumer that will
    /// not take it is answered to the caller and the takedown stands.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AnUnannouncedTriggerStillStandsAsync()
    {
        _events.Refusal = Error.From(ErrorCodes.SystemFault);

        Assert.Equal(ErrorCodes.SystemFault, Refused(await ExecutedAsync()).Code);
        Assert.Equal(AccountState.Deleting, _accounts.Of(Ahmed));
        Assert.Equal(1, _work.Committed);
        Assert.Single(_outbox.Deliveries);
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
