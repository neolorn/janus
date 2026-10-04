using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Events;
using Janus.Authentication.Tests;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Hosting.Events;
using Janus.Hosting.Tests.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Janus.Hosting.Tests.Events;

/// <summary>
/// The publisher offering each committed event to the consumers the host registered
/// for its kind (LIB-API-001, CONV-DESIGN-002, IDN-LIFE-003a).
/// </summary>
[Trait("kind", "unit")]
public sealed class EventPublisherTests : IAsyncDisposable
{
    private static readonly AccessContext Carrier = AccessContext.Of(
        SystemPrincipal.ForDeployment("events", "IDN-LIFE-003a", SystemOperation.Delivery));

    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly PendingEventsInMemory _events = new();
    private readonly EventsInMemory _alerts = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly RecordingConsumer _recording = new();
    private readonly RefusingConsumer _refusing = new();
    private readonly ServiceProvider _services;

    /// <summary>
    /// A host that registered one consumer of registrations and suspensions and another
    /// of registrations alone.
    /// </summary>
    public EventPublisherTests()
    {
        _events.Work = _work;
        _services = new ServiceCollection()
            .AddSingleton<IEventConsumer<AccountRegistered>>(_recording)
            .AddSingleton<IEventConsumer<AccountSuspended>>(_recording)
            .AddSingleton<IEventConsumer<AccountRegistered>>(_refusing)
            .BuildServiceProvider();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// CONV-DESIGN-002, LIB-API-001: a committed event reaches every consumer registered
    /// for its kind and no other, and its row is marked once they all have it; an event
    /// no consumer is registered for is marked at once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_002_AnEventReachesEveryConsumerOfItsKindAndIsMarkedAsync()
    {
        var registered = new AccountRegistered(Noon, "registered");
        var suspended = new AccountSuspended(Noon, "suspended", SuspensionOrigin.Self);

        await PublishedAsync(registered);
        await PublishedAsync(suspended);
        await PublishedAsync(new AccountReactivated(Noon, "reactivated"));

        Assert.Equal(3, await PassAsync());

        Assert.Equal(
            [registered.IdempotencyKey, suspended.IdempotencyKey],
            _recording.Received.Select(raised => raised.IdempotencyKey).Order(StringComparer.Ordinal));
        Assert.Equal(1, _refusing.Offered);
        Assert.All(_events.Held, pending => Assert.Equal(Noon, pending.PublishedAt));
    }

    /// <summary>
    /// IDN-LIFE-003a, D-162 item 29: a consumer that did not take the event leaves the
    /// row unmarked and is offered it again once the delay has passed, and the consumer
    /// that took it is not offered it twice.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_OnlyAConsumerThatRefusedIsOfferedTheEventAgainAsync()
    {
        _refusing.Refusals = 1;

        await PublishedAsync(new AccountRegistered(Noon, "registered"));

        Assert.Equal(0, await PassAsync());

        PendingEvent pending = Assert.Single(_events.Held);

        Assert.Null(pending.PublishedAt);
        Assert.Equal(1, pending.Attempts);
        Assert.Equal([typeof(RecordingConsumer).FullName], pending.Taken);

        _clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(1, await PassAsync());

        Assert.Single(_recording.Received);
        Assert.Equal(2, _refusing.Offered);
        Assert.Equal(_clock.GetUtcNow(), Assert.Single(_events.Held).PublishedAt);
    }

    /// <summary>
    /// IDN-LIFE-003a, OPS-OBS-002: an event a consumer goes on refusing, here by
    /// throwing, is failed when <c>outbox.retry.maxattempts</c> is spent and
    /// <c>degradation</c> is raised under its kind, naming the consumer still
    /// outstanding; a failed event is not offered again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_AnEventWhoseBudgetIsSpentFailsAndRaisesDegradationAsync()
    {
        _configuration.Set(Settings.OutboxRetryMaxAttempts, 2);
        _refusing.Throws = true;

        await PublishedAsync(new AccountRegistered(Noon, "registered"));

        Assert.Equal(0, await PassAsync());
        Assert.Empty(_alerts.Of<AlertRaised>());

        _clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(0, await PassAsync());

        PendingEvent pending = Assert.Single(_events.Held);

        Assert.Equal(_clock.GetUtcNow(), pending.FailedAt);

        AlertRaised raised = Assert.Single(_alerts.Of<AlertRaised>());

        Assert.Equal(AlertCondition.Degradation, raised.Condition);
        Assert.StartsWith(
            Alerts.Key(AlertCondition.Degradation, scope: null, "event:AccountRegistered") + "@",
            raised.IdempotencyKey,
            StringComparison.Ordinal);
        Assert.Equal(pending.Id.ToString(), raised.Details["event"].GetString());
        Assert.Equal("AccountRegistered", raised.Details["kind"].GetString());
        Assert.Equal(
            [typeof(RefusingConsumer).FullName],
            raised.Details["outstanding"].EnumerateArray().Select(each => each.GetString()));

        _clock.Advance(TimeSpan.FromDays(1));

        Assert.Equal(0, await PassAsync());
        Assert.Equal(2, _refusing.Offered);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC5, IDN-LIFE-003a: an event whose budget is spent and whose alert
    /// cannot be raised is refused after the unit of work began, and rolls it back, so
    /// the failure is not recorded without its alert.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC5_ASpentBudgetWhoseAlertCannotBeRaisedRollsBackAsync()
    {
        _configuration.Set(Settings.OutboxRetryMaxAttempts, 1);
        _refusing.Throws = true;

        await PublishedAsync(new AccountRegistered(Noon, "registered"));

        _alerts.Refusal = Error.From(ErrorCodes.SystemFault);
        _work.Reset();

        Result<int> passed = await PassedAsync();

        Assert.Equal(ErrorCodes.SystemFault, passed.Match(_ => default(ErrorCode?), error => error.Code));
        Assert.False(_work.Open);
        Assert.Equal(1, _work.RolledBack);
        Assert.Equal(_work.Opened, _work.Committed + _work.RolledBack);

        PendingEvent pending = Assert.Single(_events.Held);

        Assert.Null(pending.FailedAt);
        Assert.Equal(0, pending.Attempts);
        Assert.Equal(0, _events.Outcomes);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, INF-BG-001: an event row is claimed whole by one claim
    /// committed on its own, the claim is renewed before each consumer is called, each
    /// take is written as it happens and the row's outcome once, every write in a unit
    /// of work, and no consumer is called while one is open.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_AnEventIsClaimedWholeAndItsClaimRenewedBeforeEachConsumerAsync()
    {
        var open = new List<bool>();
        var renewals = new List<int>();

        _recording.Meanwhile = () =>
        {
            open.Add(_work.Open);
            renewals.Add(_events.Renewed.Count);
            _clock.Advance(TimeSpan.FromSeconds(90));
        };
        _refusing.Meanwhile = () =>
        {
            open.Add(_work.Open);
            renewals.Add(_events.Renewed.Count);
        };

        await PublishedAsync(new AccountRegistered(Noon, "registered"));

        _work.Reset();

        Assert.Equal(1, await PassAsync());

        EventClaim claim = Assert.Single(_events.Claimed);

        Assert.Equal(Noon + TimeSpan.FromMinutes(2), claim.Until);
        Assert.Equal([false, false], open);
        Assert.Equal([1, 2], renewals);
        Assert.Equal(
            [Noon + TimeSpan.FromMinutes(2), Noon + TimeSpan.FromSeconds(210)],
            _events.Renewed.Select(renewed => renewed.Until));
        Assert.Equal(
            [
                [typeof(RecordingConsumer).FullName!],
                [typeof(RecordingConsumer).FullName!, typeof(RefusingConsumer).FullName!],
            ],
            _events.Takes);
        Assert.Equal(1, _events.Outcomes);
        Assert.False(_events.WroteOutsideAUnitOfWork);
        Assert.Equal((6, 6, 0), (_work.Opened, _work.OutermostCommitted, _work.RolledBack));
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, IDN-LIFE-003a: an attempt is one pass over the consumers
    /// still to take the event. A consumer that faults leaves its take unwritten, the
    /// consumers after it are still offered the event, and the pass counts one attempt
    /// for the row.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_AnAttemptIsOnePassOverTheConsumersStillToTakeTheEventAsync()
    {
        await using ServiceProvider host = new ServiceCollection()
            .AddSingleton<IEventConsumer<AccountRegistered>>(_refusing)
            .AddSingleton<IEventConsumer<AccountRegistered>>(_recording)
            .BuildServiceProvider();

        _refusing.Throws = true;

        await PublishedAsync(new AccountRegistered(Noon, "registered"));

        Assert.Equal(0, (await PassedAsync(host)).Match(published => published, _ => -1));

        PendingEvent pending = Assert.Single(_events.Held);

        Assert.Equal(1, _refusing.Offered);
        Assert.Single(_recording.Received);
        Assert.Equal(1, pending.Attempts);
        Assert.Equal([typeof(RecordingConsumer).FullName], pending.Taken);
        Assert.Equal(1, _events.Outcomes);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9: a pass stops where the renewal of its claim changes nothing.
    /// A row another pass took over while a consumer ran is that pass's: no further
    /// consumer is offered the event and no outcome is written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_APassStopsWhereTheRenewalOfItsClaimChangesNothingAsync()
    {
        await using ServiceProvider host = new ServiceCollection()
            .AddSingleton<IEventConsumer<AccountRegistered>>(_refusing)
            .AddSingleton<IEventConsumer<AccountRegistered>>(_recording)
            .BuildServiceProvider();

        _refusing.Refusals = 1;
        _refusing.Meanwhile = () =>
            _events.TakeOver(_events.Held[0].Id, Noon + TimeSpan.FromMinutes(10));

        await PublishedAsync(new AccountRegistered(Noon, "registered"));

        Assert.Equal(0, (await PassedAsync(host)).Match(published => published, _ => -1));

        PendingEvent pending = Assert.Single(_events.Held);

        Assert.Equal(1, _refusing.Offered);
        Assert.Empty(_recording.Received);
        Assert.Single(_events.Renewed);
        Assert.Equal(0, _events.Outcomes);
        Assert.Equal(0, pending.Attempts);
        Assert.Empty(pending.Taken);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9: a take and an outcome are written only under the claim. A
    /// consumer that took the event while another pass took the row over has its take
    /// left unwritten by this pass, which stops and writes no outcome.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_ATakeWhoseClaimWasTakenOverIsNotWrittenAsync()
    {
        _recording.Meanwhile = () =>
            _events.TakeOver(_events.Held[0].Id, Noon + TimeSpan.FromMinutes(10));

        await PublishedAsync(new AccountRegistered(Noon, "registered"));

        Assert.Equal(0, await PassAsync());

        PendingEvent pending = Assert.Single(_events.Held);

        Assert.Single(_recording.Received);
        Assert.Equal(0, _refusing.Offered);
        Assert.Empty(_events.Takes);
        Assert.Empty(pending.Taken);
        Assert.Equal(0, _events.Outcomes);
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, INF-BG-001: a row another pass holds is not carried, and
    /// one whose claim has timed out is carried by the next pass.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_AnEventAnotherPassHoldsIsCarriedOnlyOnceItsClaimTimesOutAsync()
    {
        await PublishedAsync(new AccountSuspended(Noon, "suspended", SuspensionOrigin.Self));

        _events.TakeOver(_events.Held[0].Id, Noon + TimeSpan.FromMinutes(2));

        Assert.Equal(0, await PassAsync());
        Assert.Empty(_recording.Received);
        Assert.Empty(_events.Claimed);

        _clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(1, await PassAsync());
        Assert.Single(_recording.Received);
    }

    /// <summary>
    /// CONV-DESIGN-003: a consumer still running when the claim times out is abandoned
    /// as one that did not take the event, and the pass counts the attempt.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AConsumerStillRunningWhenTheClaimTimesOutIsAbandonedAsync()
    {
        _recording.Stalls = true;

        await PublishedAsync(new AccountSuspended(Noon, "suspended", SuspensionOrigin.Self));

        Assert.Equal(0, (await PassedAsync(clock: new LapsedClock(Noon))).Match(published => published, _ => -1));

        PendingEvent pending = Assert.Single(_events.Held);

        Assert.Equal(1, pending.Attempts);
        Assert.Empty(pending.Taken);
        Assert.Null(pending.PublishedAt);
    }

    /// <summary>
    /// CONV-DESIGN-002 and LIB-EXT-001: event publication is not a default a host
    /// replaces, so an <c>IEvents</c> the host registered before <c>AddJanus</c> is not
    /// what the library publishes through: the publication writes its row, and the
    /// host's is never called.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_002_AnIEventsTheHostRegisteredFirstDoesNotBypassTheRowAsync()
    {
        var bypassing = new EventsInMemory();

        IServiceCollection services = new ServiceCollection()
            .AddSingleton<IEvents>(bypassing)
            .AddJanus("Host=nowhere.invalid;Database=identity", HostFixture.Declaration(), ApplicationKind.Public);

        // The events table and the transaction, over the area's fakes, so the row the
        // library writes is read here without a database.
        services.Replace(ServiceDescriptor.Singleton<IPendingEvents>(_events));
        services.Replace(ServiceDescriptor.Singleton<IUnitOfWork>(_work));

        await using ServiceProvider deployed = services.BuildServiceProvider();
        await using AsyncServiceScope scope = deployed.CreateAsyncScope();

        var registered = new AccountRegistered(Noon, "registered");

        Result published = await scope.ServiceProvider.GetRequiredService<IEvents>()
            .PublishAsync(registered, TestContext.Current.CancellationToken);

        Assert.Null(published.Match(() => (Error?)null, error => error));
        Assert.Equal(registered.IdempotencyKey, Assert.Single(_events.Held).Raised.IdempotencyKey);
        Assert.Empty(bypassing.Published);
    }

    /// <summary>
    /// LIB-API-001: every event the library emits can be offered to the consumers of its
    /// kind. An event the library adds without teaching the publisher its kind fails
    /// here.
    /// </summary>
    [Fact]
    public void LIB_API_001_EveryEmittedEventHasItsConsumers()
    {
        var consumers = new EventConsumers(_services);

        foreach (Type kind in typeof(DomainEvent).Assembly.GetExportedTypes()
            .Where(type => type.IsSubclassOf(typeof(DomainEvent)) && !type.IsAbstract))
        {
            var raised = (DomainEvent)RuntimeHelpers.GetUninitializedObject(kind);

            Assert.Equal(
                kind == typeof(AccountRegistered) ? 2 : kind == typeof(AccountSuspended) ? 1 : 0,
                consumers.Of(raised).Count);
        }
    }

    private async Task PublishedAsync(DomainEvent raised) =>
        Assert.True((await new EventOutbox(_events, _work)
                .PublishAsync(raised, TestContext.Current.CancellationToken))
            .Match(() => true, _ => false));

    private async Task<int> PassAsync() =>
        (await PassedAsync())
        .Match(published => published, error => throw new InvalidOperationException(error.Code.ToString()));

    private ValueTask<Result<int>> PassedAsync(IServiceProvider? host = null, TimeProvider? clock = null) =>
        new EventPublisher(
                _events,
                new EventConsumers(host ?? _services),
                _configuration,
                _alerts,
                _work,
                clock ?? _clock,
                _randomness)
            .PublishAsync(Carrier, TestContext.Current.CancellationToken);
}
