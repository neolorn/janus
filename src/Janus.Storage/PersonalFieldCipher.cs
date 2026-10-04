using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Janus.Core;
using Janus.Privacy.SubjectKeys;

namespace Janus.Storage;

/// <summary>
/// Envelope encryption of personal fields: one data key per subject, wrapped under the
/// deployment's key-encryption key, and every value bound to the place it is stored.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-005a. The primitives are the base class library's, as the item
/// requires; what is written here is the format the values are stored in.
/// </remarks>
internal static class PersonalFieldCipher
{
    private const int MarkerLength = 1;
    private const int SubjectLength = 16;
    private const int LengthPrefix = 2;

    /// <summary>
    /// Draws a data key for a subject who has none.
    /// </summary>
    /// <param name="randomness">The randomness the deployment runs on.</param>
    /// <returns>The plaintext data key, to be wrapped and then cleared.</returns>
    public static byte[] NewDataKey(RandomNumberGenerator randomness)
    {
        ArgumentNullException.ThrowIfNull(randomness);

        byte[] dataKey = new byte[PersonalDataFormat.DataKeyLength];
        randomness.GetBytes(dataKey);

        return dataKey;
    }

    /// <summary>
    /// The value erasure overwrites a wrapped key with, wherever one is held: 32 zero
    /// bytes, and no marker byte in them. The subject-key table keeps its marker in a
    /// column of its own beside them; an outbox row's, an invitation's and a mailbox's
    /// key is these bytes alone (PRIV-RIGHT-005a).
    /// </summary>
    /// <returns>The erased value.</returns>
    public static byte[] ErasedKey() => new byte[PersonalDataFormat.DataKeyLength];

    /// <summary>
    /// Whether a wrapped key is the erased value, which every unwrap refuses before it is
    /// tried and which a restore and the erasure ledger's replay read as erased.
    /// </summary>
    /// <param name="wrapped">The wrapped key as it is stored.</param>
    /// <returns>Whether it is 32 zero bytes.</returns>
    public static bool IsErased(ReadOnlySpan<byte> wrapped) =>
        wrapped.Length == PersonalDataFormat.DataKeyLength
        && CryptographicOperations.FixedTimeEquals(wrapped, stackalloc byte[PersonalDataFormat.DataKeyLength]);

    /// <summary>
    /// Wraps a data key under a key-encryption key, or a value that belongs to no subject
    /// under the deployment's data key.
    /// </summary>
    /// <param name="dataKey">The data key or the value.</param>
    /// <param name="wrappingKey">The key to wrap it under.</param>
    /// <returns>The wrapped key, as it is stored.</returns>
    public static byte[] Wrap(ReadOnlySpan<byte> dataKey, ReadOnlySpan<byte> wrappingKey)
    {
        using var aes = Aes.Create();
        byte[] material = wrappingKey.ToArray();

        try
        {
            aes.Key = material;
            return aes.EncryptKeyWrapPadded(dataKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material);
        }
    }

    /// <summary>
    /// Wraps a data key, or a value that belongs to no subject's key, under the current
    /// version of the key-encryption key the ring lends.
    /// </summary>
    /// <param name="dataKey">The data key.</param>
    /// <param name="ring">The key ring the key-encryption key is borrowed from.</param>
    /// <returns>The version it is wrapped under and the wrapped key, as they are stored.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    /// <exception cref="InvalidOperationException">The ring holds no key-encryption key.</exception>
    public static (int Version, byte[] Wrapped) WrapUnderCurrent(byte[] dataKey, IKeyRing ring)
    {
        ArgumentNullException.ThrowIfNull(dataKey);
        ArgumentNullException.ThrowIfNull(ring);

        return ring
            .BorrowKeyEncryptionKeys(keys => (keys.CurrentVersion, Wrap(dataKey, keys.Current.Span)))
            .Match(wrapped => wrapped, error => throw new InvalidOperationException(error.Code.ToString()));
    }

    /// <summary>
    /// The version of the key-encryption key new keys are wrapped under.
    /// </summary>
    /// <param name="ring">The key ring the version is read from.</param>
    /// <returns>The current version.</returns>
    /// <exception cref="ArgumentNullException">The ring is absent.</exception>
    /// <exception cref="InvalidOperationException">The ring holds no key-encryption key.</exception>
    public static int CurrentVersion(IKeyRing ring)
    {
        ArgumentNullException.ThrowIfNull(ring);

        return ring
            .BorrowKeyEncryptionKeys(keys => keys.CurrentVersion)
            .Match(version => version, error => throw new InvalidOperationException(error.Code.ToString()));
    }

    /// <summary>
    /// Unwraps a subject's data key.
    /// </summary>
    /// <param name="formatMarker">The scheme the stored key is written under.</param>
    /// <param name="keyVersion">The key-encryption key version it is wrapped under.</param>
    /// <param name="wrappedKey">The wrapped key as it is stored.</param>
    /// <param name="ring">The key ring the version is borrowed from.</param>
    /// <returns>The plaintext data key, to be cleared after use.</returns>
    /// <exception cref="CryptographicException">
    /// The key has been erased, it names a scheme this version does not read, or it is
    /// wrapped under a version the deployment no longer holds.
    /// </exception>
    public static byte[] Unwrap(
        byte formatMarker,
        int keyVersion,
        ReadOnlyMemory<byte> wrappedKey,
        IKeyRing ring)
    {
        ArgumentNullException.ThrowIfNull(ring);

        if (formatMarker == PersonalDataFormat.ErasedMarker)
        {
            throw new CryptographicException("The subject's key has been erased.");
        }

        if (formatMarker != PersonalDataFormat.Marker)
        {
            throw new CryptographicException("The subject's key names a scheme this version does not read.");
        }

        return ring
            .BorrowKeyEncryptionKey(keyVersion, key => Unwrap(wrappedKey.Span, key.Span))
            .Match(
                dataKey => dataKey,
                _ => throw new CryptographicException("The subject's key is wrapped under a retired version."));
    }

