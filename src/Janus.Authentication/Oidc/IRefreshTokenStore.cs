using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Oidc;

/// <summary>
/// Where the refresh tokens are held, by family, so that a token presented twice can
/// take the whole family with it (AUTH-OIDC-003).
/// </summary>
internal interface IRefreshTokenStore
{
    /// <summary>
    /// The token a client presented.
    /// </summary>
    /// <param name="fingerprint">What the presented token hashes to.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The token, or nothing where none answers to it.</returns>
    ValueTask<RefreshToken?> FindAsync(byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Every token of one family, used or not.
    /// </summary>
    /// <param name="family">Which family.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The tokens.</returns>
    ValueTask<IReadOnlyList<RefreshToken>> OfAsync(
        RefreshFamilyId family,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a newly issued token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AddAsync(RefreshToken token, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a change to a token the caller read.
    /// </summary>
    /// <param name="token">The token as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of carrying it.</returns>
    ValueTask RecordAsync(RefreshToken token, CancellationToken cancellationToken);

    /// <summary>
    /// Removes every token of one family, which is what a reuse does.
    /// </summary>
    /// <param name="family">Which family.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of removing them.</returns>
    ValueTask RemoveFamilyAsync(RefreshFamilyId family, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the tokens that have expired or been used (AUTH-KEY-003).
    /// </summary>
    /// <param name="now">Now.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many went.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
