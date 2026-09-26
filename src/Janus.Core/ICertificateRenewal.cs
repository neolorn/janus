using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What tells the library how the environment's last certificate renewal went. The
/// library renews no certificate and reads none: a deployment registers one of these
/// over whatever renews its certificates, and until it does the renewal is raised as a
/// degradation, since nothing watches it.
/// </summary>
/// <remarks>
/// Implements LIB-EXT-001 and INF-TLS-003 AC2. The library asks every hour and raises
/// <c>certificate-renewal-failed</c> for a failed renewal, so a failure reaches the
/// alert channels the day it happens without anyone checking. The expiry of the
/// certificate served is watched apart from this and from whatever renews it
/// (INF-TLS-003 AC1, AC3), which is the environment's.
/// </remarks>
public interface ICertificateRenewal
{
    /// <summary>
    /// When the most recent renewal failed.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The instant the most recent attempt failed, nothing where the most recent attempt
    /// succeeded, or the failure where how it went cannot be read.
    /// </returns>
    ValueTask<Result<DateTimeOffset?>> LastFailureAsync(CancellationToken cancellationToken);
}
