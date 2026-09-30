using System;
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

    private static KeyRing Filled()
    {
        var ring = new KeyRing();

        ring.Hold("google", ProviderCredential.Secret(Secret));
        ring.Hold("apple", ProviderCredential.Signed("TEAM1", "KEY1", Key));
        ring.Fill();

        return ring;
    }
}
