using System;
using System.Security.Cryptography;
using System.Text;

namespace Janus.Authentication.Factors;

/// <summary>
/// One recovery code: ten symbols of Crockford base32, about fifty bits, shown as two
/// groups of five.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-008 and chapter 10 section 5. The alphabet excludes the
/// letters a reader confuses with digits, and reading a code back folds those
/// confusions in, so a code copied off a sheet of paper by hand is accepted.
/// </remarks>
internal static class RecoveryCode
{
    /// <summary>
    /// How many symbols a code carries.
    /// </summary>
    public const int Symbols = 10;

    /// <summary>
    /// How many symbols a group is shown in.
    /// </summary>
    public const int GroupSize = 5;

    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>
    /// Draws a code, shown as two groups of five.
    /// </summary>
    /// <param name="randomness">Where the symbols are drawn from.</param>
    /// <returns>The code as the person reads it.</returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public static string Draw(RandomNumberGenerator randomness)
    {
        ArgumentNullException.ThrowIfNull(randomness);

        var drawn = new StringBuilder(Symbols + 1);

        for (int symbol = 0; symbol < Symbols; symbol++)
        {
            if (symbol == GroupSize)
            {
                drawn.Append('-');
            }

            drawn.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        }

        return drawn.ToString();
    }

    /// <summary>
    /// The form a code is hashed and compared in: the separators and the case gone,
    /// and the symbols a reader confuses with digits folded into them.
    /// </summary>
    /// <param name="entered">The code as it was typed.</param>
    /// <returns>The canonical symbols, empty where none of it is a symbol.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static string Canonical(string entered)
    {
        ArgumentNullException.ThrowIfNull(entered);

        var canonical = new StringBuilder(Symbols);

        foreach (char typed in entered)
        {
            char folded = Fold(typed);

            if (Alphabet.Contains(folded, StringComparison.Ordinal))
            {
                canonical.Append(folded);
            }
        }

        return canonical.ToString();
    }

    /// <summary>
    /// The bytes a code is hashed and compared as, which is its canonical form in
    /// UTF-8.
    /// </summary>
    /// <param name="entered">The code as it was typed.</param>
    /// <returns>The bytes. The caller clears them.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static byte[] Presented(string entered) =>
        Encoding.UTF8.GetBytes(Canonical(entered));

    // Crockford base32 reads O as zero and I, L as one, and is case-insensitive.
    private static char Fold(char typed) => char.ToUpperInvariant(typed) switch
    {
        'O' => '0',
        'I' or 'L' => '1',
        char upper => upper,
    };
}
