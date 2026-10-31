using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        Assert.Equal(dataKey, PersonalFieldCipher.Unwrap(Scheme, 1, wrapped, Ring(keys)));
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
            PersonalFieldCipher.Unwrap(Erased, 1, erased, Ring(keys)));
    }

    /// <summary>
    /// PRIV-RIGHT-005a: the erased value is 32 zero bytes with no marker byte in them,
    /// wherever a wrapped key is held, and an unwrap under the deployment's data key
    /// refuses it before it is tried; a value of another length, or one that is not all
    /// zero, is not the erased value.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_TheErasedValueIsThirtyTwoZeroBytesAndIsRefusedBeforeTheUnwrap()
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] deploymentKey = PersonalFieldCipher.NewDataKey(randomness);
        byte[] wrapped = PersonalFieldCipher.Wrap(PersonalFieldCipher.NewDataKey(randomness), deploymentKey);

        byte[] erased = PersonalFieldCipher.ErasedKey();

        Assert.Equal(new byte[32], erased);
        Assert.True(PersonalFieldCipher.IsErased(erased));
        Assert.False(PersonalFieldCipher.IsErased(new byte[33]));
        Assert.False(PersonalFieldCipher.IsErased(new byte[wrapped.Length]));
        Assert.False(PersonalFieldCipher.IsErased(wrapped));

        CryptographicException refused = Assert.Throws<CryptographicException>(() =>
            PersonalFieldCipher.Unwrap(erased, deploymentKey));

        Assert.Equal("The key has been erased.", refused.Message);
        Assert.Equal(32, PersonalFieldCipher.Unwrap(wrapped, deploymentKey).Length);
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
            PersonalFieldCipher.Unwrap(Scheme, 1, wrapped, Ring(rotated)),
            rotated.Current.Span);

        Assert.Equal(
            Plaintext,
            PersonalFieldCipher.Decrypt(
                PersonalFieldCipher.Unwrap(Scheme, 2, reWrapped, Ring(rotated)),
                field,
                stored));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC16: the key-encryption key wraps the rows of the subject-key
    /// table and nothing else. The one wrap under it is called by the three stores that
    /// write such a row, a subject's, the deployment's and the rotation's re-wrap; the
    /// other sources that borrow it unwrap such a row, derive the token protection key
    /// (AUTH-KEY-002) or read which versions are held; and the
    /// key wrap itself is written in one place.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC16_OnlyTheStoresOfTheSubjectKeyTableWrapUnderTheKeyEncryptionKey()
    {
        Assert.Equal(
            ["DeploymentDataKeyStore.cs", "KeyRotationStore.cs", "SubjectKeyStore.cs"],
            Sources("PersonalFieldCipher.WrapUnderCurrent("));
        Assert.Equal(
            ["KeyRingService.cs", "KeyRotation.cs", "PersonalFieldCipher.cs", "TokenProtection.cs"],
            Sources(".BorrowKeyEncryptionKey"));
        Assert.Equal(["PersonalFieldCipher.cs"], Sources("EncryptKeyWrapPadded("));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC16: a rotation re-wraps the two kinds of row the table holds, a
    /// subject's key and the deployment's data key, and touches no value under either.
    /// With the previous version retired, a subject's field and a value of no subject
    /// wrapped under the deployment's key both read as they were written.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC16_AfterRetirementASubjectsValueAndAValueOfNoSubjectStillDecrypt()
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] subjectKey = PersonalFieldCipher.NewDataKey(randomness);
        byte[] deploymentKey = PersonalFieldCipher.NewDataKey(randomness);
        byte[] secret = RandomNumberGenerator.GetBytes(43);
        var field = new PersonalFieldLocation(Ahmed, "accounts", "enc_legal_name");
        KeyEncryptionKeys first = OneVersion(1);

        byte[] personal = PersonalFieldCipher.Encrypt(subjectKey, field, Plaintext, randomness);
        byte[] held = PersonalFieldCipher.Wrap(secret, deploymentKey);
        (int Version, byte[] Wrapped) ofSubject = PersonalFieldCipher.WrapUnderCurrent(subjectKey, Ring(first));
        (int Version, byte[] Wrapped) ofDeployment = PersonalFieldCipher.WrapUnderCurrent(deploymentKey, Ring(first));

        KeyEncryptionKeys rotating = TwoVersions(first.Current, randomness);

        ofSubject = PersonalFieldCipher.WrapUnderCurrent(
            PersonalFieldCipher.Unwrap(Scheme, ofSubject.Version, ofSubject.Wrapped, Ring(rotating)),
            Ring(rotating));
        ofDeployment = PersonalFieldCipher.WrapUnderCurrent(
            PersonalFieldCipher.Unwrap(Scheme, ofDeployment.Version, ofDeployment.Wrapped, Ring(rotating)),
            Ring(rotating));

        var retired = new KeyEncryptionKeys(
            2,
            new Dictionary<int, ReadOnlyMemory<byte>> { [2] = rotating.Current });

        Assert.Equal((2, 2), (ofSubject.Version, ofDeployment.Version));
        Assert.Equal(
            Plaintext,
            PersonalFieldCipher.Decrypt(
                PersonalFieldCipher.Unwrap(Scheme, ofSubject.Version, ofSubject.Wrapped, Ring(retired)),
                field,
                personal));
        Assert.Equal(
            secret,
            PersonalFieldCipher.Unwrap(
                held,
                PersonalFieldCipher.Unwrap(Scheme, ofDeployment.Version, ofDeployment.Wrapped, Ring(retired))));
    }

    /// <summary>
    /// OPS-SEC-003 AC3 (D-183): once a version is retired a key still wrapped under it
    /// fails to unwrap as a fault, never read under another version and never a result
    /// its callers are handed; the fault carries the code, the key and the version.
    /// </summary>
    [Fact]
    public void OPS_SEC_003_AC3_AKeyUnderARetiredVersionFailsWithANamedError()
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        KeyEncryptionKeys keys = OneVersion(2);

        byte[] wrapped = PersonalFieldCipher.Wrap(dataKey, keys.Current.Span);

        CodedFault fault = Assert.Throws<CodedFault>(
            () => PersonalFieldCipher.Unwrap(Scheme, 1, wrapped, Ring(keys)));

        Assert.Equal(ErrorCodes.StartupSecretUnavailable, fault.Failure.Code);
        Assert.Equal("keyEncryptionKeys", fault.Failure.Details["key"].GetString());
        Assert.Equal(1, fault.Failure.Details["version"].GetInt32());
    }

    /// <summary>
    /// OPS-SEC-003 AC3 (D-183): the erased value is read before the version is asked for,
    /// so an erased key under a version the application no longer holds reads as erased
    /// and is no fault.
    /// </summary>
    [Fact]
    public void OPS_SEC_003_AC3_AnErasedKeyUnderARetiredVersionReadsAsErased()
    {
        KeyEncryptionKeys keys = OneVersion(2);

        CryptographicException erased = Assert.Throws<CryptographicException>(
            () => PersonalFieldCipher.Unwrap(Erased, 1, new byte[32], Ring(keys)));

        Assert.IsNotType<CodedFault>(erased);
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

    /// <summary>
    /// PRIV-RIGHT-005a AC20 (D-203): a host's value is AES-256-GCM under the marker, its
    /// own initialisation vector and the tag, and its authenticated data is the marker,
    /// the subject's 16 bytes in network order, then the mark <c>host</c>, the type's
    /// declared name and the field's declared name, each behind its length in two bytes,
    /// big-endian. The value is opened here with the primitive alone, over data laid out
    /// by hand, so the layout is the one the item states and not the one the code chose.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC20_AHostValueIsBoundToTheMarkTheTypeAndTheFieldAsLaidOut()
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        var field = new HostFieldLocation(Ahmed, ResourceType.Parse("delivery"), "RecipientName");

        byte[] stored = PersonalFieldCipher.Encrypt(dataKey, field, Plaintext, randomness);

        byte[] associated =
        [
            Scheme,
            .. Ahmed.Value.ToByteArray(bigEndian: true),
            0x00, 0x04, .. "host"u8,
            0x00, 0x08, .. "delivery"u8,
            0x00, 0x0D, .. "RecipientName"u8,
        ];
        byte[] opened = new byte[Plaintext.Length];

        using var cipher = new AesGcm(dataKey, 16);
        cipher.Decrypt(stored.AsSpan(1, 12), stored.AsSpan(13, Plaintext.Length), stored.AsSpan(^16), opened, associated);

        Assert.Equal(Scheme, stored[0]);
        Assert.Equal(1 + 12 + Plaintext.Length + 16, stored.Length);
        Assert.Equal(Plaintext, opened);
        Assert.Equal(Plaintext, PersonalFieldCipher.Decrypt(dataKey, field, stored));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC20: a host's value moved to another subject, another type or
    /// another field fails its authentication tag.
    /// </summary>
    /// <param name="moved">Where the value is read back from.</param>
    [Theory]
    [InlineData("subject")]
    [InlineData("type")]
    [InlineData("field")]
    public void PRIV_RIGHT_005a_AC20_AHostValueMovedElsewhereDoesNotDecrypt(string moved)
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        var written = new HostFieldLocation(Ahmed, ResourceType.Parse("delivery"), "RecipientName");
        byte[] stored = PersonalFieldCipher.Encrypt(dataKey, written, Plaintext, randomness);

        HostFieldLocation elsewhere = moved switch
        {
            "subject" => written with { Subject = Mona },
            "type" => written with { Type = ResourceType.Parse("parcel") },
            _ => written with { Field = "RecipientPhone" },
        };

        Assert.ThrowsAny<CryptographicException>(() => PersonalFieldCipher.Decrypt(dataKey, elsewhere, stored));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC20: a host's value carries three named parts and a library's
    /// two, so a host's value moved into a column of the library's does not decrypt, nor
    /// a library value moved into a host's field, whatever names the host chose, the
    /// mark itself and a library table's name among them.
    /// </summary>
    /// <param name="type">The type's declared name.</param>
    /// <param name="field">The field's declared name.</param>
    [Theory]
    [InlineData("delivery", "RecipientName")]
    [InlineData("host", "host")]
    [InlineData("accounts", "enc_legal_name")]
    public void PRIV_RIGHT_005a_AC20_AHostValueAndALibraryValueNeverStandForEachOther(string type, string field)
    {
        using var randomness = RandomNumberGenerator.Create();
        byte[] dataKey = PersonalFieldCipher.NewDataKey(randomness);
        var hosted = new HostFieldLocation(Ahmed, ResourceType.Parse(type), field);
        PersonalFieldLocation[] libraryPlaces =
        [
            new(Ahmed, "host", type),
            new(Ahmed, type, field),
            new(Ahmed, "accounts", "enc_legal_name"),
        ];

        byte[] hostValue = PersonalFieldCipher.Encrypt(dataKey, hosted, Plaintext, randomness);

        foreach (PersonalFieldLocation library in libraryPlaces)
        {
            byte[] libraryValue = PersonalFieldCipher.Encrypt(dataKey, library, Plaintext, randomness);

            Assert.ThrowsAny<CryptographicException>(() => PersonalFieldCipher.Decrypt(dataKey, library, hostValue));
            Assert.ThrowsAny<CryptographicException>(() => PersonalFieldCipher.Decrypt(dataKey, hosted, libraryValue));
        }
    }

    // The library sources carrying the text outside a comment, by file name.
    private static IReadOnlyList<string> Sources(string text)
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at is not null && !Directory.Exists(Path.Combine(at.FullName, "src")))
        {
            at = at.Parent;
        }

        return
        [
            .. Directory
                .EnumerateFiles(Path.Combine(at!.FullName, "src"), "*.cs", SearchOption.AllDirectories)
                .Where(file => File.ReadLines(file).Any(line =>
                    !line.TrimStart().StartsWith('/') && line.Contains(text, StringComparison.Ordinal)))
                .Select(file => Path.GetFileName(file)!)
                .Order(StringComparer.Ordinal),
        ];
    }

    // The ring a store borrows the versions from, holding these alone.
    private static KeyRingInMemory Ring(KeyEncryptionKeys keys) => new(keys, Deployment.FingerprintKeys);

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
