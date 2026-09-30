using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Oidc;

/// <summary>
/// Where the token signing keys are held. The private material is wrapped under the
/// deployment's data key and is handed back only to the one caller that makes a
/// credential of it (AUTH-KEY-002, PRIV-RIGHT-005a).
/// </summary>
/// <remarks>
/// Every change is conditional on the stored keys its caller read, and the stored keys
/// admit one next key and one current key, so of two processes making the same change
/// one makes it and the other is told it did not (AUTH-KEY-001, D-166 X3).
/// </remarks>
internal interface ISigningKeyStore
{
    /// <summary>
    /// Every key the database holds, retired ones whose public key is kept included.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The keys, oldest first.</returns>
    ValueTask<IReadOnlyList<SigningKey>> HeldAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The private material of one key.
    /// </summary>
    /// <param name="keyId">Which key.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The private key as it was written, to be cleared after use, or nothing where no
    /// key answers to the identifier or its private key is gone.
    /// </returns>
    ValueTask<byte[]?> PrivateKeyAsync(string keyId, CancellationToken cancellationToken);

    /// <summary>
    /// Records a new key, next or the first current one, and the private material it
    /// signs with.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="privateKey">Its private material, which the store wraps.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it was recorded; not where a key of its kind is already held.</returns>
    ValueTask<bool> AddAsync(SigningKey key, [NeverLogged] byte[] privateKey, CancellationToken cancellationToken);

    /// <summary>
    /// Makes the next key current and replaces the current one, both as the caller read
    /// them.
    /// </summary>
    /// <param name="next">The next key as read.</param>
    /// <param name="current">The current key as read, its longest lifetime included.</param>
    /// <param name="at">When the next key takes over.</param>
    /// <param name="overlapEndsAt">When the replaced key's overlap ends.</param>
    /// <param name="keptUntil">When the replaced key's public key stops being kept.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether both stood as read and were changed.</returns>
    ValueTask<bool> PromoteAsync(
        SigningKey next,
        SigningKey current,
        DateTimeOffset at,
        DateTimeOffset overlapEndsAt,
        DateTimeOffset keptUntil,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores a longer access-token lifetime with the current key, never a shorter one.
    /// </summary>
    /// <param name="current">The key the caller read as current.</param>
    /// <param name="lifetime">The lifetime an access token is about to be signed under.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether the key is still current and now carries at least that lifetime.
    /// </returns>
    ValueTask<bool> LengthenAsync(SigningKey current, TimeSpan lifetime, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the private key of a key whose overlap has ended.
    /// </summary>
    /// <param name="key">The key as read.</param>
    /// <param name="now">Now.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of removing it.</returns>
    ValueTask RetireAsync(SigningKey key, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a key whose keeping has ended.
    /// </summary>
    /// <param name="key">The key as read.</param>
    /// <param name="now">Now.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of removing it.</returns>
    ValueTask RemoveAsync(SigningKey key, DateTimeOffset now, CancellationToken cancellationToken);
}
