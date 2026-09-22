using System;

namespace Janus.Storage.Tests;

/// <summary>
/// A clock reading one instant, so that what a test writes and what it reads back are
/// judged against the same moment.
/// </summary>
/// <param name="now">The instant the clock reads.</param>
internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => now;
}
