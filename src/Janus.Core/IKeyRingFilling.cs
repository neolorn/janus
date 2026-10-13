using System;

namespace Janus.Core;

/// <summary>
/// The filling of the key ring and the recording of the mail server in use at the
/// start, and the ring's clearing once everything has stopped.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-007, CONV-CODE-007 and D-189. The key ring and the mail server
/// in use are seams, and no public contract declares what fills the one and chooses the
/// other, so the service that fills the key ring asks through this contract and never
/// names what stands behind either.
/// </remarks>
internal interface IKeyRingFilling
{
    /// <summary>
    /// Holds the key-encryption key, every version copied into an array of the ring's own.
    /// </summary>
    /// <param name="keys">What the source answered.</param>
    /// <exception cref="InvalidOperationException">
    /// The ring is no longer being filled, or holds the key already.
    /// </exception>
    void HoldKeyEncryptionKeys(KeyEncryptionKeys keys);

    /// <summary>
    /// Holds the fingerprint key, every version copied into an array of the ring's own.
    /// </summary>
    /// <param name="keys">What the source answered.</param>
    /// <exception cref="InvalidOperationException">
    /// The ring is no longer being filled, or holds the key already.
    /// </exception>
    void HoldFingerprintKeys(FingerprintKeys keys);

    /// <summary>
    /// Holds the maintenance credential, copied into an array of the ring's own.
    /// </summary>
    /// <param name="credential">What the source answered.</param>
    /// <exception cref="InvalidOperationException">
    /// The ring is no longer being filled, or holds the credential already.
    /// </exception>
    void HoldMaintenanceCredential(ReadOnlyMemory<byte> credential);

    /// <summary>
    /// Holds a social provider's credential, copied into an array of the ring's own.
    /// </summary>
    /// <param name="provider">The provider's name, as the secret source was asked for it.</param>
    /// <param name="credential">What the source answered.</param>
    /// <exception cref="InvalidOperationException">The ring is no longer being filled.</exception>
    void Hold(string provider, ProviderCredential credential);

    /// <summary>
    /// Marks the ring filled, after which it lends what it holds.
    /// </summary>
    /// <exception cref="InvalidOperationException">The ring is no longer being filled.</exception>
    void Fill();

    /// <summary>
    /// Holds the mail server's management key, copied into an array of the ring's own.
    /// </summary>
    /// <param name="secret">What the source answered.</param>
    /// <exception cref="InvalidOperationException">
    /// The ring is not at the step that reads the mail server's key.
    /// </exception>
    void HoldMailServerSecret(ReadOnlyMemory<byte> secret);

    /// <summary>
    /// Marks the ring complete, once the start has chosen the mail server in use, after
    /// which it lends the mail server's key too.
    /// </summary>
    /// <exception cref="InvalidOperationException">The ring is not filled, or is complete already.</exception>
    void Completed();

    /// <summary>
    /// Records the choice the start made of the mail server in use.
    /// </summary>
    /// <param name="server">The mail server in use, or nothing where there is none.</param>
    /// <exception cref="InvalidOperationException">A choice was already made.</exception>
    void Choose(IMailServer? server);

    /// <summary>
    /// Clears every array the ring holds, after which a read is a fault.
    /// </summary>
    void Clear();
}
