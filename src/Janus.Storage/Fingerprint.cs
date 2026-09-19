using System;
using System.Security.Cryptography;

namespace Janus.Storage;

/// <summary>
/// The keyed fingerprint a searchable identifier is stored and looked up by. The key is
/// held outside the database, so a dump yields nothing even for a candidate space as
/// small as a country's mobile numbers.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-005c. Case-insensitive matching cannot be done by the database
/// on a fingerprint, so it happens in the canonical form applied before this function.
/// </remarks>
internal static class Fingerprint
{
    /// <summary>
    /// The length of a fingerprint, and of the value erasure leaves behind.
    /// </summary>
    public const int Length = 32;

    /// <summary>
    /// Computes the fingerprint of an identifier already in its canonical form.
    /// </summary>
    /// <param name="canonical">The canonical form, as UTF-8 bytes.</param>
    /// <param name="key">The fingerprint key.</param>
    /// <returns>The fingerprint.</returns>
    public static byte[] Compute(ReadOnlySpan<byte> canonical, ReadOnlySpan<byte> key) =>
        HMACSHA256.HashData(key, canonical);

    /// <summary>
    /// The value erasure overwrites a fingerprint with. The row persists and no lookup
    /// path matches it.
    /// </summary>
    /// <returns>The neutralised value.</returns>
    public static byte[] Neutralised() => new byte[Length];

    /// <summary>
    /// Whether a stored fingerprint has been neutralised by erasure.
    /// </summary>
    /// <param name="stored">The stored value.</param>
    /// <returns>Whether erasure overwrote it.</returns>
    public static bool IsNeutralised(ReadOnlySpan<byte> stored) =>
        CryptographicOperations.FixedTimeEquals(stored, stackalloc byte[Length]);

    /// <summary>
    /// Whether a presented identifier's fingerprint is the stored one.
    /// </summary>
    /// <param name="stored">The stored fingerprint.</param>
    /// <param name="presented">The fingerprint of the identifier presented.</param>
    /// <returns>Whether they are the same live fingerprint.</returns>
    public static bool Matches(ReadOnlySpan<byte> stored, ReadOnlySpan<byte> presented) =>
        CryptographicOperations.FixedTimeEquals(stored, presented) & !IsNeutralised(stored);
}