    /// <summary>
    /// Unwraps a value wrapped under the deployment's data key (PRIV-RIGHT-005a, D-166).
    /// </summary>
    /// <param name="wrapped">The wrapped value as it is stored.</param>
    /// <param name="wrappingKey">The deployment's data key.</param>
    /// <returns>The plaintext value, to be cleared after use.</returns>
    /// <exception cref="CryptographicException">
    /// The value is the erased value, which is refused before the unwrap is tried, or it
    /// does not unwrap under the key.
    /// </exception>
    public static byte[] Unwrap(ReadOnlySpan<byte> wrapped, ReadOnlySpan<byte> wrappingKey)
    {
        if (IsErased(wrapped))
        {
            throw new CryptographicException("The key has been erased.");
        }

        using var aes = Aes.Create();
        byte[] material = wrappingKey.ToArray();

        try
        {
            aes.Key = material;
            return aes.DecryptKeyWrapPadded(wrapped);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material);
        }
    }

    /// <summary>
    /// Encrypts one personal value under a subject's data key.
    /// </summary>
    /// <param name="dataKey">The subject's data key.</param>
    /// <param name="location">Where the value is stored.</param>
    /// <param name="plaintext">The value.</param>
    /// <param name="randomness">The randomness the initialisation vector is drawn from.</param>
    /// <returns>The marker, the initialisation vector, the ciphertext and the tag.</returns>
    public static byte[] Encrypt(
        ReadOnlySpan<byte> dataKey,
        in PersonalFieldLocation location,
        ReadOnlySpan<byte> plaintext,
        RandomNumberGenerator randomness)
    {
        ArgumentNullException.ThrowIfNull(randomness);

        byte[] stored = new byte[
            MarkerLength + PersonalDataFormat.NonceLength + plaintext.Length + PersonalDataFormat.TagLength];

        stored[0] = PersonalDataFormat.Marker;
        Span<byte> nonce = stored.AsSpan(MarkerLength, PersonalDataFormat.NonceLength);
        randomness.GetBytes(nonce);

        using var cipher = new AesGcm(dataKey, PersonalDataFormat.TagLength);
        cipher.Encrypt(
            nonce,
            plaintext,
            stored.AsSpan(MarkerLength + PersonalDataFormat.NonceLength, plaintext.Length),
            stored.AsSpan(stored.Length - PersonalDataFormat.TagLength),
            AssociatedData(location));

        return stored;
    }

    /// <summary>
    /// Decrypts one personal value.
    /// </summary>
    /// <param name="dataKey">The subject's data key.</param>
    /// <param name="location">Where the value is stored.</param>
    /// <param name="stored">The stored value.</param>
    /// <returns>The value.</returns>
    /// <exception cref="CryptographicException">
    /// The value is shorter than the format allows, names a scheme this version does
    /// not read, or fails its authentication tag because it was altered or moved.
    /// </exception>
    public static byte[] Decrypt(
        ReadOnlySpan<byte> dataKey,
        in PersonalFieldLocation location,
        ReadOnlySpan<byte> stored)
    {
        int overhead = MarkerLength + PersonalDataFormat.NonceLength + PersonalDataFormat.TagLength;

        if (stored.Length < overhead)
        {
            throw new CryptographicException("The stored value is shorter than the format allows.");
        }

        if (stored[0] != PersonalDataFormat.Marker)
        {
            throw new CryptographicException("The stored value names a scheme this version does not read.");
        }

        byte[] plaintext = new byte[stored.Length - overhead];

        using var cipher = new AesGcm(dataKey, PersonalDataFormat.TagLength);
        cipher.Decrypt(
            stored.Slice(MarkerLength, PersonalDataFormat.NonceLength),
            stored.Slice(MarkerLength + PersonalDataFormat.NonceLength, plaintext.Length),
            stored[^PersonalDataFormat.TagLength..],
            plaintext,
            AssociatedData(location));

        return plaintext;
    }

    // The marker is authenticated along with the location: unauthenticated it would be
    // a downgrade vector, an adversary editing it to force a weaker scheme. Each part
    // carries its length, so that no two locations produce the same bytes.
    private static byte[] AssociatedData(in PersonalFieldLocation location)
    {
        int table = Encoding.UTF8.GetByteCount(location.Table);
        int column = Encoding.UTF8.GetByteCount(location.Column);

        byte[] associated = new byte[
            MarkerLength + SubjectLength + LengthPrefix + table + LengthPrefix + column];

        associated[0] = PersonalDataFormat.Marker;
        location.Subject.Value.TryWriteBytes(associated.AsSpan(MarkerLength), bigEndian: true, out _);

        int at = MarkerLength + SubjectLength;
        BinaryPrimitives.WriteUInt16BigEndian(associated.AsSpan(at), checked((ushort)table));
        Encoding.UTF8.GetBytes(location.Table, associated.AsSpan(at + LengthPrefix));

        at += LengthPrefix + table;
        BinaryPrimitives.WriteUInt16BigEndian(associated.AsSpan(at), checked((ushort)column));
        Encoding.UTF8.GetBytes(location.Column, associated.AsSpan(at + LengthPrefix));

        return associated;
    }
}
