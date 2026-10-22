using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Testing;

/// <summary>
/// A clock reference that measures no offset, so a development deployment raises no
/// degradation for its clock.
/// </summary>
/// <remarks>Implements LIB-TEST-003 and INF-HOST-001.</remarks>
public sealed class FixedClockReference : IClockReference
{
    /// <inheritdoc/>
    public ValueTask<Result<TimeSpan>> OffsetAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(TimeSpan.Zero));
}
