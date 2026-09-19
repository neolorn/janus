using System;
using System.Security.Cryptography;
using Janus.Core.Configuration;
using Konscious.Security.Cryptography;

namespace Janus.Authentication.Passwords;

/// <summary>
/// Computes and checks a password hash. The parameters come from the caller, which
/// reads them from configuration, so raising them is a configuration change and not a
/// deployment.
/// </summary>
/// <param name="randomness">Where the salt is drawn from.</param>
/// <remarks>Implements AUTH-PASS-007.</remarks>
internal sealed class Argon2idHasher(RandomNumberGenerator randomness)
{
    private const int SaltLength = 16;
    private const int DigestLength = 32;

    /// <summary>
    /// Hashes a password at the parameters given.
    /// </summary>
    /// <param name="password">The password, in UTF-8. The caller clears it.</param>
    /// <param name="parameters">The memory and the iterations.</param>
    /// <param name="parallelism">The lanes.</param>
    /// <returns>The hash, with the parameters it was computed at.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public PasswordHash Hash(byte[] password, Argon2StrengthClass parameters, int parallelism)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(parameters);

        byte[] salt = new byte[SaltLength];
        randomness.GetBytes(salt);

        return PasswordHash.Of(parameters, parallelism, salt, Digest(password, salt, parameters, parallelism));
    }

    /// <summary>
    /// Checks a password against a stored hash, in time that does not depend on how
    /// much of the digest matched.
    /// </summary>
    /// <param name="password">The password, in UTF-8. The caller clears it.</param>
    /// <param name="hash">The stored hash.</param>
    /// <returns>Whether the password is the one the hash was computed over.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static bool Verify(byte[] password, PasswordHash hash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(hash);

        byte[] computed = Digest(
            password,
            hash.Salt.ToArray(),
            hash.Parameters,
            hash.Parallelism);

        return CryptographicOperations.FixedTimeEquals(computed, hash.Digest.Span);
    }

    private static byte[] Digest(
        byte[] password,
        byte[] salt,
        Argon2StrengthClass parameters,
        int parallelism)
    {
        using var argon = new Argon2id(password)
        {
            Salt = salt,
            MemorySize = parameters.Memory,
            Iterations = parameters.Iterations,
            DegreeOfParallelism = parallelism,
        };

        return argon.GetBytes(DigestLength);
    }
}
