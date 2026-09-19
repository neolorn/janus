using System;
using System.Buffers.Text;
using System.Globalization;
using Janus.Core.Configuration;

namespace Janus.Authentication.Passwords;

/// <summary>
/// A stored password hash: the digest, the salt it was drawn over, and the parameters
/// it was computed at, in the encoding the algorithm's own reference form uses.
/// </summary>
/// <remarks>
/// Implements AUTH-PASS-007. The parameters travel with the hash rather than being
/// read from configuration at verification, so raising them leaves every existing
/// password verifiable and marks it for rehash on the next successful sign-in.
/// </remarks>
internal sealed record PasswordHash
{
    private const string Prefix = "$argon2id$v=19$";

    private PasswordHash(Argon2StrengthClass parameters, int parallelism, byte[] salt, byte[] digest)
    {
        Parameters = parameters;
        Parallelism = parallelism;
        Salt = salt;
        Digest = digest;
    }

    /// <summary>
    /// The memory and the iterations the digest was computed at.
    /// </summary>
    public Argon2StrengthClass Parameters { get; }

    /// <summary>
    /// The lanes the digest was computed over.
    /// </summary>
    public int Parallelism { get; }

    /// <summary>
    /// The salt, which is stored and is not a secret.
    /// </summary>
    public ReadOnlyMemory<byte> Salt { get; }

    /// <summary>
    /// The digest.
    /// </summary>
    public ReadOnlyMemory<byte> Digest { get; }

    /// <summary>
    /// The one string the row holds.
    /// </summary>
    public string Encoded => string.Create(
        CultureInfo.InvariantCulture,
        $"{Prefix}m={Parameters.Memory},t={Parameters.Iterations},p={Parallelism}${Base64Url.EncodeToString(Salt.Span)}${Base64Url.EncodeToString(Digest.Span)}");

    /// <summary>
    /// A hash just computed.
    /// </summary>
    /// <param name="parameters">The memory and the iterations.</param>
    /// <param name="parallelism">The lanes.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="digest">The digest.</param>
    /// <returns>The hash.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static PasswordHash Of(
        Argon2StrengthClass parameters,
        int parallelism,
        byte[] salt,
        byte[] digest)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(digest);

        return new PasswordHash(parameters, parallelism, salt, digest);
    }

    /// <summary>
    /// The hash a stored row holds.
    /// </summary>
    /// <param name="encoded">The stored string.</param>
    /// <returns>The hash.</returns>
    /// <exception cref="ArgumentNullException">The string is absent.</exception>
    /// <exception cref="FormatException">
    /// The string is not a hash this library wrote. A row it cannot read is a fault
    /// and never a password that fails to verify.
    /// </exception>
    public static PasswordHash Parse(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        if (!encoded.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new FormatException("The stored hash is not in the form this library writes.");
        }

        string[] parts = encoded[Prefix.Length..].Split('$');

        if (parts.Length != 3)
        {
            throw new FormatException("The stored hash is not in the form this library writes.");
        }

        string[] costs = parts[0].Split(',');

        if (costs.Length != 3)
        {
            throw new FormatException("The stored hash is not in the form this library writes.");
        }

        return new PasswordHash(
            new Argon2StrengthClass(Cost(costs[0], "m="), Cost(costs[1], "t=")),
            Cost(costs[2], "p="),
            Base64Url.DecodeFromChars(parts[1]),
            Base64Url.DecodeFromChars(parts[2]));
    }

    private static int Cost(string part, string name) =>
        part.StartsWith(name, StringComparison.Ordinal)
        && int.TryParse(part[name.Length..], CultureInfo.InvariantCulture, out int value)
            ? value
            : throw new FormatException("The stored hash is not in the form this library writes.");
}
