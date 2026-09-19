using System;
using System.Collections.Generic;

namespace Janus.Core.Unicode;

/// <summary>
/// The normalization forms of UAX #15, over the tables the library carries.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-004. The forms are the library's own so that the pinned Unicode
/// version governs every canonical form it computes, whatever library the machine it
/// runs on happens to have installed.
/// </remarks>
internal static class Normalizer
{
    private const int SyllableBase = 0xAC00;
    private const int LeadingBase = 0x1100;
    private const int VowelBase = 0x1161;
    private const int TrailingBase = 0x11A7;
    private const int LeadingCount = 19;
    private const int VowelCount = 21;
    private const int TrailingCount = 28;
    private const int SyllableBlock = VowelCount * TrailingCount;
    private const int SyllableCount = LeadingCount * SyllableBlock;

    /// <summary>
    /// Normalization Form C.
    /// </summary>
    /// <param name="text">The code points.</param>
    /// <returns>The normalized code points.</returns>
    internal static List<int> Nfc(List<int> text) =>
        Compose(Decompose(
            text,
            NormalizationTables.CanonicalKeys,
            NormalizationTables.CanonicalOffsets,
            NormalizationTables.CanonicalData));

    /// <summary>
    /// Normalization Form D.
    /// </summary>
    /// <param name="text">The code points.</param>
    /// <returns>The normalized code points.</returns>
    internal static List<int> Nfd(List<int> text) => Decompose(
        text,
        NormalizationTables.CanonicalKeys,
        NormalizationTables.CanonicalOffsets,
        NormalizationTables.CanonicalData);

    /// <summary>
    /// Normalization Form KC.
    /// </summary>
    /// <param name="text">The code points.</param>
    /// <returns>The normalized code points.</returns>
    internal static List<int> Nfkc(List<int> text) =>
        Compose(Decompose(
            text,
            NormalizationTables.CompatibilityKeys,
            NormalizationTables.CompatibilityOffsets,
            NormalizationTables.CompatibilityData));

    /// <summary>
    /// The Canonical_Combining_Class of a code point.
    /// </summary>
    /// <param name="code">The code point.</param>
    /// <returns>The class.</returns>
    internal static int CombiningClass(int code) => Ranges.Value(
        NormalizationTables.CombiningClassStarts,
        NormalizationTables.CombiningClassValues,
        code);

    private static List<int> Decompose(List<int> text, int[] keys, int[] offsets, int[] data)
    {
        var decomposed = new List<int>(text.Count + 4);

        foreach (int code in text)
        {
            int syllable = code - SyllableBase;

            if (syllable >= 0 && syllable < SyllableCount)
            {
                decomposed.Add(LeadingBase + (syllable / SyllableBlock));
                decomposed.Add(VowelBase + (syllable % SyllableBlock / TrailingCount));

                if (syllable % TrailingCount != 0)
                {
                    decomposed.Add(TrailingBase + (syllable % TrailingCount));
                }
            }
            else if (Mappings.TryFind(keys, offsets, data, code, out ReadOnlySpan<int> mapping))
            {
                foreach (int part in mapping)
                {
                    decomposed.Add(part);
                }
            }
            else
            {
                decomposed.Add(code);
            }
        }

        Order(decomposed);

        return decomposed;
    }

    private static void Order(List<int> decomposed)
    {
        // The canonical ordering algorithm of UAX #15: an insertion sort, because it
        // must be stable inside one combining class and the runs it moves are short.
        for (int index = 1; index < decomposed.Count; index++)
        {
            int current = CombiningClass(decomposed[index]);

            if (current == 0)
            {
                continue;
            }

            int position = index;

            while (position > 0 && CombiningClass(decomposed[position - 1]) > current)
            {
                (decomposed[position - 1], decomposed[position]) =
                    (decomposed[position], decomposed[position - 1]);
                position--;
            }
        }
    }

    private static List<int> Compose(List<int> decomposed)
    {
        if (decomposed.Count == 0)
        {
            return decomposed;
        }

        var composed = new List<int>(decomposed.Count) { decomposed[0] };
        int starter = 0;
        int lastClass = CombiningClass(decomposed[0]) == 0 ? -1 : 256;

        for (int index = 1; index < decomposed.Count; index++)
        {
            int code = decomposed[index];
            int combining = CombiningClass(code);

            if (lastClass < combining && TryCombine(composed[starter], code, out int result))
            {
                composed[starter] = result;

                continue;
            }

            // A code point is blocked from the starter when something between them is a
            // starter or is of a class not below its own (UAX #15, D115). The sentinel
            // below a class of zero is what "nothing between them" means.
            if (combining == 0)
            {
                starter = composed.Count;
                lastClass = -1;
            }
            else
            {
                lastClass = combining;
            }

            composed.Add(code);
        }

        return composed;
    }

    private static bool TryCombine(int starter, int following, out int composed)
    {
        int leading = starter - LeadingBase;
        int vowel = following - VowelBase;

        if (leading >= 0 && leading < LeadingCount && vowel >= 0 && vowel < VowelCount)
        {
            composed = SyllableBase + (((leading * VowelCount) + vowel) * TrailingCount);

            return true;
        }

        int syllable = starter - SyllableBase;
        int trailing = following - TrailingBase;

        if (syllable >= 0
            && syllable < SyllableCount
            && syllable % TrailingCount == 0
            && trailing > 0
            && trailing < TrailingCount)
        {
            composed = starter + trailing;

            return true;
        }

        return TryPrimaryComposite(starter, following, out composed);
    }

    private static bool TryPrimaryComposite(int starter, int following, out int composed)
    {
        long pair = ((long)starter << 21) | (uint)following;
        int found = Array.BinarySearch(NormalizationTables.CompositionPairs, pair);

        if (found < 0)
        {
            composed = 0;

            return false;
        }

        composed = NormalizationTables.CompositionValues[found];

        return true;
    }
}
