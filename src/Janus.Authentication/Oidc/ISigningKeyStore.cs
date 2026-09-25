using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Oidc;

/// <summary>
/// Where the token signing keys are held. The private material is wrapped under the
/// deployment's key-encryption key and is handed back only to the one caller that
/// signs with it (AUTH-KEY-002).
/// </summary>
internal interface ISigningKeyStore
{
    /// <summary>
    /// Every key still published: the one signing now and any within its overlap.
    /// </summary>
    /// <param name="now">Now, which decides what has retired.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The keys, newest first.</returns>
    ValueTask<IReadOnlyList<SigningKey>> PublishedAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// The private material of one key.
    /// </summary>
    /// <param name="keyId">Which key.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The private key as it was written, to be cleared after use, or nothing where no
    /// key answers to the identifier.
    /// </returns>
    ValueTask<byte[]?> PrivateKeyAsync(string keyId, CancellationToken cancellationToken);

    /// <summary>
    /// Records a new key and the private material it signs with.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="privateKey">Its private material, which the store wraps.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AddAsync(SigningKey key, [NeverLogged] byte[] privateKey, CancellationToken cancellationToken);

    /// <summary>
    /// Carries a change to a key the caller read.
    /// </summary>
    /// <param name="key">The key as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of carrying it.</returns>
    ValueTask RecordAsync(SigningKey key, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the keys that have left the published set, with their private material.
    /// </summary>
    /// <param name="now">Now.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many went.</returns>
    ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
