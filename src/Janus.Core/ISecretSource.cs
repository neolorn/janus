using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// Where the deployment's key material comes from. The library ships no
/// secrets-manager client and no default: a host that supplies none does not start.
/// </summary>
/// <remarks>
/// Implements LIB-EXT-001, OPS-SEC-001 and CONV-DESIGN-007. Each value is read once,
/// at startup or at the start of a command, and never written to the database.
/// </remarks>
public interface ISecretSource
{
    /// <summary>
    /// Reads the key-encryption key and the versions retained beside it.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The versions a subject key may be wrapped or unwrapped under.</returns>
    ValueTask<KeyEncryptionKeys> ReadKeyEncryptionKeysAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the key the searchable fingerprints are computed under and the versions
    /// retained beside it.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The versions a stored fingerprint may be under, the one written current.</returns>
    ValueTask<FingerprintKeys> ReadFingerprintKeysAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the database credential the scheduled maintenance runs under, which the
    /// application's own configuration never carries.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The credential, as its UTF-8 bytes.</returns>
    ValueTask<ReadOnlyMemory<byte>> ReadMaintenanceCredentialAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the secret this application presents at the provider's token endpoint
    /// when it exchanges a sign-on code (BFF-SESS-006).
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The secret, as its UTF-8 bytes.</returns>
    ValueTask<ReadOnlyMemory<byte>> ReadSignOnSecretAsync(CancellationToken cancellationToken);
}
