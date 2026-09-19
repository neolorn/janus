using System;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A clock that does not move, so that a grant's expiry is read against a stated
/// instant rather than against the moment the test happened to run.
/// </summary>
/// <param name="now">The instant it reports.</param>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => now;
}
