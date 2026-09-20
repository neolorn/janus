using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Janus.Authentication.Registration;

/// <summary>
/// The code a message carries to prove control of an address or a number: six digits,
/// compared in fixed time.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-004 and REG-SESS-003. It is not an authentication credential
/// and no sign-in endpoint takes one; it dies after
/// <c>code.verification.attempts</c> wrong tries and a replacement draws on the
/// sending restrictions. It is held rather than fingerprinted because a link opened
/// away from the registering browser has to show it (REG-SESS-003), and what is held
/// is encrypted at rest under the key-encryption key (OPS-SEC-001).
/// </remarks>
internal static class VerificationCode
{
    /// <summary>
    /// How many digits a code carries.
    /// </summary>
    public const int Digits = 6;

    private const int Ceiling = 1000000;

    /// <summary>
    /// Draws a code.
    /// </summary>
    /// <param name="randomness">Where the digits are drawn from.</param>
    /// <returns>The code as the person reads it.</returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public static string Draw(RandomNumberGenerator randomness)
    {
        ArgumentNullException.ThrowIfNull(randomness);

        return RandomNumberGenerator.GetInt32(Ceiling)
            .ToString(CultureInfo.InvariantCulture)
            .PadLeft(Digits, '0');
    }

    /// <summary>
    /// What is held against the staged identifier.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The digits, which the store encrypts.</returns>
    /// <exception cref="ArgumentNullException">The code is absent.</exception>
    public static byte[] Held(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return Encoding.UTF8.GetBytes(Canonical(code));
    }

    /// <summary>
    /// The code as the landing page away from the registering browser shows it.
    /// </summary>
    /// <param name="held">What is held.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static string Read(byte[] held)
    {
        ArgumentNullException.ThrowIfNull(held);

        return Encoding.UTF8.GetString(held);
    }

    /// <summary>
    /// Whether a code as typed is the one held.
    /// </summary>
    /// <param name="held">What is held.</param>
    /// <param name="entered">The code as it was typed.</param>
    /// <returns>Whether they match.</returns>
    /// <exception cref="ArgumentNullException">Either is absent.</exception>
    public static bool Matches(byte[] held, string entered)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(entered);

        byte[] presented = Held(entered);

        try
        {
            return CryptographicOperations.FixedTimeEquals(held, presented);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(presented);
        }
    }

    // Spaces and separators are what a person copying six digits off a screen adds.
    private static string Canonical(string entered)
    {
        var canonical = new StringBuilder(Digits);

        foreach (char typed in entered)
        {
            if (char.IsAsciiDigit(typed))
            {
                _ = canonical.Append(typed);
            }
        }

        return canonical.ToString();
    }
}
