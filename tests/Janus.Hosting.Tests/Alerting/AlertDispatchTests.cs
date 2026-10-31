using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Alerting;
using Janus.Hosting.Background;
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

        Error refused = await RefusedAsync(faulted);

        Assert.Equal(ErrorCodes.SystemFault, refused.Code);
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
        Assert.True(await _deployment.Alerts.TakenAsync(key, held.Id, held.Raised.RaisedAt, CancellationToken.None));

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

    /// <summary>
    /// OPS-ALERT-003 AC2, BFF-ERR-002 AC2: what the host's handler threw is kept with the
    /// refusal as the worker keeps a job's fault, by its type and frames and never its
    /// message, so the operator reads why the handler failed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_003_AC2_WhatTheHandlerThrewIsKeptByItsTypeAndFramesAsync()
    {
        var throwing = new ThrowingOnce();

        await using var faulted = new Deployment(services: services => services.AddSingleton<IAlertHandler>(throwing));

        Flow.Prepare(faulted);

        AlertRaised raised = Alerts.Of(AlertCondition.BreakGlassUsed, "issue", faulted.Clock.GetUtcNow());

        await faulted.Raised.AddAsync(new RaisedAlert(RaisedAlertId.Of(raised.RaisedAt), raised), CancellationToken.None);

        Error refused = await RefusedAsync(faulted);

        string kept = Assert.IsType<string>(refused.Details[FaultLog.Detail].GetString());

        Assert.StartsWith(typeof(InvalidOperationException).FullName!, kept, StringComparison.Ordinal);
        Assert.Contains(nameof(ThrowingOnce), kept, StringComparison.Ordinal);
        Assert.DoesNotContain(ThrowingOnce.Message, kept, StringComparison.Ordinal);
    }

    /// <summary>
    /// OPS-ALERT-003 AC2, OPS-ALERT-002 AC1: a handler that ignores its token outlasts
    /// its pass's claim, while a second pass hands the alert over and its handler takes
    /// it. The first hand-over's token was cancelled when the claim lapsed, and what the
    /// first does when it ends leaves the alert recorded as taken, so the next raise of
    /// the condition inside the window is folded into it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_003_AC2_AHandOverAbandonedPastItsClaimLeavesTheOneThatTookItTakenAsync()
    {
        var hanging = new Hanging();

        await using (var deployment = new Deployment(services: services => services.AddScoped(provider => new AlertDispatch(
            provider.GetRequiredService<IRaisedAlerts>(),
            provider.GetRequiredService<IAlertLedger>(),
            hanging,
            provider.GetRequiredService<IConfigurationStore>(),
            provider.GetRequiredService<IUnitOfWork>(),
            hanging.Clock))))
        {
            Flow.Prepare(deployment);

            TimeSpan timeout = Settings.OutboxClaimTimeout.Default;
            var late = TimeSpan.FromSeconds(1);

            await AddedAsync(deployment, Alerts.Of(AlertCondition.BreakGlassUsed, "issue", hanging.Clock.GetUtcNow()));

            // The first pass reaches its handler a second after it claimed the alert.
            deployment.Work.Meanwhile = () => hanging.Clock.Advance(late);

            Task<int> first = deployment.CarryAlertsAsync();

            await hanging.Entered;

            hanging.Clock.Advance(timeout - late);

            Assert.True(hanging.First.IsCancellationRequested);

            // The first hand-over ends after the second pass has handed the alert over and
            // before its handler answers.
            hanging.Meanwhile = async () =>
            {
                hanging.End();

                _ = await Assert.ThrowsAsync<InvalidOperationException>(() => first);
            };

            Assert.Equal(1, await deployment.CarryAlertsAsync());
            Assert.Empty(deployment.Raised.Waiting);
            _ = Assert.Single(hanging.Taken);

            hanging.Clock.Advance(late);

            await AddedAsync(deployment, Alerts.Of(AlertCondition.BreakGlassUsed, "issue", hanging.Clock.GetUtcNow()));

            Assert.Equal(1, await deployment.CarryAlertsAsync());
            Assert.Empty(deployment.Raised.Waiting);
            _ = Assert.Single(hanging.Taken);
        }
    }

    // One pass run as the worker runs it, answering the refusal that stopped it.
    private static async Task<Error> RefusedAsync(Deployment deployment)
    {
        await using AsyncServiceScope scope = deployment.Scope();

        return (await scope.ServiceProvider
                .GetRequiredService<AlertDispatch>()
                .CarryAsync(
                    AccessContext.Of(BackgroundJobs.All.Single(job => job.Name == AlertDispatch.Job).Principal),
                    CancellationToken.None))
            .Match(
                carried => throw new InvalidOperationException("The pass carried " + carried + " and refused nothing."),
                error => error);
    }

    private static async Task AddedAsync(Deployment deployment, AlertRaised raised) =>
        await deployment.Raised.AddAsync(new RaisedAlert(RaisedAlertId.Of(raised.RaisedAt), raised), CancellationToken.None);

    private async Task RaisedAsync(AlertCondition condition, string named) =>
        await WaitingAsync(Alerts.Of(condition, named, _deployment.Clock.GetUtcNow()));

    private async Task ScopedAsync(AlertCondition condition, string scope) =>
        await WaitingAsync(Alerts.Scoped(condition, scope, _deployment.Clock.GetUtcNow()));

    private async Task WaitingAsync(AlertRaised raised) =>
        await _deployment.Raised.AddAsync(new RaisedAlert(RaisedAlertId.Of(raised.RaisedAt), raised), CancellationToken.None);

    // A handler that throws at its first hand-over and takes every later one.
    private sealed class ThrowingOnce : IAlertHandler
    {
        public const string Message = "The host's delivery faulted.";

        private bool _thrown;

        public List<AlertRaised> Taken { get; } = [];

        public ValueTask<Result> TakeAsync(AlertRaised alert, CancellationToken cancellationToken)
        {
            if (!_thrown)
            {
                _thrown = true;

                throw new InvalidOperationException(Message);
            }

            Taken.Add(alert);

            return ValueTask.FromResult(Result.Success());
        }
    }

    // A handler whose first hand-over ignores its token and hangs until the test ends it,
    // then throws; every later one runs what the test set and takes the alert. It keeps
    // a clock whose timers fire as it is moved on, so its pass's deadline is seen.
    private sealed class Hanging : IAlertHandler
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _handed;

        public Ticking Clock { get; } = new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

        public Task Entered => _entered.Task;

        public CancellationToken First { get; private set; }

        public Func<Task>? Meanwhile { get; set; }

        public List<AlertRaised> Taken { get; } = [];

        public void End() => _ended.SetResult();

        public async ValueTask<Result> TakeAsync(AlertRaised alert, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _handed) == 1)
            {
                First = cancellationToken;
                _entered.SetResult();

                await _ended.Task;

                throw new InvalidOperationException("The host's delivery hung.");
            }

            if (Meanwhile is Func<Task> meanwhile)
            {
                Meanwhile = null;

                await meanwhile();
            }

            Taken.Add(alert);

            return Result.Success();
        }
    }

    // A clock moved on by hand whose timers fire when it reaches them.
    private sealed class Ticking(DateTimeOffset now) : TimeProvider
    {
        private readonly Lock _gate = new();

        private readonly List<Timer> _timers = [];

        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow()
        {
            lock (_gate)
            {
                return _now;
            }
        }

        public void Advance(TimeSpan span)
        {
            List<Timer> due;

            lock (_gate)
            {
                _now += span;
                due = [.. _timers.Where(timer => timer.Due <= _now)];
            }

            foreach (Timer timer in due)
            {
                timer.Fire();
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new Timer(this, callback, state);

            _ = timer.Change(dueTime, period);

            lock (_gate)
            {
                _timers.Add(timer);
            }

            return timer;
        }

        private sealed class Timer(Ticking clock, TimerCallback callback, object? state) : ITimer
        {
            public DateTimeOffset Due { get; private set; } = DateTimeOffset.MaxValue;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                Due = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : clock.GetUtcNow() + dueTime;

                return true;
            }

            public void Fire()
            {
                Due = DateTimeOffset.MaxValue;
                callback(state);
            }

            public void Dispose()
            {
                lock (clock._gate)
                {
                    _ = clock._timers.Remove(this);
                }
            }

            public ValueTask DisposeAsync()
            {
                Dispose();

                return ValueTask.CompletedTask;
            }
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
