using System;
using System.Security.Cryptography;
using System.Text;
using Janus.Core;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The keyed fingerprint an identifier is looked up by, and what erasure leaves in its
/// place (PRIV-RIGHT-005c).
/// </summary>
[Trait("kind", "unit")]
public sealed class FingerprintTests
{
    private static readonly byte[] Address = Encoding.UTF8.GetBytes("ahmed@example.com");

    /// <summary>
    /// PRIV-RIGHT-005c AC2: the function is keyed, so the same identifier under two keys
    /// gives two values and a dump without the key yields nothing.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005c_AC2_TheSameIdentifierUnderTwoKeysGivesTwoFingerprints()
    {
        byte[] first = new byte[32];
        byte[] second = new byte[32];
        RandomNumberGenerator.Fill(first);
        RandomNumberGenerator.Fill(second);

        byte[] one = Fingerprint.Compute(Address, first);
        byte[] other = Fingerprint.Compute(Address, second);

        Assert.NotEqual(one, other);
    }

    /// <summary>
    /// PRIV-RIGHT-005c AC3: the canonical form is computed before the keyed function,
    /// so two casings and two Unicode compositions of one address reach one
    /// fingerprint and a lookup finds the account whichever was typed.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005c_AC3_TwoCasingsAndTwoCompositionsGiveOneFingerprint()
    {
        byte[] key = new byte[32];
        RandomNumberGenerator.Fill(key);

        byte[] entered = Fingerprinted("Ahmed@Example.COM", key);
        byte[] cased = Fingerprinted("ahmed@example.com", key);
        byte[] composed = Fingerprinted("Ahméd@example.com", key);
        byte[] decomposed = Fingerprinted("Ahméd@example.com", key);

        Assert.Equal(entered, cased);
        Assert.Equal(composed, decomposed);
        Assert.NotEqual(cased, composed);
    }

    /// <summary>
    /// PRIV-RIGHT-005c: the fingerprint is the 32 bytes of HMAC-SHA-256 and is
    /// deterministic under one key, which is what makes the lookup possible.
    /// </summary>
    [Fact]
    public void Compute_OneIdentifierUnderOneKey_IsThirtyTwoDeterministicBytes()
    {
        byte[] key = new byte[32];
        RandomNumberGenerator.Fill(key);

        byte[] fingerprint = Fingerprint.Compute(Address, key);

        Assert.Equal(32, fingerprint.Length);
        Assert.Equal(fingerprint, Fingerprint.Compute(Address, key));
    }

    /// <summary>
    /// PRIV-RIGHT-005c AC5: erasure leaves 32 zero bytes where the fingerprint was, and
    /// the row persists.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005c_AC5_TheNeutralisedValueIsThirtyTwoZeroBytes()
    {
        byte[] neutralised = Fingerprint.Neutralised();

        Assert.Equal(new byte[32], neutralised);
        Assert.True(Fingerprint.IsNeutralised(neutralised));
    }

    /// <summary>
    /// PRIV-RIGHT-005c: no lookup path matches a neutralised fingerprint, including one
    /// that presents the neutralised value itself.
    /// </summary>
    [Fact]
    public void Matches_ANeutralisedFingerprint_IsNeverAMatch()
    {
        byte[] neutralised = Fingerprint.Neutralised();

        Assert.False(Fingerprint.Matches(neutralised, neutralised));
        Assert.False(Fingerprint.Matches(neutralised, new byte[32]));
    }

    /// <summary>
    /// PRIV-RIGHT-005c AC1: a live fingerprint matches the identifier it was computed
    /// from and nothing else.
    /// </summary>
    [Fact]
    public void Matches_TheIdentifierItWasComputedFrom_IsAMatch()
    {
        byte[] key = new byte[32];
        RandomNumberGenerator.Fill(key);
        byte[] stored = Fingerprint.Compute(Address, key);

        bool same = Fingerprint.Matches(stored, Fingerprint.Compute(Address, key));

        Assert.True(same);
        Assert.False(Fingerprint.Matches(
            stored,
            Fingerprint.Compute(Encoding.UTF8.GetBytes("mona@example.com"), key)));
    }

    private static byte[] Fingerprinted(string entered, byte[] key) =>
        Fingerprint.Compute(Encoding.UTF8.GetBytes(CanonicalForm.Of(entered)), key);
}
