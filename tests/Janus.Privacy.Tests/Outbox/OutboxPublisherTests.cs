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
    private static readonly DateTimeOffset Noon = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

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

        Assert.Equal(1, await Publisher(_whole).PublishAsync(CancellationToken.None));

        ErasureRequested raised = Assert.IsType<ErasureRequested>(Assert.Single(host.Offered));

        Assert.Equal(Ahmed, raised.Subject);
        Assert.Equal(delivery.IdempotencyKey, raised.IdempotencyKey);
        Assert.Equal(ErasureStatus.Complete, delivery.Status);
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

        Assert.Equal(0, await Publisher(_none).PublishAsync(CancellationToken.None));
        Assert.Equal(ErasureStatus.AwaitingSubscribers, delivery.Status);
        Assert.Equal(["host"], delivery.Confirmed);

        warehouse.Confirms = true;

        Assert.Equal(1, await Publisher(_none).PublishAsync(CancellationToken.None));
        Assert.Equal(ErasureStatus.Complete, delivery.Status);
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

        await Publisher(_whole).PublishAsync(CancellationToken.None);

        Assert.Equal(ErasureStatus.AwaitingSubscribers, delivery.Status);
        Assert.Equal(1, delivery.Attempts);
        Assert.Empty(delivery.Confirmed);
        Assert.True(delivery.NextAttemptAt > Noon);
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

        Assert.Equal(0, await Publisher(_none).PublishAsync(CancellationToken.None));
        Assert.Equal(ErasureStatus.AwaitingSubscribers, delivery.Status);
        Assert.Equal(["warehouse"], delivery.Confirmed);

        host.Faults = false;

        Assert.Equal(1, await Publisher(_none).PublishAsync(CancellationToken.None));
        Assert.Equal(ErasureStatus.Complete, delivery.Status);
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

        await Publisher(_none).PublishAsync(CancellationToken.None);
        await Publisher(_none).PublishAsync(CancellationToken.None);

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

        await Publisher(_whole).PublishAsync(CancellationToken.None);

        Assert.Equal(Noon.AddSeconds(30), delivery.NextAttemptAt);

        _clock.Advance(TimeSpan.FromMinutes(1));

        await Publisher(_whole).PublishAsync(CancellationToken.None);

        Assert.Equal(Noon.AddMinutes(1).AddSeconds(60), delivery.NextAttemptAt);

        _clock.Advance(TimeSpan.FromMinutes(5));

        await Publisher(_none).PublishAsync(CancellationToken.None);

        Assert.Equal(_clock.GetUtcNow(), delivery.NextAttemptAt);
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

        Assert.Equal(0, await Publisher(_none).PublishAsync(CancellationToken.None));
        Assert.Empty(_alerts.Raised);

        Assert.Equal(1, await Publisher(_none).PublishAsync(CancellationToken.None));
        Assert.Equal(ErasureStatus.Failed, delivery.Status);

        PrivacyAlertRaised raised = Assert.Single(_alerts.Raised);

        Assert.Equal(AlertCondition.ErasureDeliveryExhausted, raised.Condition);
        Assert.Equal(
            ["host"],
            raised.Details["outstanding"].EnumerateArray().Select(name => name.GetString()));
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

        Assert.Equal(1, await Publisher(_none).PublishAsync(CancellationToken.None));
        Assert.Equal(ErasureStatus.Complete, delivery.Status);
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

        await Publisher(_none).PublishAsync(CancellationToken.None);

        Assert.Equal(1, erasure.Attempts);
        Assert.Equal(ErasureStatus.AwaitingSubscribers, erasure.Status);

        host.Confirms = true;

        await Publisher(_none).PublishAsync(CancellationToken.None);

        Assert.Equal(2, erasure.Attempts);
        Assert.Equal(ErasureStatus.Complete, erasure.Status);
        Assert.Equal(ErasureStatus.Complete, delivery.Status);
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

        Assert.Equal(0, await Publisher(_none).PublishAsync(CancellationToken.None));
        Assert.Equal(ErasureStatus.AwaitingSubscribers, delivery.Status);
        Assert.Equal(ErasureStatus.AwaitingSubscribers, erasure.Status);
        Assert.Equal(["host"], delivery.Confirmed);
        Assert.Empty(_ledger.Lines);

        _ledger.Durable = true;

        Assert.Equal(1, await Publisher(_none).PublishAsync(CancellationToken.None));
        Assert.Equal(ErasureStatus.Complete, delivery.Status);
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

        await Publisher(_none).PublishAsync(CancellationToken.None);

        host.Confirms = true;

        await Publisher(_none).PublishAsync(CancellationToken.None);

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

        await Publisher(_none).PublishAsync(CancellationToken.None);
        await Publisher(_none).PublishAsync(CancellationToken.None);

        Assert.Equal(ErasureStatus.Failed, delivery.Status);

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

        Assert.Equal(1, await Publisher(_none).PublishAsync(CancellationToken.None));
        Assert.Equal(ErasureStatus.Complete, delivery.Status);
        Assert.Equal(["host"], delivery.Confirmed);
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
