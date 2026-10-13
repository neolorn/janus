using System;

namespace Janus.Core;

/// <summary>
/// The one place the secrets read at startup are held between their uses. A use
/// borrows a secret for its own length and keeps nothing.
/// </summary>
/// <remarks>
/// Implements CONV-CODE-007, CONV-DESIGN-007 and CONV-LAYOUT-002. Every secret the ring
/// holds is one the host's own <see cref="ISecretSource"/> supplied, so resolving it
/// exposes nothing the host does not already hold. The ring is filled in steps, the mail
/// server's key last; a read of a key before the step that reads it is done, or after
/// the ring is cleared when the application stops or a command ends, is a fault.
/// </remarks>
public interface IKeyRing
{
    /// <summary>
    /// Lends the key-encryption key, every version the deployment holds, for the length
    /// of one use.
    /// </summary>
    /// <typeparam name="TValue">What the use answers.</typeparam>
    /// <param name="use">
    /// What is done with the versions, which keeps nothing of them once it returns.
    /// </param>
    /// <returns>
    /// What the use answered, or <c>model.startup.secretunavailable</c> naming
    /// <c>keyEncryptionKeys</c> where the ring holds none.
    /// </returns>
    /// <exception cref="InvalidOperationException">The ring is not filled, or is cleared.</exception>
    Result<TValue> BorrowKeyEncryptionKeys<TValue>(Func<KeyEncryptionKeys, TValue> use);

    /// <summary>
    /// Lends one version of the key-encryption key for the length of one use.
    /// </summary>
    /// <typeparam name="TValue">What the use answers.</typeparam>
    /// <param name="version">The version asked for.</param>
    /// <param name="use">What is done with the key, which keeps nothing of it once it returns.</param>
    /// <returns>
    /// What the use answered, or <c>model.startup.secretunavailable</c> naming
    /// <c>keyEncryptionKeys</c> and the version where the ring does not hold it.
    /// </returns>
    /// <exception cref="InvalidOperationException">The ring is not filled, or is cleared.</exception>
    Result<TValue> BorrowKeyEncryptionKey<TValue>(int version, Func<ReadOnlyMemory<byte>, TValue> use);

    /// <summary>
    /// Lends the fingerprint key, every version the deployment holds, for the length of
    /// one use.
    /// </summary>
    /// <typeparam name="TValue">What the use answers.</typeparam>
    /// <param name="use">
    /// What is done with the versions, which keeps nothing of them once it returns.
    /// </param>
    /// <returns>
    /// What the use answered, or <c>model.startup.secretunavailable</c> naming
    /// <c>fingerprintKeys</c> where the ring holds none.
    /// </returns>
    /// <exception cref="InvalidOperationException">The ring is not filled, or is cleared.</exception>
    Result<TValue> BorrowFingerprintKeys<TValue>(Func<FingerprintKeys, TValue> use);

    /// <summary>
    /// Lends the maintenance credential for the length of one use.
    /// </summary>
    /// <typeparam name="TValue">What the use answers.</typeparam>
    /// <param name="use">
    /// What is done with the credential, as its UTF-8 bytes, which keeps nothing of it
    /// once it returns.
    /// </param>
    /// <returns>
    /// What the use answered, or <c>model.startup.secretunavailable</c> naming
    /// <c>maintenanceCredential</c> where the ring holds none, as a command's does.
    /// </returns>
    /// <exception cref="InvalidOperationException">The ring is not filled, or is cleared.</exception>
    Result<TValue> BorrowMaintenanceCredential<TValue>(Func<ReadOnlyMemory<byte>, TValue> use);

    /// <summary>
    /// Lends a declared social provider's credential for the length of one use.
    /// </summary>
    /// <typeparam name="TValue">What the use answers.</typeparam>
    /// <param name="provider">The provider's name, as the secret source was asked for it.</param>
    /// <param name="use">
    /// What is done with the credential, which keeps nothing of it once it returns.
    /// </param>
    /// <returns>
    /// What the use answered, or <c>model.startup.secretunavailable</c> naming
    /// <c>socialProvider.&lt;provider&gt;</c> where the ring holds no credential for it.
    /// </returns>
    /// <exception cref="InvalidOperationException">The ring is not filled, or is cleared.</exception>
    Result<TValue> BorrowProviderCredential<TValue>(string provider, Func<ProviderCredential, TValue> use);

    /// <summary>
    /// Lends the mail server's management key for the length of one use, once the start
    /// has chosen the mail server in use.
    /// </summary>
    /// <typeparam name="TValue">What the use answers.</typeparam>
    /// <param name="use">What is done with the key, which keeps nothing of it once it returns.</param>
    /// <returns>
    /// What the use answered, or <c>model.startup.secretunavailable</c> naming
    /// <c>mailServerSecret</c> where the start did not choose the library's mail-server
    /// adapter, so the ring holds no such key.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The start has not chosen the mail server in use, or the ring is cleared.
    /// </exception>
    Result<TValue> BorrowMailServerSecret<TValue>(Func<ReadOnlyMemory<byte>, TValue> use);
}
