using System;
using System.Security.Cryptography;
using System.Text;
using Janus.Core;

namespace Janus.Authentication.BreakGlass;

/// <summary>
/// The break-glass credential as it is printed and typed: 27 symbols of Crockford
/// base32, 135 bits, in nine groups of three each closed by a check symbol, shown as 36
/// symbols in groups of four.
/// </summary>
/// <remarks>
/// Implements OPS-BOOT-004. A group's check symbol is the sum of its three symbols'
/// values weighted 1, 2 and 3, modulo 32, so a symbol copied wrongly off the sheet is
/// caught before anything is compared. Reading a code back folds case and the letters
/// a reader confuses with digits, as a recovery code is read.
/// </remarks>
internal static class BreakGlassCode
{
    /// <summary>
    /// How many drawn symbols a code carries.
    /// </summary>
    public const int DataSymbols = 27;

    /// <summary>
    /// How many symbols a printed group holds: three drawn and one check.
    /// </summary>
    public const int GroupSize = 4;

    private const int Groups = DataSymbols / (GroupSize - 1);

    private const int Printed = Groups * GroupSize;

    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>
    /// Draws a code, shown as nine groups of four separated by hyphens.
    /// </summary>
    /// <param name="randomness">Where the symbols are drawn from.</param>
    /// <returns>The code as it is printed.</returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public static string Draw(RandomNumberGenerator randomness)
    {
        ArgumentNullException.ThrowIfNull(randomness);

        var drawn = new StringBuilder(Printed + Groups - 1);
        Span<int> values = stackalloc int[GroupSize - 1];

        for (int group = 0; group < Groups; group++)
        {
            if (group > 0)
            {
                drawn.Append('-');
            }

            for (int symbol = 0; symbol < values.Length; symbol++)
            {
                values[symbol] = RandomNumberGenerator.GetInt32(Alphabet.Length);
                drawn.Append(Alphabet[values[symbol]]);
            }

            drawn.Append(Alphabet[Check(values)]);
        }

        return drawn.ToString();
    }

    /// <summary>
    /// The form a code is hashed and compared in, where every group's check symbol
    /// holds: the separators and the case gone and the confusable letters folded.
    /// </summary>
    /// <param name="entered">The code as it was typed or scanned.</param>
    /// <returns>
    /// The 36 canonical symbols, or nothing where the code is not 36 symbols long or a
    /// group's check symbol does not hold.
    /// </returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static string? Checked([NeverLogged] string entered)
    {
        ArgumentNullException.ThrowIfNull(entered);

        var canonical = new StringBuilder(Printed);

        foreach (char typed in entered)
        {
            char folded = Fold(typed);

            if (Alphabet.Contains(folded, StringComparison.Ordinal))
            {
                canonical.Append(folded);
            }
            else if (typed is not ('-' or ' '))
            {
                return null;
            }
        }

        if (canonical.Length != Printed)
        {
            return null;
        }

        Span<int> values = stackalloc int[GroupSize - 1];

        for (int group = 0; group < Groups; group++)
        {
            int start = group * GroupSize;

            for (int symbol = 0; symbol < values.Length; symbol++)
            {
                values[symbol] = Alphabet.IndexOf(canonical[start + symbol], StringComparison.Ordinal);
            }

            if (Alphabet[Check(values)] != canonical[start + GroupSize - 1])
            {
                return null;
            }
        }

        return canonical.ToString();
    }

    /// <summary>
    /// The bytes a checked code is hashed and compared as, which is its canonical
    /// form in UTF-8.
    /// </summary>
    /// <param name="canonical">The code as <see cref="Checked"/> answered it.</param>
    /// <returns>The bytes. The caller clears them.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static byte[] Presented([NeverLogged] string canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);

        return Encoding.UTF8.GetBytes(canonical);
    }

    private static int Check(ReadOnlySpan<int> values)
    {
        int weighted = 0;

        for (int symbol = 0; symbol < values.Length; symbol++)
        {
            weighted += (symbol + 1) * values[symbol];
        }

        return weighted % Alphabet.Length;
    }

    // Crockford base32 reads O as zero and I, L as one, and is case-insensitive.
    private static char Fold(char typed) => char.ToUpperInvariant(typed) switch
    {
        'O' => '0',
        'I' or 'L' => '1',
        char upper => upper,
    };
}
