using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The key ring: what it lends, when a read is a fault, and what its clearing leaves
/// behind (CONV-CODE-007, D-171).
/// </summary>
[Trait("kind", "unit")]
public sealed class KeyRingTests
{
    private static readonly byte[] Secret = "the-client-secret"u8.ToArray();

    private static readonly byte[] Key = [0x30, 0x81, 0x87, 0x02, 0x01, 0x00];

    /// <summary>
    /// CONV-CODE-007 AC3: a read before the ring is filled is a fault, not a refusal.
    /// </summary>
    [Fact]
    public void CONV_CODE_007_AC3_AReadBeforeTheRingIsFilledThrows()
    {
        var ring = new KeyRing();

        ring.Hold("google", ProviderCredential.Secret(Secret));

        Assert.Throws<InvalidOperationException>(
            () => ring.BorrowProviderCredential("google", credential => credential.Material.Length));
    }

    /// <summary>
    /// CONV-CODE-007 AC3: the ring's clearing leaves every array it holds zero, and a
    /// read after it is a fault.
    /// </summary>
    [Fact]
    public void CONV_CODE_007_AC3_TheClearingLeavesEveryArrayZeroAndAReadAfterItThrows()
    {
        KeyRing ring = Filled();

        Assert.All(ring.Arrays, array => Assert.Contains(array, value => value != 0));

        ring.Clear();

        Assert.Equal(2, ring.Arrays.Count());
        Assert.All(ring.Arrays, array => Assert.All(array, value => Assert.Equal(0, value)));
        Assert.Throws<InvalidOperationException>(
            () => ring.BorrowProviderCredential("google", credential => credential.Material.Length));
    }

    /// <summary>
    /// CONV-CODE-007: the ring holds a copy of its own, so what the source answered can
    /// be cleared by the source, and it lends each credential in the form it was read.
    /// </summary>
    [Fact]
    public void CONV_CODE_007_TheRingLendsItsOwnCopyInTheFormItWasRead()
    {
        byte[] answered = [.. Secret];
        var ring = new KeyRing();

        ring.Hold("google", ProviderCredential.Secret(answered));
        ring.Hold("apple", ProviderCredential.Signed("TEAM1", "KEY1", Key));
        ring.Fill();

        Array.Clear(answered);

        Assert.Equal(
            Secret,
            ring.BorrowProviderCredential("google", credential => credential.Material.ToArray())
                .Match(read => read, _ => []));
        Assert.Equal(
            (true, "TEAM1", "KEY1"),
            ring.BorrowProviderCredential("apple", credential => (credential.IsSigned, credential.Issuer, credential.KeyId))
                .Match(read => read, _ => default));
        Assert.Equal(
            Key,
            ring.BorrowProviderCredential("apple", credential => credential.Material.ToArray())
                .Match(read => read, _ => []));
    }

