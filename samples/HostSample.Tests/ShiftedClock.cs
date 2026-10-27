using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HostSample.Tests;

/// <summary>
/// The deployment's clock: the machine's, moved forward by what a test advances it by,
/// so a test reaches a step-up's recency or a deletion's grace window without waiting
/// for it. A timer made on it fires when the moved clock reaches its due time, so an
/// advance brings forward whatever the background work was waiting for.
/// </summary>
internal sealed class ShiftedClock : TimeProvider
{
    private readonly Lock _gate = new();

    private readonly HashSet<ShiftedTimer> _timers = [];

    private TimeSpan _offset;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return TimeProvider.System.GetUtcNow() + _offset;
        }
    }

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ShiftedTimer(this, callback, state);

        lock (_gate)
        {
            _ = _timers.Add(timer);
        }

        _ = timer.Change(dueTime, period);

        return timer;
    }

    /// <summary>
    /// Moves the clock forward, and brings each timer's firing forward with it.
    /// </summary>
    /// <param name="by">How far.</param>
    public void Advance(TimeSpan by)
    {
        ShiftedTimer[] live;

        lock (_gate)
        {
            _offset += by;
            live = [.. _timers];
        }

        foreach (ShiftedTimer timer in live)
        {
            timer.Rearm();
        }
    }

    private void Forget(ShiftedTimer timer)
    {
        lock (_gate)
        {
            _ = _timers.Remove(timer);
        }
    }

    // A timer whose due time is an instant of the moved clock, carried by a timer of
    // the machine's that is set again whenever the clock moves.
    private sealed class ShiftedTimer : ITimer
    {
        private readonly Lock _gate = new();

        private readonly ShiftedClock _clock;

        private readonly TimerCallback _callback;

        private readonly object? _state;

        private readonly ITimer _carrier;

        private DateTimeOffset? _due;

        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public ShiftedTimer(ShiftedClock clock, TimerCallback callback, object? state)
        {
            _clock = clock;
            _callback = callback;
            _state = state;
            _carrier = TimeProvider.System.CreateTimer(_ => Fired(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (_gate)
            {
                _period = period;
                _due = dueTime == Timeout.InfiniteTimeSpan ? null : _clock.GetUtcNow() + dueTime;

                return _carrier.Change(dueTime, Timeout.InfiniteTimeSpan);
            }
        }

        public void Rearm()
        {
            lock (_gate)
            {
                if (_due is DateTimeOffset due)
                {
                    TimeSpan remaining = due - _clock.GetUtcNow();

                    _ = _carrier.Change(remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining, Timeout.InfiniteTimeSpan);
                }
            }
        }

        public void Dispose()
        {
            _clock.Forget(this);
            _carrier.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            _clock.Forget(this);
            await _carrier.DisposeAsync();
        }

        private void Fired()
        {
            lock (_gate)
            {
                if (_period == Timeout.InfiniteTimeSpan)
                {
                    _due = null;
                }
                else
                {
                    _due = _clock.GetUtcNow() + _period;
                    _ = _carrier.Change(_period, Timeout.InfiniteTimeSpan);
                }
            }

            _callback(_state);
        }
    }
}
