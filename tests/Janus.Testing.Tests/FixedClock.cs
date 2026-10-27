using System;

namespace Janus.Testing.Tests;

/// <summary>
/// A clock reading an instant a test chose.
/// </summary>
/// <param name="now">The instant the clock reads.</param>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => now;
}