    /// <summary>
    /// D-171: asked for a credential it does not hold, the ring answers that the secret
    /// is unavailable and names it.
    /// </summary>
    [Fact]
    public void CONV_CODE_007_ACredentialTheRingDoesNotHoldIsNamedUnavailable()
    {
        Error? refused = Filled()
            .BorrowProviderCredential("facebook", credential => credential.Material.Length)
            .Match(_ => (Error?)null, error => error);

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, refused?.Code);
        Assert.Equal("socialProvider.facebook", refused?.Details["key"].GetString());
    }

    /// <summary>
    /// D-171: the ring is filled once, so nothing is held after it is filled.
    /// </summary>
    [Fact]
    public void CONV_CODE_007_TheRingIsFilledOnce()
    {
        KeyRing ring = Filled();

        Assert.Throws<InvalidOperationException>(() => ring.Hold("google", ProviderCredential.Secret(Secret)));
        Assert.Throws<InvalidOperationException>(ring.Fill);
    }

    /// <summary>
    /// CONV-CODE-007 AC3, D-176: the mail server's key is read in the last step, so a
    /// read of it once the other secrets are filled, before the start chose the mail
    /// server in use, is a fault; once the ring is complete it is lent, and cleared with
    /// the rest.
    /// </summary>
    [Fact]
    public void CONV_CODE_007_AC3_TheMailServerKeyIsReadOnlyOnceItsStepIsDone()
    {
        byte[] answered = [.. Secret];
        KeyRing ring = Filled();

        ring.HoldMailServerSecret(answered);
        Array.Clear(answered);

        Assert.Throws<InvalidOperationException>(() => ring.BorrowMailServerSecret(secret => secret.Length));
        Assert.Equal(3, ring.BorrowProviderCredential("google", credential => 3).Match(read => read, _ => 0));

        ring.Completed();

        Assert.Equal(Secret, ring.BorrowMailServerSecret(secret => secret.ToArray()).Match(read => read, _ => []));
        Assert.Throws<InvalidOperationException>(() => ring.HoldMailServerSecret(Secret));

        ring.Clear();

        Assert.Equal(3, ring.Arrays.Count());
        Assert.All(ring.Arrays, array => Assert.All(array, value => Assert.Equal(0, value)));
        Assert.Throws<InvalidOperationException>(() => ring.BorrowMailServerSecret(secret => secret.Length));
    }

    /// <summary>
    /// D-176: where the start did not choose the library's adapter the ring holds no mail
    /// server key, and asked for one it answers that the secret is unavailable.
    /// </summary>
    [Fact]
    public void CONV_CODE_007_AMailServerKeyNotHeldIsNamedUnavailable()
    {
        KeyRing ring = Filled();

        ring.Completed();

        Error? refused = ring.BorrowMailServerSecret(secret => secret.Length).Match(_ => (Error?)null, error => error);

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, refused?.Code);
        Assert.Equal("mailServerSecret", refused?.Details["key"].GetString());
        Assert.Throws<InvalidOperationException>(ring.Completed);
    }

    /// <summary>
    /// CONV-CODE-007 AC3, D-176: the key-encryption key, the fingerprint key and the
    /// maintenance credential are read in the first step, so a read of any before the
    /// ring is filled is a fault; once it is filled each is lent from the ring's own copy
    /// of every version, whatever became of what the source answered, and the clearing
    /// leaves every array zero, after which a read is a fault again.
    /// </summary>
    [Fact]
    public void CONV_CODE_007_AC3_TheKeysAreLentFromTheRingsOwnCopyUntilItIsCleared()
    {
        byte[] previous = Material(1);
        byte[] current = Material(2);
        byte[] fingerprint = Material(3);
        byte[] maintenance = "Host=maintenance.example.test"u8.ToArray();
        var ring = new KeyRing();

        ring.HoldKeyEncryptionKeys(new KeyEncryptionKeys(2, new Dictionary<int, ReadOnlyMemory<byte>>
        {
            [1] = previous,
            [2] = current,
        }));
        ring.HoldFingerprintKeys(new FingerprintKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = fingerprint }));
        ring.HoldMaintenanceCredential(maintenance);

        Assert.Throws<InvalidOperationException>(() => ring.BorrowKeyEncryptionKeys(keys => keys.CurrentVersion));
        Assert.Throws<InvalidOperationException>(() => ring.BorrowKeyEncryptionKey(2, key => key.Length));
        Assert.Throws<InvalidOperationException>(() => ring.BorrowFingerprintKeys(keys => keys.CurrentVersion));
        Assert.Throws<InvalidOperationException>(() => ring.BorrowMaintenanceCredential(credential => credential.Length));

        ring.Fill();

        byte[][] answered = [[.. previous], [.. current], [.. fingerprint], [.. maintenance]];

        Array.Clear(previous);
        Array.Clear(current);
        Array.Clear(fingerprint);
        Array.Clear(maintenance);

        Assert.Equal(2, ring.BorrowKeyEncryptionKeys(keys => keys.CurrentVersion).Match(read => read, _ => 0));
        Assert.Equal(answered[1], ring.BorrowKeyEncryptionKeys(keys => keys.Current.ToArray()).Match(read => read, _ => []));
        Assert.Equal(answered[0], ring.BorrowKeyEncryptionKey(1, key => key.ToArray()).Match(read => read, _ => []));
        Assert.Equal(answered[2], ring.BorrowFingerprintKeys(keys => keys.Current.ToArray()).Match(read => read, _ => []));
        Assert.Equal(answered[3], ring.BorrowMaintenanceCredential(credential => credential.ToArray()).Match(read => read, _ => []));

        ring.Clear();

        Assert.Equal(4, ring.Arrays.Count());
        Assert.All(ring.Arrays, array => Assert.All(array, value => Assert.Equal(0, value)));
        Assert.Throws<InvalidOperationException>(() => ring.BorrowKeyEncryptionKeys(keys => keys.CurrentVersion));
        Assert.Throws<InvalidOperationException>(() => ring.BorrowKeyEncryptionKey(1, key => key.Length));
        Assert.Throws<InvalidOperationException>(() => ring.BorrowFingerprintKeys(keys => keys.CurrentVersion));
        Assert.Throws<InvalidOperationException>(() => ring.BorrowMaintenanceCredential(credential => credential.Length));
    }

    /// <summary>
    /// CONV-CODE-007: asked for a version of the key-encryption key it does not hold, the
    /// ring answers that the secret is unavailable, naming the key and the version.
    /// </summary>
    [Fact]
    public void CONV_CODE_007_AVersionTheRingDoesNotHoldIsNamedWithItsVersion()
    {
        var ring = new KeyRing();

        ring.HoldKeyEncryptionKeys(new KeyEncryptionKeys(2, new Dictionary<int, ReadOnlyMemory<byte>> { [2] = Material(2) }));
        ring.Fill();

        Error? refused = ring.BorrowKeyEncryptionKey(1, key => key.Length).Match(_ => (Error?)null, error => error);

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, refused?.Code);
        Assert.Equal("keyEncryptionKeys", refused?.Details["key"].GetString());
        Assert.Equal(1, refused?.Details["version"].GetInt32());
    }

    /// <summary>
    /// CONV-CODE-007: asked for a key or the credential it was not filled with, the ring
    /// answers that the secret is unavailable and names it.
    /// </summary>
    [Fact]
    public void CONV_CODE_007_AKeyTheRingDoesNotHoldIsNamedUnavailable()
    {
        KeyRing ring = Filled();

        Assert.Equal("keyEncryptionKeys", Refused(ring.BorrowKeyEncryptionKeys(keys => keys.CurrentVersion)));
        Assert.Equal("fingerprintKeys", Refused(ring.BorrowFingerprintKeys(keys => keys.CurrentVersion)));
        Assert.Equal("maintenanceCredential", Refused(ring.BorrowMaintenanceCredential(credential => credential.Length)));
    }

    /// <summary>
    /// D-171: each key is held once, and only while the ring is being filled.
    /// </summary>
    [Fact]
    public void CONV_CODE_007_EachKeyIsHeldOnce()
    {
        var keys = new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = Material(1) });
        var fingerprints = new FingerprintKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = Material(3) });
        var ring = new KeyRing();

        ring.HoldKeyEncryptionKeys(keys);
        ring.HoldFingerprintKeys(fingerprints);
        ring.HoldMaintenanceCredential(Secret);

        Assert.Throws<InvalidOperationException>(() => ring.HoldKeyEncryptionKeys(keys));
        Assert.Throws<InvalidOperationException>(() => ring.HoldFingerprintKeys(fingerprints));
        Assert.Throws<InvalidOperationException>(() => ring.HoldMaintenanceCredential(Secret));

        KeyRing filled = Filled();

        Assert.Throws<InvalidOperationException>(() => filled.HoldKeyEncryptionKeys(keys));
        Assert.Throws<InvalidOperationException>(() => filled.HoldFingerprintKeys(fingerprints));
        Assert.Throws<InvalidOperationException>(() => filled.HoldMaintenanceCredential(Secret));
    }

    // A key of 32 bytes, each the given value.
    private static byte[] Material(byte value) => [.. Enumerable.Repeat(value, 32)];

    private static string? Refused<TValue>(Result<TValue> borrowed) =>
        borrowed.Match(_ => (Error?)null, error => error)?.Details["key"].GetString();

    private static KeyRing Filled()
    {
        var ring = new KeyRing();

        ring.Hold("google", ProviderCredential.Secret(Secret));
        ring.Hold("apple", ProviderCredential.Signed("TEAM1", "KEY1", Key));
        ring.Fill();

        return ring;
    }
}
