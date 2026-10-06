using System;

namespace Janus.Core;

/// <summary>
/// Fills and clears the key ring and records the mail server in use, for the service
/// that does both at the start.
/// </summary>
/// <param name="ring">The key ring it fills and clears.</param>
/// <param name="inUse">The mail server in use, whose choice it records.</param>
/// <remarks>
/// Implements CONV-DESIGN-007, CONV-CODE-007 and D-189. It stands over the one ring and
/// the one mail server in use the process holds, and keeps nothing of its own.
/// </remarks>
internal sealed class KeyRingFilling(KeyRing ring, MailServerInUse inUse) : IKeyRingFilling
{
    /// <inheritdoc/>
    public void HoldKeyEncryptionKeys(KeyEncryptionKeys keys) => ring.HoldKeyEncryptionKeys(keys);

    /// <inheritdoc/>
    public void HoldFingerprintKeys(FingerprintKeys keys) => ring.HoldFingerprintKeys(keys);

    /// <inheritdoc/>
    public void HoldMaintenanceCredential(ReadOnlyMemory<byte> credential) => ring.HoldMaintenanceCredential(credential);

    /// <inheritdoc/>
    public void Hold(string provider, ProviderCredential credential) => ring.Hold(provider, credential);

    /// <inheritdoc/>
    public void Fill() => ring.Fill();

    /// <inheritdoc/>
    public void HoldMailServerSecret(ReadOnlyMemory<byte> secret) => ring.HoldMailServerSecret(secret);

    /// <inheritdoc/>
    public void Completed() => ring.Completed();

    /// <inheritdoc/>
    public void Choose(IMailServer? server) => inUse.Choose(server);

    /// <inheritdoc/>
    public void Clear() => ring.Clear();
}
