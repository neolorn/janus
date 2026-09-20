using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Oidc;

/// <summary>
/// Where the one-time codes are held while they wait to be exchanged.
/// </summary>
internal interface IAuthorizationCodeStore
{
    /// <summary>
    /// The code a client presented.
    /// </summary>
    /// <param name="fingerprint">What the presented code hashes to.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The code, or nothing where none answers to it.</returns>
    ValueTask<AuthorizationCode?> FindAsync(byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Records a newly issued code.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AddAsync(AuthorizationCode code, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a change to a code the caller read.
    /// </summary>
    /// <param name="code">The code as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of carrying it.</returns>
    ValueTask RecordAsync(AuthorizationCode code, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the codes that have expired or been exchanged (AUTH-KEY-003).
    /// </summary>
    /// <param name="now">Now.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many went.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
