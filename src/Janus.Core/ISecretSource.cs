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
    /// <returns>
    /// The versions a subject key may be wrapped or unwrapped under, or the failure the
    /// source met reading them.
    /// </returns>
    ValueTask<Result<KeyEncryptionKeys>> ReadKeyEncryptionKeysAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the key the searchable fingerprints are computed under and the versions
    /// retained beside it.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>
    /// The versions a stored fingerprint may be under, the one written current, or the
    /// failure the source met reading them.
    /// </returns>
    ValueTask<Result<FingerprintKeys>> ReadFingerprintKeysAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the database connection the scheduled maintenance runs under, whose login
    /// holds the maintenance role's rights and nothing else, and which the application's
    /// own configuration never carries.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The connection, as its UTF-8 bytes, or the failure the source met reading it.</returns>
    ValueTask<Result<ReadOnlyMemory<byte>>> ReadMaintenanceCredentialAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads what this application presents at a declared social provider's token
    /// endpoint, asked only for a provider the deployment declares.
    /// </summary>
    /// <param name="provider">The provider's name, as the factor catalogue spells it: <c>google</c> or <c>apple</c>.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The credential, or the failure the source met reading it.</returns>
    ValueTask<Result<ProviderCredential>> ReadProviderCredentialAsync(string provider, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the mail server's management key, which the library's mail-server adapter
    /// presents, asked only where the start chooses that adapter.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The key, as its UTF-8 bytes, or the failure the source met reading it.</returns>
    ValueTask<Result<ReadOnlyMemory<byte>>> ReadMailServerSecretAsync(CancellationToken cancellationToken);
}
