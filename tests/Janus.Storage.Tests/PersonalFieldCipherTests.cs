using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Janus.Core;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The envelope the personal fields are written in: what it binds a value to, what it
/// refuses, and what a rotation and an erasure do to it (PRIV-RIGHT-005a).
/// </summary>
[Trait("kind", "unit")]
public sealed class PersonalFieldCipherTests
{
    private const byte Scheme = 0x01;
    private const byte Erased = 0x00;

    private static readonly SubjectId Ahmed = new(Guid.Parse("11111111-1111-4111-8111-111111111111"));
    private static readonly SubjectId Mona = new(Guid.Parse("22222222-2222-4222-8222-222222222222"));
    private static readonly byte[] Plaintext = Encoding.UTF8.GetBytes("Ahmed Mahmoud");

    /// <summary>
    /// PRIV-RIGHT-005a: the wrap is AES key wrap with padding, checked against the
    /// vector of RFC 5649 section A.1 so that the format is known and not assumed.
    /// </summary>
    [Fact]
    public void Wrap_TheVectorOfRfcFiveSixFourNine_ProducesItsCiphertext()
    {
        byte[] keyEncryptionKey = Convert.FromHexString("5840df6e29b02af1ab493b705bf16ea1ae8338f4dcc176a8");
        byte[] key = Convert.FromHexString("c37b7e6492584340bed12207808941155068f738");

        byte[] wrapped = PersonalFieldCipher.Wrap(key, keyEncryptionKey);

        Assert.Equal(
            "138BDEAA9B8FA7FC61F97742E72248EE5AE6AE5360D1AE6A5F54F373FA543B6A",
            Convert.ToHexString(wrapped));
    }

    /// <summary>
    /// PRIV-RIGHT-005a: a data key goes in wrapped and comes back out, and the wrapped
    /// form is not the key itself.
    /// </summary>
    [Fact]
    public void Unwrap_AKeyWrappedUnderTheCurrentVersion_ReturnsIt()
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        KeyEncryptionKeys keys = OneVersion(1);

        byte[] wrapped = PersonalFieldCipher.Wrap(dataKey, keys.Current.Span);

