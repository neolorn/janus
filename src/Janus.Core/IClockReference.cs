using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What tells the library how far this host's clock stands from the time the
/// environment keeps it to. The library reads no time source of its own: a deployment
/// registers one of these, and until it does the drift of its clock is raised as a
/// degradation, since nothing measures it.
/// </summary>
/// <remarks>
/// Implements LIB-EXT-001 and INF-HOST-001 AC2. Keeping the clock synchronised is the
/// environment's (INF-HOST-001 AC1); the library reads the offset the environment last
/// measured every hour and raises <c>clock-drift</c> where it exceeds the tolerance a
/// time-based code is accepted within, <c>factor.totp.drift</c> steps of thirty seconds
/// either way.
/// </remarks>
public interface IClockReference
{
    /// <summary>
    /// How far this host's clock stood from the reference when the environment last
    /// measured it.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The offset, positive where this host's clock is ahead, or the failure where the
    /// environment holds no measurement.
    /// </returns>
    ValueTask<Result<TimeSpan>> OffsetAsync(CancellationToken cancellationToken);
}
