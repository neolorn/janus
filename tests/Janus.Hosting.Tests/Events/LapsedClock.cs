using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Hosting.Tests.Events;

/// <summary>
/// A clock reading one instant on which every wait has already run out, so whatever is
/// bounded by a wait on it is abandoned at once.
/// </summary>
/// <param name="now">The instant the clock reads.</param>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class LapsedClock(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => now;

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);

        callback(state);

        return new Lapsed();
    }

    private sealed class Lapsed : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