        Assert.NotEqual(dataKey, wrapped);
        Assert.Equal(dataKey, PersonalFieldCipher.Unwrap(Scheme, 1, wrapped, keys));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC3: two fields of one row may be encrypted for two subjects,
    /// each under its own key and bound to its own column.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC3_TwoFieldsOfOneRowMayNameDifferentSubjects()
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] referrer = PersonalFieldCipher.NewDataKey(randomness);
        byte[] referred = PersonalFieldCipher.NewDataKey(randomness);
        var referrerField = new PersonalFieldLocation(Ahmed, "referrals", "enc_referrer_name");
        var referredField = new PersonalFieldLocation(Mona, "referrals", "enc_referred_name");

        byte[] first = PersonalFieldCipher.Encrypt(referrer, referrerField, Plaintext, randomness);
        byte[] second = PersonalFieldCipher.Encrypt(referred, referredField, Plaintext, randomness);

        Assert.Equal(Plaintext, PersonalFieldCipher.Decrypt(referrer, referrerField, first));
        Assert.Equal(Plaintext, PersonalFieldCipher.Decrypt(referred, referredField, second));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC5: two subjects holding the same value produce different
    /// ciphertext, so the store discloses nothing about who matches whom.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC5_TwoSubjectsWithOneValueProduceDifferentCiphertext()
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);

        byte[] first = PersonalFieldCipher.Encrypt(
            dataKey, new PersonalFieldLocation(Ahmed, "accounts", "enc_legal_name"), Plaintext, randomness);
        byte[] second = PersonalFieldCipher.Encrypt(
            dataKey, new PersonalFieldLocation(Mona, "accounts", "enc_legal_name"), Plaintext, randomness);

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC5: the initialisation vector alone makes two encryptions of one
    /// value differ, so the property does not rest on the subject differing.
    /// </summary>
    [Fact]
    public void Encrypt_OneValueTwice_ProducesDifferentCiphertext()
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        var field = new PersonalFieldLocation(Ahmed, "accounts", "enc_legal_name");

        byte[] first = PersonalFieldCipher.Encrypt(dataKey, field, Plaintext, randomness);
        byte[] second = PersonalFieldCipher.Encrypt(dataKey, field, Plaintext, randomness);

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC6: the marker is authenticated, so altering it fails the value
    /// rather than selecting another algorithm.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC6_AnAlteredMarkerFailsRatherThanSelectingAnAlgorithm()
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        var field = new PersonalFieldLocation(Ahmed, "accounts", "enc_legal_name");
        byte[] stored = PersonalFieldCipher.Encrypt(dataKey, field, Plaintext, randomness);

        stored[0] = 0x02;

        Assert.ThrowsAny<CryptographicException>(() => PersonalFieldCipher.Decrypt(dataKey, field, stored));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC7: the scheme a value is read under is the byte the value
    /// carries and not the one the deployment writes today, so a value of this scheme
    /// keeps reading with a value of a later one, of whatever shape, stored beside it.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC7_AValueOfTheEarlierSchemeReadsBesideALaterOne()
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        var field = new PersonalFieldLocation(Ahmed, "accounts", "enc_legal_name");
        byte[] earlier = PersonalFieldCipher.Encrypt(dataKey, field, Plaintext, randomness);

        byte[] later = [0x02, .. RandomNumberGenerator.GetBytes(24), .. earlier[1..]];

        Assert.Equal(Scheme, earlier[0]);
        Assert.Equal(Plaintext, PersonalFieldCipher.Decrypt(dataKey, field, earlier));
        Assert.ThrowsAny<CryptographicException>(() => PersonalFieldCipher.Decrypt(dataKey, field, later));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC9: once the wrapped key is overwritten nothing encrypted under
    /// it can be read, wherever the ciphertext was written.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC9_AnErasedWrappedKeyNeverUnwraps()
    {
        KeyEncryptionKeys keys = OneVersion(1);
        byte[] erased = new byte[32];

        Assert.ThrowsAny<CryptographicException>(() =>
            PersonalFieldCipher.Unwrap(Erased, 1, erased, keys));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC10: a value moved to another column, another table or another
    /// subject fails its authentication tag.
    /// </summary>
    [Theory]
    [InlineData("accounts", "enc_display_name")]
    [InlineData("profiles", "enc_legal_name")]
    public void PRIV_RIGHT_005a_AC10_AValueMovedElsewhereDoesNotDecrypt(string table, string column)
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        var written = new PersonalFieldLocation(Ahmed, "accounts", "enc_legal_name");
        byte[] stored = PersonalFieldCipher.Encrypt(dataKey, written, Plaintext, randomness);

        var moved = new PersonalFieldLocation(Ahmed, table, column);

        Assert.ThrowsAny<CryptographicException>(() => PersonalFieldCipher.Decrypt(dataKey, moved, stored));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC10: the subject is bound too, so one subject's ciphertext moved
    /// onto another's row does not decrypt.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC10_AValueMovedToAnotherSubjectDoesNotDecrypt()
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        var written = new PersonalFieldLocation(Ahmed, "accounts", "enc_legal_name");
        byte[] stored = PersonalFieldCipher.Encrypt(dataKey, written, Plaintext, randomness);

        var moved = new PersonalFieldLocation(Mona, "accounts", "enc_legal_name");

        Assert.ThrowsAny<CryptographicException>(() => PersonalFieldCipher.Decrypt(dataKey, moved, stored));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC13: a rotation re-wraps the subject key and leaves every stored
    /// value exactly as it was.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC13_ARotationReWrapsTheKeyAndChangesNoStoredValue()
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        var field = new PersonalFieldLocation(Ahmed, "accounts", "enc_legal_name");
        KeyEncryptionKeys first = OneVersion(1);
        byte[] stored = PersonalFieldCipher.Encrypt(dataKey, field, Plaintext, randomness);
        byte[] wrapped = PersonalFieldCipher.Wrap(dataKey, first.Current.Span);

        KeyEncryptionKeys rotated = TwoVersions(first.Current, randomness);
        byte[] reWrapped = PersonalFieldCipher.Wrap(
            PersonalFieldCipher.Unwrap(Scheme, 1, wrapped, rotated),
            rotated.Current.Span);

        Assert.Equal(
            Plaintext,
            PersonalFieldCipher.Decrypt(
                PersonalFieldCipher.Unwrap(Scheme, 2, reWrapped, rotated),
                field,
                stored));
    }

    /// <summary>
    /// OPS-SEC-003 AC3: once a version is retired a key still wrapped under it is
    /// refused with a named error rather than read under another version.
    /// </summary>
    [Fact]
    public void OPS_SEC_003_AC3_AKeyUnderARetiredVersionFailsWithANamedError()
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        KeyEncryptionKeys keys = OneVersion(2);

        byte[] wrapped = PersonalFieldCipher.Wrap(dataKey, keys.Current.Span);

        CryptographicException refused = Assert.ThrowsAny<CryptographicException>(
            () => PersonalFieldCipher.Unwrap(Scheme, 1, wrapped, keys));

        Assert.Equal("The subject's key is wrapped under a retired version.", refused.Message);
    }

    /// <summary>
    /// PRIV-RIGHT-005a: a value shorter than the marker, the initialisation vector and
    /// the tag is refused before any key is used on it.
    /// </summary>
    [Fact]
    public void Decrypt_AValueShorterThanTheFormat_Throws()
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        var field = new PersonalFieldLocation(Ahmed, "accounts", "enc_legal_name");

        Assert.ThrowsAny<CryptographicException>(() =>
            PersonalFieldCipher.Decrypt(dataKey, field, new byte[8]));
    }

    private static KeyEncryptionKeys OneVersion(int version)
    {
        byte[] material = new byte[32];
        RandomNumberGenerator.Fill(material);

        return new KeyEncryptionKeys(
            version,
            new Dictionary<int, ReadOnlyMemory<byte>> { [version] = material });
    }

    private static KeyEncryptionKeys TwoVersions(ReadOnlyMemory<byte> first, RandomNumberGenerator randomness)
    {
        byte[] second = new byte[32];
        randomness.GetBytes(second);

        return new KeyEncryptionKeys(
            2,
            new Dictionary<int, ReadOnlyMemory<byte>> { [1] = first, [2] = second });
    }
}
