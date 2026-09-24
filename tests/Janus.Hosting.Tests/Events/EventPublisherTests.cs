using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Events;
using Janus.Authentication.Tests;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Events;

/// <summary>
/// The publisher offering each committed event to the consumers the host registered
/// for its kind (LIB-API-001, CONV-DESIGN-002, IDN-LIFE-003a).
/// </summary>
public sealed class EventPublisherTests : IAsyncDisposable
{
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
    public EventPublisherTests() =>
        _services = new ServiceCollection()
            .AddSingleton<IEventConsumer<AccountRegistered>>(_recording)
            .AddSingleton<IEventConsumer<AccountSuspended>>(_recording)
            .AddSingleton<IEventConsumer<AccountRegistered>>(_refusing)
            .BuildServiceProvider();

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
        Assert.Equal(_clock.GetUtcNow(), pending.PublishedAt);
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
            Alerts.Key(AlertCondition.Degradation, "event:AccountRegistered") + "@",
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
        (await new EventPublisher(
                _events,
                new EventConsumers(_services),
                _configuration,
                _alerts,
                _work,
                _clock,
                _randomness)
            .PublishAsync(TestContext.Current.CancellationToken))
        .Match(published => published, error => throw new InvalidOperationException(error.Code.ToString()));
}
