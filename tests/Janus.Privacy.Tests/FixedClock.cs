using System;

namespace Janus.Privacy.Tests;

/// <summary>
/// A clock reading an instant a test chose, and reading the next one when the test
/// moves it.
/// </summary>
/// <param name="now">The instant the clock starts at.</param>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>
    /// Moves the clock on.
    /// </summary>
    /// <param name="span">How far to move it.</param>
    public void Advance(TimeSpan span) => _now += span;
}
