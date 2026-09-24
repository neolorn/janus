using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Janus.Core;

namespace Janus.Storage;

/// <summary>
/// The keyed fingerprint a searchable identifier is stored and looked up by. The key is
/// held outside the database, so a dump yields nothing even for a candidate space as
/// small as a country's mobile numbers.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-005c and OPS-SEC-003. Case-insensitive matching cannot be done
/// by the database on a fingerprint, so it happens in the canonical form applied before
/// this function. Every fingerprint is written under the current version of the key and
/// looked up under every version held, so one written before a rotation is found until
/// the rotation has computed it again.
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
    /// Computes the fingerprint a write stores: under the current version.
    /// </summary>
    /// <param name="canonical">The canonical form, as UTF-8 bytes.</param>
    /// <param name="keys">The versions of the fingerprint key.</param>
    /// <returns>The fingerprint.</returns>
    public static byte[] Compute(ReadOnlySpan<byte> canonical, FingerprintKeys keys) =>
        Compute(canonical, keys.Current.Span);

    /// <summary>
    /// Computes the fingerprint under every version held, the current first and the
    /// others newest first: what a lookup matches.
    /// </summary>
    /// <param name="canonical">The canonical form, as UTF-8 bytes.</param>
    /// <param name="keys">The versions of the fingerprint key.</param>
    /// <returns>The fingerprints, one to a version.</returns>
    public static IReadOnlyList<byte[]> Candidates(ReadOnlySpan<byte> canonical, FingerprintKeys keys)
    {
        var candidates = new List<byte[]>(keys.Versions.Count) { Compute(canonical, keys) };

        foreach (int version in keys.Versions.Keys.Where(version => version != keys.CurrentVersion).OrderDescending())
        {
            candidates.Add(Compute(canonical, keys.Versions[version].Span));
        }

        return candidates;
    }

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
