using System;
using System.Collections.Generic;

namespace Janus.Core.Unicode;

/// <summary>
/// The canonical form every identifier is stored and compared under, and the digit
/// mapping a phone number takes instead.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-004. <c>toNFKC_Casefold</c> maps each code point through the
/// NFKC_Casefold property and normalizes the result to Form C. The two steps around
/// that mapping are not optional. Form D comes first because a handful of code points,
/// U+0345 among them, map to a starter, so a combining mark that was reordered by the
/// form the value arrived in would otherwise be mapped in a different place and give a
/// different answer for the same string. Form C comes last because two code points that
/// were separate before the mapping can compose after it.
/// </remarks>
internal static class Canonicalization
{
    private const int AsciiZero = 0x0030;

    /// <summary>
    /// The NFKC_Casefold form of a string.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <returns>The canonical form.</returns>
    internal static string Casefold(string value)
    {
        List<int> codes = Normalizer.Nfd(Text.Read(value));
        var mapped = new List<int>(codes.Count);

        foreach (int code in codes)
        {
            if (Mappings.TryFind(
                CanonicalFormTables.CasefoldKeys,
                CanonicalFormTables.CasefoldOffsets,
                CanonicalFormTables.CasefoldData,
                code,
                out ReadOnlySpan<int> mapping))
            {
                foreach (int part in mapping)
                {
                    mapped.Add(part);
                }

                continue;
            }

            mapped.Add(code);
        }

        return Text.Write(Normalizer.Nfc(mapped));
    }

    /// <summary>
    /// A string with every decimal digit, of whatever script, written as its ASCII
    /// digit. Everything else is left as it stands.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <returns>The string with ASCII digits.</returns>
    internal static string AsciiDigits(string value)
    {
        List<int> codes = Text.Read(value);

        for (int index = 0; index < codes.Count; index++)
        {
            int found = Array.BinarySearch(CanonicalFormTables.DigitKeys, codes[index]);

            if (found >= 0)
            {
                codes[index] = AsciiZero + CanonicalFormTables.DigitValues[found];
            }
        }

        return Text.Write(codes);
    }
}
