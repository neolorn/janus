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
    /// Wraps a data key under a key-encryption key.
    /// </summary>
    /// <param name="dataKey">The data key.</param>
    /// <param name="keyEncryptionKey">The key-encryption key to wrap it under.</param>
    /// <returns>The wrapped key, as it is stored.</returns>
    public static byte[] Wrap(ReadOnlySpan<byte> dataKey, ReadOnlySpan<byte> keyEncryptionKey)
    {
        using var aes = Aes.Create();
        byte[] material = keyEncryptionKey.ToArray();

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
    /// Unwraps a subject's data key.
    /// </summary>
    /// <param name="formatMarker">The scheme the stored key is written under.</param>
    /// <param name="keyVersion">The key-encryption key version it is wrapped under.</param>
    /// <param name="wrappedKey">The wrapped key as it is stored.</param>
    /// <param name="keyEncryptionKeys">The versions the deployment holds.</param>
    /// <returns>The plaintext data key, to be cleared after use.</returns>
    /// <exception cref="CryptographicException">
    /// The key has been erased, it names a scheme this version does not read, or it is
    /// wrapped under a version the deployment no longer holds.
    /// </exception>
    public static byte[] Unwrap(
        byte formatMarker,
        int keyVersion,
        ReadOnlySpan<byte> wrappedKey,
        KeyEncryptionKeys keyEncryptionKeys)
    {
        ArgumentNullException.ThrowIfNull(keyEncryptionKeys);

        if (formatMarker == PersonalDataFormat.ErasedMarker)
        {
            throw new CryptographicException("The subject's key has been erased.");
        }

        if (formatMarker != PersonalDataFormat.Marker)
        {
            throw new CryptographicException("The subject's key names a scheme this version does not read.");
        }

        if (!keyEncryptionKeys.Versions.TryGetValue(keyVersion, out ReadOnlyMemory<byte> material))
        {
            throw new CryptographicException("The subject's key is wrapped under a retired version.");
        }

        using var aes = Aes.Create();
        byte[] keyEncryptionKey = material.ToArray();

        try
        {
            aes.Key = keyEncryptionKey;
            return aes.DecryptKeyWrapPadded(wrappedKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyEncryptionKey);
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
