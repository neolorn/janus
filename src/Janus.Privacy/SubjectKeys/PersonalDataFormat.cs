namespace Janus.Privacy.SubjectKeys;

/// <summary>
/// The one scheme personal data is written under, and the lengths it fixes. A stored
/// value names its scheme in its first byte, so a later scheme is read-old-write-new
/// rather than one re-encryption of everything.
/// </summary>
/// <remarks>Implements PRIV-RIGHT-005a.</remarks>
internal static class PersonalDataFormat
{
    /// <summary>
    /// The marker of AES-256-GCM under a per-subject data key wrapped with RFC 5649.
    /// </summary>
    public const byte Marker = 0x01;

    /// <summary>
    /// The marker of a wrapped key that erasure has overwritten. Every unwrap refuses
    /// it, and a restore reads it as erased.
    /// </summary>
    public const byte ErasedMarker = 0x00;

    /// <summary>
    /// The length of a data key.
    /// </summary>
    public const int DataKeyLength = 32;

    /// <summary>
    /// The length of a data key wrapped under the key-encryption key, which RFC 5649
    /// pads to the next eight bytes and prefixes with its own eight.
    /// </summary>
    public const int WrappedKeyLength = 40;

    /// <summary>
    /// The length of the initialisation vector, one per encryption, without which two
    /// subjects holding the same value would produce the same ciphertext.
    /// </summary>
    public const int NonceLength = 12;

    /// <summary>
    /// The length of the authentication tag.
    /// </summary>
    public const int TagLength = 16;
}
