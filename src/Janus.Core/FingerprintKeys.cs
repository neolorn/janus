using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// The key the deployment computes searchable fingerprints under, with the versions
/// retained beside it until a rotation has computed every stored fingerprint again.
/// </summary>
/// <param name="CurrentVersion">The version every fingerprint is written under.</param>
/// <param name="Versions">Every version a stored fingerprint may still be under, by its number.</param>
/// <remarks>
/// Implements PRIV-RIGHT-005c, OPS-SEC-001 and OPS-SEC-003. A fingerprint carries no
/// key version, so the version it was computed under is recorded beside it, and a
/// lookup matches under every version held here until the rotation retires the
/// previous one.
/// </remarks>
[NeverLogged]
public sealed record FingerprintKeys(
    int CurrentVersion,
    IReadOnlyDictionary<int, ReadOnlyMemory<byte>> Versions)
{
    /// <summary>
    /// The shortest key the library starts on: the length of the hash a fingerprint is,
    /// below which the key weakens the code it is used by (AUTH-KEY-002).
    /// </summary>
    public const int MinimumLength = 32;

    /// <summary>
    /// Every version a stored fingerprint may still be under, by its number.
    /// </summary>
    /// <exception cref="ArgumentNullException">The set is absent.</exception>
    /// <exception cref="ArgumentException">The set is missing the current version.</exception>
    public IReadOnlyDictionary<int, ReadOnlyMemory<byte>> Versions
    {
        get;
        init => field = Usable(value, CurrentVersion);
    } = Usable(Versions, CurrentVersion);

    /// <summary>
    /// The key every fingerprint is written under.
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
                "The current fingerprint key version is not among the versions held.",
                nameof(value));
        }

        return value;
    }
}
