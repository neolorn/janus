using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Erasures;
using Janus.Privacy.Outbox;
using Janus.Privacy.Tests.Erasures;
using Xunit;

namespace Janus.Privacy.Tests.Outbox;

/// <summary>
/// One pass of the outbox worker: who is offered the event, who is offered it again,
/// and what happens when the budget runs out (IDN-LIFE-003a, PRIV-RIGHT-005b).
/// </summary>
[Trait("kind", "unit")]
public sealed class OutboxPublisherTests : IAsyncDisposable
{
    private static readonly AccessContext Carrier = AccessContext.Of(
        SystemPrincipal.ForDeployment("outbox", "IDN-LIFE-003a", SystemOperation.Delivery));

    private static readonly DateTimeOffset Noon = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private static readonly SubjectId Mona =
        new(Guid.Parse("22222222-2222-4222-8222-222222222222"));

    private readonly OutboxStoreInMemory _outbox = new();
    private readonly ErasureStoreInMemory _erasures = new();
    private readonly List<ISubjectEventSubscriber> _subscribers = [];
    private readonly ConfigurationInMemory _configuration = new();
    private readonly PrivacyAlertsInMemory _alerts = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly FixedRandomness _whole = new(0xFF, 0xFF);
    private readonly FixedRandomness _none = new(0x00);
    private ErasureLedgerInMemory? _ledger;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _whole.Dispose();
        _none.Dispose();

