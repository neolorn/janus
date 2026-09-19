using System;
using Janus.Core;

namespace Janus.Privacy.SubjectKeys;

/// <summary>
/// One subject's data key, held wrapped under the deployment's key-encryption key.
/// Erasure overwrites it, and every field encrypted under it becomes unrecoverable at
/// once, including fields in tables the library never touches.
/// </summary>
/// <remarks>Implements PRIV-RIGHT-005a, IDN-PRIN-003, OPS-SEC-003.</remarks>
internal sealed class SubjectKey
{
    private SubjectKey(
        SubjectId subject,
        byte formatMarker,
        int keyVersion,
        ReadOnlyMemory<byte> wrappedKey)
    {
        Subject = subject;
        FormatMarker = formatMarker;
        KeyVersion = keyVersion;
        WrappedKey = wrappedKey;
    }

    /// <summary>
    /// Whose key this is.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// The scheme the wrapped key is written under.
    /// </summary>
    public byte FormatMarker { get; private set; }

    /// <summary>
    /// The version of the key-encryption key this one is wrapped under. A stored value
    /// carries no key version, so the version lives here.
    /// </summary>
    public int KeyVersion { get; private set; }

    /// <summary>
    /// The data key as the key-encryption key wrapped it.
    /// </summary>
    public ReadOnlyMemory<byte> WrappedKey { get; private set; }

    /// <summary>
    /// Whether erasure has overwritten the key.
    /// </summary>
    public bool IsErased => FormatMarker == PersonalDataFormat.ErasedMarker;

    /// <summary>
    /// Records a newly wrapped data key.
    /// </summary>
    /// <param name="subject">Whose key it is.</param>
    /// <param name="keyVersion">The key-encryption key version it is wrapped under.</param>
    /// <param name="wrappedKey">The wrapped key.</param>
    /// <returns>The key as it is stored.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The version is not a version, or the wrapped key is not the length the scheme
    /// produces.
    /// </exception>
    public static SubjectKey Wrapped(SubjectId subject, int keyVersion, ReadOnlyMemory<byte> wrappedKey)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(keyVersion, 1);
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            wrappedKey.Length,
            PersonalDataFormat.WrappedKeyLength,
            nameof(wrappedKey));

        return new SubjectKey(subject, PersonalDataFormat.Marker, keyVersion, wrappedKey);
    }

    /// <summary>
    /// Replaces the wrapping with one under a newer key-encryption key version. No
    /// stored value changes.
    /// </summary>
    /// <param name="keyVersion">The version now wrapping it.</param>
    /// <param name="wrappedKey">The wrapped key.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The version is not later than the one held, or the wrapped key is not the length
    /// the scheme produces.
    /// </exception>
    /// <exception cref="InvalidOperationException">The key has been erased.</exception>
    public void ReWrap(int keyVersion, ReadOnlyMemory<byte> wrappedKey)
    {
        if (IsErased)
        {
            throw new InvalidOperationException("An erased key is never re-wrapped.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(keyVersion, KeyVersion);
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            wrappedKey.Length,
            PersonalDataFormat.WrappedKeyLength,
            nameof(wrappedKey));

        KeyVersion = keyVersion;
        WrappedKey = wrappedKey;
    }

    /// <summary>
    /// Destroys the key by overwriting it. The row stays; every field encrypted under
    /// the key becomes unrecoverable.
    /// </summary>
    public void Erase()
    {
        FormatMarker = PersonalDataFormat.ErasedMarker;
        WrappedKey = new byte[PersonalDataFormat.DataKeyLength];
    }
}
