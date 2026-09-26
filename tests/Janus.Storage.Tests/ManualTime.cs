using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Storage.Tests;

/// <summary>
/// A clock that moves only when the test moves it, firing the timers that fall due as it
/// does, so a wait taken on the clock ends when the test says its interval has passed and
/// never before, however long the machine takes.
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class ManualTime : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<ManualTimer> _standing = [];
    private TaskCompletionSource _scheduled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DateTimeOffset _now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    /// <inheritdoc/>
    public override long GetTimestamp() => GetUtcNow().UtcTicks;

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);

        _ = timer.Change(dueTime, period);

        return timer;
    }

    /// <summary>
    /// Waits until a timer stands that has neither fired nor been disposed. A caller
    /// bounds the wait, since code that never asks the clock for a timer never ends it.
    /// </summary>
    /// <returns>The work of waiting.</returns>
    public Task PendingAsync()
    {
        lock (_gate)
        {
            return _standing.Count > 0 ? Task.CompletedTask : _scheduled.Task;
        }
    }

    /// <summary>
    /// Moves the clock on, firing every timer that falls due by the new instant.
    /// </summary>
    /// <param name="by">How far.</param>
    public void Advance(TimeSpan by)
    {
        List<ManualTimer> due;

        lock (_gate)
        {
            _now += by;
            due = _standing.FindAll(timer => timer.Due <= _now);
            _ = _standing.RemoveAll(due.Contains);
        }

        foreach (ManualTimer timer in due)
        {
            timer.Fire();
        }
    }

    private void Schedule(ManualTimer timer, TimeSpan dueTime)
    {
        lock (_gate)
        {
            _ = _standing.Remove(timer);

            if (dueTime == Timeout.InfiniteTimeSpan)
            {
                return;
            }

            timer.Due = _now + dueTime;
            _standing.Add(timer);
            _ = _scheduled.TrySetResult();
            _scheduled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private void Cancel(ManualTimer timer)
    {
        lock (_gate)
        {
            _ = _standing.Remove(timer);
        }
    }

    private sealed class ManualTimer(ManualTime time, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period = Timeout.InfiniteTimeSpan;
        private bool _disposed;

        public DateTimeOffset Due { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (_disposed)
            {
                return false;
            }

            _period = period;
            time.Schedule(this, dueTime);

            return true;
        }

        public void Fire()
        {
            callback(state);

            if (!_disposed && _period != Timeout.InfiniteTimeSpan)
            {
                time.Schedule(this, _period);
            }
        }

        public void Dispose()
        {
            _disposed = true;
            time.Cancel(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