        await _work.DisposeAsync();
    }

    /// <summary>
    /// IDN-LIFE-003a AC6: a handler the library has never heard of is offered the
    /// event by name alone, and what it is offered is the fact and its key.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_AC6_ASubscriberTheLibraryNeverNamedReceivesTheEventAsync()
    {
        var host = new SubscriberInMemory("host", required: true);

        _subscribers.Add(host);

        Delivery delivery = await RaisedAsync(SubjectEventKind.ErasureRequested);

        Assert.Equal(1, await Publisher(_whole).PublishAsync(Carrier, CancellationToken.None));

        ErasureRequested raised = Assert.IsType<ErasureRequested>(Assert.Single(host.Offered));

        Assert.Equal(Ahmed, raised.Subject);
        Assert.Equal(delivery.IdempotencyKey, raised.IdempotencyKey);
        Assert.Equal(ErasureStatus.Complete, _outbox.Held(delivery.Id).Status);
    }

    /// <summary>
    /// PRIV-RIGHT-005b AC1: the delivery stays open while one required subscriber
    /// has not confirmed, and closes when it does.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005b_AC1_TheDeliveryStaysOpenUntilEveryRequiredSubscriberConfirmsAsync()
    {
        var host = new SubscriberInMemory("host", required: true);
        var warehouse = new SubscriberInMemory("warehouse", required: true) { Confirms = false };

        _subscribers.Add(host);
        _subscribers.Add(warehouse);

        Delivery delivery = await RaisedAsync(SubjectEventKind.ErasureRequested);

        Assert.Equal(0, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Equal(ErasureStatus.AwaitingSubscribers, _outbox.Held(delivery.Id).Status);
        Assert.Equal(["host"], _outbox.Held(delivery.Id).Confirmed);

        warehouse.Confirms = true;

        Assert.Equal(1, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Equal(ErasureStatus.Complete, _outbox.Held(delivery.Id).Status);
    }

    /// <summary>
    /// PRIV-RIGHT-005b AC2: a subscriber that answered nothing leaves the delivery
    /// outstanding with its attempt counted, never quietly done.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005b_AC2_AnUnansweredDeliveryRetriesRatherThanBeingMarkedDoneAsync()
    {
        _subscribers.Add(new SubscriberInMemory("host", required: true) { Confirms = false });

        Delivery delivery = await RaisedAsync(SubjectEventKind.RestrictionChanged);

        await Publisher(_whole).PublishAsync(Carrier, CancellationToken.None);

        Assert.Equal(ErasureStatus.AwaitingSubscribers, _outbox.Held(delivery.Id).Status);
        Assert.Equal(1, _outbox.Held(delivery.Id).Attempts);
        Assert.Empty(_outbox.Held(delivery.Id).Confirmed);
        Assert.True(_outbox.Held(delivery.Id).NextAttemptAt > Noon);
    }

    /// <summary>
    /// PRIV-RIGHT-005b AC2: a handler whose own store fails it is a handler that did
    /// not confirm, so the pass finishes and the delivery is offered again.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005b_AC2_ASubscriberThatFaultsIsRetriedRatherThanConfirmedAsync()
    {
        var host = new SubscriberInMemory("host", required: true) { Faults = true };
        var warehouse = new SubscriberInMemory("warehouse", required: true);

        _subscribers.Add(host);
        _subscribers.Add(warehouse);

        Delivery delivery = await RaisedAsync(SubjectEventKind.ErasureRequested);

        Assert.Equal(0, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Equal(ErasureStatus.AwaitingSubscribers, _outbox.Held(delivery.Id).Status);
        Assert.Equal(["warehouse"], _outbox.Held(delivery.Id).Confirmed);

        host.Faults = false;

        Assert.Equal(1, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Equal(ErasureStatus.Complete, _outbox.Held(delivery.Id).Status);
    }

    /// <summary>
    /// IDN-LIFE-003a AC3: a subscriber that confirmed is not offered the event a
    /// second time, and the one that did not is offered the key it saw before, so
    /// handling the repeat produces the result the first attempt would have.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_AC3_ASubscriberThatConfirmedIsNotOfferedTheEventAgainAsync()
    {
        var host = new SubscriberInMemory("host", required: true);
        var warehouse = new SubscriberInMemory("warehouse", required: true) { Confirms = false };

        _subscribers.Add(host);
        _subscribers.Add(warehouse);

        Delivery delivery = await RaisedAsync(SubjectEventKind.ErasureRequested);

        await Publisher(_none).PublishAsync(Carrier, CancellationToken.None);
        await Publisher(_none).PublishAsync(Carrier, CancellationToken.None);

        Assert.Single(host.Offered);
        Assert.Equal(2, warehouse.Offered.Count);
        Assert.All(
            warehouse.Offered,
            raised => Assert.Equal(delivery.IdempotencyKey, raised.IdempotencyKey));
    }

    /// <summary>
    /// IDN-LIFE-003a: the delay grows by the factor on each attempt, and full jitter
    /// takes a fraction of it, so two deliveries failing together do not retry
    /// together.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_TheRetryDelayGrowsByTheFactorWithFullJitterAsync()
    {
        _subscribers.Add(new SubscriberInMemory("host", required: true) { Confirms = false });
        _configuration.Set(Settings.OutboxRetryInitial, TimeSpan.FromSeconds(30));
        _configuration.Set(Settings.OutboxRetryFactor, 2.0m);

        Delivery delivery = await RaisedAsync(SubjectEventKind.ErasureRequested);

        await Publisher(_whole).PublishAsync(Carrier, CancellationToken.None);

        Assert.Equal(Noon.AddSeconds(30), _outbox.Held(delivery.Id).NextAttemptAt);

        _clock.Advance(TimeSpan.FromMinutes(1));

        await Publisher(_whole).PublishAsync(Carrier, CancellationToken.None);

        Assert.Equal(Noon.AddMinutes(1).AddSeconds(60), _outbox.Held(delivery.Id).NextAttemptAt);

        _clock.Advance(TimeSpan.FromMinutes(5));

        await Publisher(_none).PublishAsync(Carrier, CancellationToken.None);

        Assert.Equal(_clock.GetUtcNow(), _outbox.Held(delivery.Id).NextAttemptAt);
    }

    /// <summary>
    /// IDN-LIFE-003a AC4: the pass that spends the last attempt fails the delivery
    /// and raises the condition in the same breath, naming who is outstanding.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_AC4_ASpentRetryBudgetAlertsImmediatelyAsync()
    {
        _subscribers.Add(new SubscriberInMemory("host", required: true) { Confirms = false });
        _configuration.Set(Settings.OutboxRetryMaxAttempts, 2);

        Delivery delivery = await RaisedAsync(SubjectEventKind.ErasureRequested);

        Assert.Equal(0, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Empty(_alerts.Raised);

        Assert.Equal(1, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Equal(ErasureStatus.Failed, _outbox.Held(delivery.Id).Status);

        PrivacyAlertRaised raised = Assert.Single(_alerts.Raised);

        Assert.Equal(AlertCondition.ErasureDeliveryExhausted, raised.Condition);
        Assert.Equal(
            ["host"],
            raised.Details["outstanding"].EnumerateArray().Select(name => name.GetString()));
    }

    /// <summary>
    /// CONV-DESIGN-002, IDN-LIFE-003a AC4: the exhaustion's row is written in the
    /// transaction that records the failed delivery, so a row that cannot be written
    /// fails the pass before the failure is committed.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CONV_DESIGN_002_AnExhaustionThatCannotBeRaisedCommitsNothingAsync()
    {
        _subscribers.Add(new SubscriberInMemory("host", required: true) { Confirms = false });
        _configuration.Set(Settings.OutboxRetryMaxAttempts, 1);

        _ = await RaisedAsync(SubjectEventKind.ErasureRequested);

        _alerts.Refusal = Error.From(ErrorCodes.SystemFault);
        _work.Reset();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));

        Assert.False(_work.Open);
        Assert.Equal(1, _work.RolledBack);
        Assert.Equal(_work.Opened, _work.Committed + _work.RolledBack);
        Assert.Empty(_alerts.Raised);
    }

    /// <summary>
    /// IDN-LIFE-003a: an optional subscriber that did not confirm holds nothing
    /// open, so the delivery closes on what the required ones did.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_AnOptionalSubscriberFailingDoesNotHoldTheDeliveryOpenAsync()
    {
        _subscribers.Add(new SubscriberInMemory("host", required: true));
        _subscribers.Add(new SubscriberInMemory("analytics", required: false) { Confirms = false });

        Delivery delivery = await RaisedAsync(SubjectEventKind.RestrictionChanged);

        Assert.Equal(1, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Equal(ErasureStatus.Complete, _outbox.Held(delivery.Id).Status);
    }

    /// <summary>
    /// IDN-LIFE-003b: the erasure row says how far the host-side work has got, so it
    /// counts the attempts the delivery counted and completes when it completes.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_LIFE_003b_TheErasureRowFollowsItsDeliveryToCompleteAsync()
    {
        var host = new SubscriberInMemory("host", required: true) { Confirms = false };

        _subscribers.Add(host);

        Delivery delivery = await RaisedAsync(SubjectEventKind.ErasureRequested);
        var erasure = Erasure.Begun(Ahmed, Noon, ErasureReason.ErasureRequest);

        _erasures.Add(erasure);

        await Publisher(_none).PublishAsync(Carrier, CancellationToken.None);

        Assert.Equal(1, erasure.Attempts);
        Assert.Equal(ErasureStatus.AwaitingSubscribers, erasure.Status);

        host.Confirms = true;

        await Publisher(_none).PublishAsync(Carrier, CancellationToken.None);

        Assert.Equal(2, erasure.Attempts);
        Assert.Equal(ErasureStatus.Complete, erasure.Status);
        Assert.Equal(ErasureStatus.Complete, _outbox.Held(delivery.Id).Status);
    }

    /// <summary>
    /// DR-016 AC2: while the ledger cannot take the line the erasure stays outstanding,
    /// with every host subscriber confirmed and its row still awaiting; the pass that
    /// makes the line durable is the one that completes it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task DR_016_AC2_AnErasureIsNotCompleteUntilItsLineIsDurableAsync()
    {
        _subscribers.Add(new SubscriberInMemory("host", required: true));
        _ledger = new ErasureLedgerInMemory { Durable = false };

        Delivery delivery = await RaisedAsync(SubjectEventKind.ErasureRequested);
        var erasure = Erasure.Begun(Ahmed, Noon, ErasureReason.ErasureRequest);

        _erasures.Add(erasure);

        Assert.Equal(0, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Equal(ErasureStatus.AwaitingSubscribers, _outbox.Held(delivery.Id).Status);
        Assert.Equal(ErasureStatus.AwaitingSubscribers, erasure.Status);
        Assert.Equal(["host"], _outbox.Held(delivery.Id).Confirmed);
        Assert.Empty(_ledger.Lines);

        _ledger.Durable = true;

        Assert.Equal(1, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Equal(ErasureStatus.Complete, _outbox.Held(delivery.Id).Status);
        Assert.Equal(ErasureStatus.Complete, erasure.Status);
        Assert.Single(_ledger.Lines);
    }

    /// <summary>
    /// DR-016, DR-016 AC4: the line is the instant to the second, the subject
    /// identifier and the reason in the spelling of chapter 10 section 5.12a, one space
    /// apart, and nothing else; it is written once however many passes the host's
    /// subscribers take.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task DR_016_AC4_TheLineHoldsTheInstantTheSubjectAndTheReasonAndNothingElseAsync()
    {
        var host = new SubscriberInMemory("host", required: true) { Confirms = false };

        _subscribers.Add(host);
        _ledger = new ErasureLedgerInMemory();

        await _outbox.AddAsync(
            Delivery.Of(
                Ahmed,
                SubjectEventKind.ErasureRequested,
                Noon.AddMilliseconds(-250),
                reason: ErasureReason.MinorTakedown),
            CancellationToken.None);

        await Publisher(_none).PublishAsync(Carrier, CancellationToken.None);

        host.Confirms = true;

        await Publisher(_none).PublishAsync(Carrier, CancellationToken.None);

        Assert.Equal(
            ["2026-09-20T11:59:59Z 11111111-1111-4111-8111-111111111111 minor-takedown"],
            _ledger.Lines);
    }

    /// <summary>
    /// DR-016 AC2, IDN-LIFE-003a AC4: a ledger that never takes the line spends the
    /// erasure's budget like any required subscriber, and the alert names it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task DR_016_AC2_ALedgerThatNeverTakesTheLineIsRaisedWhenTheBudgetIsSpentAsync()
    {
        _subscribers.Add(new SubscriberInMemory("host", required: true));
        _ledger = new ErasureLedgerInMemory { Durable = false };
        _configuration.Set(Settings.OutboxRetryMaxAttempts, 2);

        Delivery delivery = await RaisedAsync(SubjectEventKind.ErasureRequested);

        await Publisher(_none).PublishAsync(Carrier, CancellationToken.None);
        await Publisher(_none).PublishAsync(Carrier, CancellationToken.None);

        Assert.Equal(ErasureStatus.Failed, _outbox.Held(delivery.Id).Status);

        PrivacyAlertRaised raised = Assert.Single(_alerts.Raised);

        Assert.Equal(AlertCondition.ErasureDeliveryExhausted, raised.Condition);
        Assert.Equal(
            ["erasure-ledger"],
            raised.Details["outstanding"].EnumerateArray().Select(name => name.GetString()));
    }

    /// <summary>
    /// DR-016: only an erasure is written down; a restriction closes on its host
    /// subscribers with the ledger unreachable and leaves no line.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task DR_016_OnlyAnErasureWaitsForTheLedgerAsync()
    {
        _subscribers.Add(new SubscriberInMemory("host", required: true));
        _ledger = new ErasureLedgerInMemory { Durable = false };

        Delivery delivery = await RaisedAsync(SubjectEventKind.RestrictionChanged);

        Assert.Equal(1, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Equal(ErasureStatus.Complete, _outbox.Held(delivery.Id).Status);
        Assert.Equal(["host"], _outbox.Held(delivery.Id).Confirmed);
    }

    /// <summary>
    /// DR-016 AC5: an erasure completed before the ledger was registered, whether its
    /// subscribers confirmed it or an operator closed it by hand, is written down once
    /// the ledger is, oldest first; its status, its attempts and its erasures row stand
    /// as they were, a refused append leaves it for the next pass, and a later pass
    /// writes nothing again.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task DR_016_AnErasureCompletedBeforeTheLedgerWasRegisteredIsAppendedOnceAsync()
    {
        _subscribers.Add(new SubscriberInMemory("host", required: true));

        var confirmed = Delivery.Of(Ahmed, SubjectEventKind.ErasureRequested, Noon.AddDays(-2), reason: ErasureReason.ErasureRequest);
        var closed = Delivery.Of(Mona, SubjectEventKind.ErasureRequested, Noon.AddDays(-3), reason: ErasureReason.MinorTakedown);
        var row = Erasure.Begun(Ahmed, Noon.AddDays(-2), ErasureReason.ErasureRequest);

        await _outbox.AddAsync(confirmed, CancellationToken.None);
        _erasures.Add(row);

        Assert.Equal(1, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));

        closed.Fail();
        closed.CompleteManually();
        await _outbox.AddAsync(closed, CancellationToken.None);

        int attempts = _outbox.Held(confirmed.Id).Attempts;

        _ledger = new ErasureLedgerInMemory { Durable = false };

        Assert.Equal(0, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Empty(_ledger.Lines);
        Assert.DoesNotContain("erasure-ledger", _outbox.Held(confirmed.Id).Confirmed);

        _ledger.Durable = true;

        Assert.Equal(0, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Equal(0, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));

        Assert.Equal(
            [
                "2026-09-17T12:00:00Z 22222222-2222-4222-8222-222222222222 minor-takedown",
                "2026-09-18T12:00:00Z 11111111-1111-4111-8111-111111111111 erasure-request",
            ],
            _ledger.Lines);
        Assert.Contains("erasure-ledger", _outbox.Held(confirmed.Id).Confirmed);
        Assert.Contains("erasure-ledger", _outbox.Held(closed.Id).Confirmed);
        Assert.Equal((ErasureStatus.Complete, attempts), (_outbox.Held(confirmed.Id).Status, _outbox.Held(confirmed.Id).Attempts));
        Assert.Equal(ErasureStatus.Complete, _outbox.Held(closed.Id).Status);
        Assert.Equal(ErasureStatus.Complete, row.Status);
        Assert.Equal(attempts, row.Attempts);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, INF-BG-001: an outbox row is claimed whole by one claim
    /// committed on its own, the claim is renewed before each subscriber is called, the
    /// erasure ledger's line among them, each confirmation is written as it happens and
    /// the row's outcome once, every write in a unit of work, and no subscriber is called
    /// while one is open.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_ADeliveryIsClaimedWholeAndItsClaimRenewedBeforeEachSubscriberAsync()
    {
        var open = new List<bool>();
        var renewals = new List<int>();
        var host = new SubscriberInMemory("host", required: true);
        var warehouse = new SubscriberInMemory("warehouse", required: true);

        host.Meanwhile = () =>
        {
            open.Add(_work.Open);
            renewals.Add(_outbox.Renewed.Count);
            _clock.Advance(TimeSpan.FromSeconds(90));
        };
        warehouse.Meanwhile = () =>
        {
            open.Add(_work.Open);
            renewals.Add(_outbox.Renewed.Count);
        };

        _subscribers.Add(host);
        _subscribers.Add(warehouse);
        _ledger = new ErasureLedgerInMemory();
        _outbox.Work = _work;

        Delivery delivery = await RaisedAsync(SubjectEventKind.ErasureRequested);

        _work.Reset();

        Assert.Equal(1, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));

        DeliveryClaim claim = Assert.Single(_outbox.Claimed);

        Assert.Equal(Noon + TimeSpan.FromMinutes(2), claim.Until);
        Assert.Equal([false, false], open);
        Assert.Equal([2, 3], renewals);
        Assert.Equal(
            [Noon + TimeSpan.FromMinutes(2), Noon + TimeSpan.FromMinutes(2), Noon + TimeSpan.FromSeconds(210)],
            _outbox.Renewed.Select(renewed => renewed.Until));
        Assert.Equal(["erasure-ledger", "host", "warehouse"], _outbox.Confirmations);
        Assert.Single(_ledger.Lines);
        Assert.Equal(1, _outbox.Outcomes);
        Assert.False(_outbox.WroteOutsideAUnitOfWork);
        Assert.Equal((8, 8, 0), (_work.Opened, _work.Committed, _work.RolledBack));
        Assert.Equal((ErasureStatus.Complete, 1), (_outbox.Held(delivery.Id).Status, _outbox.Held(delivery.Id).Attempts));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, IDN-LIFE-003a: an attempt is one pass over the subscribers
    /// still to confirm. A subscriber that faults leaves its delivery unconfirmed, the
    /// subscribers after it are still offered the event, and the pass counts one attempt
    /// for the row and for its erasure.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_AnAttemptIsOnePassOverTheSubscribersStillToConfirmAsync()
    {
        var host = new SubscriberInMemory("host", required: true) { Faults = true };
        var warehouse = new SubscriberInMemory("warehouse", required: true);
        var analytics = new SubscriberInMemory("analytics", required: false) { Confirms = false };

        _subscribers.Add(host);
        _subscribers.Add(warehouse);
        _subscribers.Add(analytics);

        Delivery delivery = await RaisedAsync(SubjectEventKind.ErasureRequested);
        var erasure = Erasure.Begun(Ahmed, Noon, ErasureReason.ErasureRequest);

        _erasures.Add(erasure);

        Assert.Equal(0, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));

        Assert.Single(host.Offered);
        Assert.Single(warehouse.Offered);
        Assert.Single(analytics.Offered);
        Assert.Equal(["warehouse"], _outbox.Confirmations);
        Assert.Equal(1, _outbox.Held(delivery.Id).Attempts);
        Assert.Equal(1, erasure.Attempts);
        Assert.Equal(1, _outbox.Outcomes);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9: a pass stops where the renewal of its claim changes nothing.
    /// A row another pass took over while a subscriber ran is that pass's: no further
    /// subscriber is offered the event, and no attempt, status or alert is written.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_APassStopsWhereTheRenewalOfItsClaimChangesNothingAsync()
    {
        var host = new SubscriberInMemory("host", required: true) { Confirms = false };
        var warehouse = new SubscriberInMemory("warehouse", required: true);

        _subscribers.Add(host);
        _subscribers.Add(warehouse);
        _configuration.Set(Settings.OutboxRetryMaxAttempts, 1);

        Delivery delivery = await RaisedAsync(SubjectEventKind.RestrictionChanged);

        host.Meanwhile = () => _outbox.TakeOver(delivery.Id, Noon + TimeSpan.FromMinutes(10));

        Assert.Equal(0, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));

        Assert.Single(host.Offered);
        Assert.Empty(warehouse.Offered);
        Assert.Single(_outbox.Renewed);
        Assert.Equal(0, _outbox.Outcomes);
        Assert.Equal((ErasureStatus.AwaitingSubscribers, 0), (_outbox.Held(delivery.Id).Status, _outbox.Held(delivery.Id).Attempts));
        Assert.Empty(_alerts.Raised);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9: a confirmation and an outcome are written only under the
    /// claim. A subscriber that confirmed while another pass took the row over has its
    /// confirmation left unwritten by this pass, which stops and writes no outcome.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_AConfirmationWhoseClaimWasTakenOverIsNotWrittenAsync()
    {
        var host = new SubscriberInMemory("host", required: true);
        var warehouse = new SubscriberInMemory("warehouse", required: true);

        _subscribers.Add(host);
        _subscribers.Add(warehouse);

        Delivery delivery = await RaisedAsync(SubjectEventKind.RestrictionChanged);

        host.Meanwhile = () => _outbox.TakeOver(delivery.Id, Noon + TimeSpan.FromMinutes(10));

        Assert.Equal(0, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));

        Assert.Single(host.Offered);
        Assert.Empty(warehouse.Offered);
        Assert.Empty(_outbox.Confirmations);
        Assert.Empty(_outbox.Held(delivery.Id).Confirmed);
        Assert.Equal(0, _outbox.Outcomes);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, INF-BG-001: a row another pass holds is not carried, and one
    /// whose claim has timed out is carried by the next pass.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_ADeliveryAnotherPassHoldsIsCarriedOnlyOnceItsClaimTimesOutAsync()
    {
        var host = new SubscriberInMemory("host", required: true);

        _subscribers.Add(host);

        Delivery delivery = await RaisedAsync(SubjectEventKind.RestrictionChanged);

        _outbox.TakeOver(delivery.Id, Noon + TimeSpan.FromMinutes(2));

        Assert.Equal(0, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Empty(host.Offered);
        Assert.Empty(_outbox.Claimed);

        _clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(1, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Single(host.Offered);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, DR-016 AC5: a completed erasure another pass holds for its
    /// line is not written down by this one, so of two passes one appends the line; the
    /// pass that does confirms it under its claim and releases the claim.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_ACompletedErasureAnotherPassHoldsIsNotWrittenDownTwiceAsync()
    {
        var completed = Delivery.Of(Ahmed, SubjectEventKind.ErasureRequested, Noon.AddDays(-2));

        completed.Complete();
        await _outbox.AddAsync(completed, CancellationToken.None);

        _ledger = new ErasureLedgerInMemory();
        _outbox.Work = _work;
        _outbox.TakeOver(completed.Id, Noon + TimeSpan.FromMinutes(2));

        Assert.Equal(0, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Empty(_ledger.Lines);
        Assert.Empty(_outbox.Claimed);

        _clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(0, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Single(_ledger.Lines);
        Assert.Single(_outbox.Claimed);
        Assert.Equal(["erasure-ledger"], _outbox.Confirmations);
        Assert.False(_outbox.WroteOutsideAUnitOfWork);

        Assert.Equal(0, await Publisher(_none).PublishAsync(Carrier, CancellationToken.None));
        Assert.Single(_ledger.Lines);
        Assert.Single(_outbox.Claimed);
    }

    private async ValueTask<Delivery> RaisedAsync(SubjectEventKind kind)
    {
        var delivery = Delivery.Of(Ahmed, kind, Noon);

        await _outbox.AddAsync(delivery, CancellationToken.None);

        return delivery;
    }

    private OutboxPublisher Publisher(RandomNumberGenerator randomness) =>
        new(
            _outbox,
            _erasures,
            _subscribers,
            _ledger,
            _configuration,
            _alerts,
            _work,
            _clock,
            randomness);
}
