using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.SignIn;

/// <summary>
/// Where sign-ins in progress are held.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-003 and AUTH-KEY-003. The area declares the port and the
/// persistence lives in the storage project; what has stopped answering is swept, not
/// left.
/// </remarks>
internal interface IChallengeStore
{
    /// <summary>
    /// The challenge a handle answers to.
    /// </summary>
    /// <param name="fingerprint">What the handle hashes to.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The challenge, or nothing.</returns>
    ValueTask<Challenge?> FindAsync(byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Records a new challenge.
    /// </summary>
    /// <param name="challenge">The challenge.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    ValueTask AddAsync(Challenge challenge, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a change to a challenge already held.
    /// </summary>
    /// <param name="challenge">The challenge.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    ValueTask RecordAsync(Challenge challenge, CancellationToken cancellationToken);

    /// <summary>
    /// Ends a challenge, whether it completed or was abandoned.
    /// </summary>
    /// <param name="fingerprint">What the handle hashes to.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    ValueTask RemoveAsync(byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Clears the challenges that have stopped answering.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were cleared.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
