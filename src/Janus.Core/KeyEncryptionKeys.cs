using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// The key-encryption key the deployment wraps subject keys under, with the versions
/// retained beside it until a rotation has re-wrapped every subject key.
/// </summary>
/// <param name="CurrentVersion">The version new subject keys are wrapped under.</param>
/// <param name="Versions">Every version still usable for unwrapping, by its number.</param>
/// <remarks>
/// Implements PRIV-RIGHT-005a and OPS-SEC-003. A stored value carries no key version,
/// so the version a subject key was wrapped under is recorded beside it and the
/// previous version stays here until the rotation reports complete.
/// </remarks>
public sealed record KeyEncryptionKeys(
    int CurrentVersion,
    IReadOnlyDictionary<int, ReadOnlyMemory<byte>> Versions)
{
    /// <summary>
    /// Every version still usable for unwrapping, by its number.
    /// </summary>
    /// <exception cref="ArgumentNullException">The set is absent.</exception>
    /// <exception cref="ArgumentException">
    /// The set is missing the current version, or holds material of a length no AES
    /// key has.
    /// </exception>
    public IReadOnlyDictionary<int, ReadOnlyMemory<byte>> Versions
    {
        get;
        init => field = Usable(value, CurrentVersion);
    } = Usable(Versions, CurrentVersion);

    /// <summary>
    /// The key new subject keys are wrapped under.
    /// </summary>
    public ReadOnlyMemory<byte> Current => Versions[CurrentVersion];

    private static IReadOnlyDictionary<int, ReadOnlyMemory<byte>> Usable(
        IReadOnlyDictionary<int, ReadOnlyMemory<byte>> value,
        int currentVersion)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!value.ContainsKey(currentVersion))
        {
            throw new ArgumentException(
                "The current key-encryption key version is not among the versions held.",
                nameof(value));
        }

        foreach (ReadOnlyMemory<byte> material in value.Values)
        {
            if (material.Length is not (16 or 24 or 32))
            {
                throw new ArgumentException(
                    "A key-encryption key is not an AES key length.",
                    nameof(value));
            }
        }

        return value;
    }
}
