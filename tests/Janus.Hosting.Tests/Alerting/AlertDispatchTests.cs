using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Alerting;

/// <summary>
/// <c>alert-dispatch</c> handing what committed transactions raised to the host's alert
/// handler, oldest first, each once, and again until it is taken (OPS-ALERT-001,
/// OPS-ALERT-002, OPS-ALERT-003, CONV-DESIGN-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class AlertDispatchTests : IAsyncDisposable
{
    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment whose host registered an alert handler.
    /// </summary>
    public AlertDispatchTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// OPS-ALERT-003 AC2 and OPS-ALERT-001 AC1: a raised condition reaches the host's
    /// handler with nobody watching, once, oldest first, and leaves nothing waiting.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_003_AC2_ARaisedAlertIsHandedToTheHandlerOnceAsync()
    {
        await RaisedAsync(AlertCondition.RestrictionGranted, "first");

        _deployment.Clock.Advance(TimeSpan.FromSeconds(1));

        await RaisedAsync(AlertCondition.RestrictionLoosened, "second");

        Assert.Equal(2, await _deployment.CarryAlertsAsync());
        Assert.Equal(0, await _deployment.CarryAlertsAsync());
        Assert.Empty(_deployment.Raised.Waiting);
        Assert.Equal(
            [AlertCondition.RestrictionGranted, AlertCondition.RestrictionLoosened],
            [.. _deployment.AlertHandler.Taken.Select(alert => alert.Condition)]);
    }

    /// <summary>
    /// OPS-ALERT-003 AC2: an alert the handler did not take waits for a later pass
    /// rather than being lost, and its deduplication key is given back, so the later
    /// pass hands it over rather than folding it into the hand-over that failed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_003_AC2_AnAlertTheHandlerDidNotTakeIsHandedAgainAsync()
    {
        await RaisedAsync(AlertCondition.BreakGlassUsed, "issue");

        _deployment.AlertHandler.FailHandOvers();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(_deployment.CarryAlertsAsync);

        _ = Assert.Single(_deployment.Raised.Waiting);
        Assert.Empty(_deployment.AlertHandler.Taken);

        _deployment.AlertHandler.AcceptHandOvers();

        Assert.Equal(1, await _deployment.CarryAlertsAsync());
        Assert.Empty(_deployment.Raised.Waiting);
        Assert.Equal(
            AlertCondition.BreakGlassUsed,
            Assert.Single(_deployment.AlertHandler.Taken).Condition);
    }

    /// <summary>
    /// OPS-ALERT-003 AC2 (D-225): a handler that throws did not take the alert, so the
    /// pass gives its deduplication key back and leaves it waiting, and the next pass
    /// hands it over again and the handler takes it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_003_AC2_AnAlertWhoseHandlerThrewIsHandedAgainUntilTakenAsync()
    {
        var throwing = new ThrowingOnce();

        await using var faulted = new Deployment(services: services => services.AddSingleton<IAlertHandler>(throwing));

        Flow.Prepare(faulted);

        AlertRaised raised = Alerts.Of(AlertCondition.BreakGlassUsed, "issue", faulted.Clock.GetUtcNow());

        await faulted.Raised.AddAsync(new RaisedAlert(RaisedAlertId.Of(raised.RaisedAt), raised), CancellationToken.None);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(faulted.CarryAlertsAsync);

        _ = Assert.Single(faulted.Raised.Waiting);
        Assert.Empty(throwing.Taken);

        Assert.Equal(1, await faulted.CarryAlertsAsync());
        Assert.Empty(faulted.Raised.Waiting);
        Assert.Equal(AlertCondition.BreakGlassUsed, Assert.Single(throwing.Taken).Condition);
    }

    /// <summary>
    /// OPS-ALERT-003 AC2 (D-225): a pass that ended after it claimed the alert and its
    /// deduplication key, before the handler answered, left the key claimed for that
    /// raise; the raise is not folded into its own unanswered hand-over, so once the
    /// pass's claim has timed out the next pass hands it over.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_003_AC2_AnAlertWhosePassEndedBeforeTheHandlerAnsweredIsHandedAgainAsync()
    {
        await RaisedAsync(AlertCondition.BreakGlassUsed, "issue");

        RaisedAlert held = Assert.Single(_deployment.Raised.Waiting);
        TimeSpan timeout = Settings.OutboxClaimTimeout.Default;

        _deployment.Raised.TakeOver(held.Id, _deployment.Clock.GetUtcNow() + timeout);
        Assert.True(await _deployment.Alerts.FirstAsync(
            Alerts.Deduplication(held.Raised.IdempotencyKey),
            held.Id,
            held.Raised.RaisedAt,
            Settings.AlertingDedupeWindow.Default,
            CancellationToken.None));

        _deployment.Clock.Advance(timeout);

        Assert.Equal(1, await _deployment.CarryAlertsAsync());
        Assert.Empty(_deployment.Raised.Waiting);
        Assert.Equal(
            AlertCondition.BreakGlassUsed,
            Assert.Single(_deployment.AlertHandler.Taken).Condition);
    }

    /// <summary>
    /// OPS-ALERT-003 AC2, OPS-ALERT-002 AC1: an alert the handler took and that a pass
    /// ended before removing is folded into its own hand-over rather than handed again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_003_AC2_AnAlertAlreadyTakenIsNotHandedAgainAsync()
    {
        await RaisedAsync(AlertCondition.BreakGlassUsed, "issue");

        RaisedAlert held = Assert.Single(_deployment.Raised.Waiting);
        string key = Alerts.Deduplication(held.Raised.IdempotencyKey);

        Assert.True(await _deployment.Alerts.FirstAsync(
            key,
            held.Id,
            held.Raised.RaisedAt,
            Settings.AlertingDedupeWindow.Default,
            CancellationToken.None));
        Assert.True(await _deployment.Alerts.TakenAsync(key, held.Id, CancellationToken.None));

        Assert.Equal(1, await _deployment.CarryAlertsAsync());
        Assert.Empty(_deployment.Raised.Waiting);
        Assert.Empty(_deployment.AlertHandler.Taken);
    }

    /// <summary>
    /// OPS-ALERT-002 AC1: two raises of one condition under one key inside the window
    /// are handed over once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_002_AC1_OneConditionInsideTheWindowIsHandedOverOnceAsync()
    {
        await RaisedAsync(AlertCondition.RestrictionGranted, "one");

        _deployment.Clock.Advance(TimeSpan.FromSeconds(1));

        await RaisedAsync(AlertCondition.RestrictionGranted, "one");

        Assert.Equal(2, await _deployment.CarryAlertsAsync());
        Assert.Empty(_deployment.Raised.Waiting);
        _ = Assert.Single(_deployment.AlertHandler.Taken);
    }

    /// <summary>
    /// OPS-ALERT-002 AC3: two alerts of one condition under different scopes inside one
    /// window are both handed over.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_002_AC3_TwoScopesOfOneConditionAreBothHandedOverAsync()
    {
        await ScopedAsync(AlertCondition.Degradation, "mailbox.reconciliation");
        await ScopedAsync(AlertCondition.Degradation, "password.blocklist.fallback");

        Assert.Equal(2, await _deployment.CarryAlertsAsync());
        Assert.Equal(2, _deployment.AlertHandler.Taken.Count);
    }

    /// <summary>
    /// OPS-ALERT-002, D-022: a pass commits each condition's deduplication claim before
    /// it calls the handler, so no handler is called while a transaction is open.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_002_NoHandlerIsCalledWhileATransactionIsOpenAsync()
    {
        Deployment? watched = null;
        var watching = new Watching(() => watched!.Work.Open);

        await using (watched = new Deployment(services: services => services.AddSingleton<IAlertHandler>(watching)))
        {
            Flow.Prepare(watched);

            AlertRaised raised = Alerts.Of(AlertCondition.RestrictionGranted, "first", watched.Clock.GetUtcNow());

            await watched.Raised.AddAsync(new RaisedAlert(RaisedAlertId.Of(raised.RaisedAt), raised), CancellationToken.None);

            Assert.Equal(1, await watched.CarryAlertsAsync());
            Assert.Equal([false], watching.Open);
            Assert.Empty(watched.Raised.Waiting);
        }
    }

    /// <summary>
    /// CONV-DESIGN-003 AC9, INF-BG-001 AC4: a raised condition another pass holds is
    /// that pass's: this one hands nothing of it over and leaves the row as it stands,
    /// and once that claim has timed out the next pass hands it over.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_003_AC9_AConditionAnotherPassHoldsIsNotCarriedAsync()
    {
        await RaisedAsync(AlertCondition.RestrictionGranted, "first");

        RaisedAlert held = Assert.Single(_deployment.Raised.Waiting);
        TimeSpan timeout = Settings.OutboxClaimTimeout.Default;

        _deployment.Raised.TakeOver(held.Id, _deployment.Clock.GetUtcNow() + timeout);

        Assert.Equal(0, await _deployment.CarryAlertsAsync());
        Assert.Empty(_deployment.AlertHandler.Taken);
        _ = Assert.Single(_deployment.Raised.Waiting);

        _deployment.Clock.Advance(timeout);

        Assert.Equal(1, await _deployment.CarryAlertsAsync());
        _ = Assert.Single(_deployment.AlertHandler.Taken);
        Assert.Empty(_deployment.Raised.Waiting);
    }

    private async Task RaisedAsync(AlertCondition condition, string named) =>
        await WaitingAsync(Alerts.Of(condition, named, _deployment.Clock.GetUtcNow()));

    private async Task ScopedAsync(AlertCondition condition, string scope) =>
        await WaitingAsync(Alerts.Scoped(condition, scope, _deployment.Clock.GetUtcNow()));

    private async Task WaitingAsync(AlertRaised raised) =>
        await _deployment.Raised.AddAsync(new RaisedAlert(RaisedAlertId.Of(raised.RaisedAt), raised), CancellationToken.None);

    // A handler that throws at its first hand-over and takes every later one.
    private sealed class ThrowingOnce : IAlertHandler
    {
        private bool _thrown;

        public List<AlertRaised> Taken { get; } = [];

        public ValueTask<Result> TakeAsync(AlertRaised alert, CancellationToken cancellationToken)
        {
            if (!_thrown)
            {
                _thrown = true;

                throw new InvalidOperationException("The host's delivery faulted.");
            }

            Taken.Add(alert);

            return ValueTask.FromResult(Result.Success());
        }
    }

    // A handler that notes, at each hand-over, whether a transaction was open.
    private sealed class Watching(Func<bool> open) : IAlertHandler
    {
        public List<bool> Open { get; } = [];

        public ValueTask<Result> TakeAsync(AlertRaised alert, CancellationToken cancellationToken)
        {
            Open.Add(open());

            return ValueTask.FromResult(Result.Success());
        }
    }
}
