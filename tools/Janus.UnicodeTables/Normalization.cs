using System;
using System.Collections.Generic;

namespace Janus.UnicodeTables;

/// <summary>
/// The normalization forms of UAX #15, computed here so that the tables can be derived
/// and checked against the database rather than taken on trust.
/// </summary>
/// <remarks>
/// The decompositions the database gives are one step deep. Expanding them here, once,
/// means the library performs no recursion at run time.
/// </remarks>
internal sealed class Normalization
{
    /// <summary>
    /// The first Hangul syllable.
    /// </summary>
    internal const int SyllableBase = 0xAC00;

    /// <summary>
    /// The first Hangul leading jamo.
    /// </summary>
    internal const int LeadingBase = 0x1100;

    /// <summary>
    /// The first Hangul vowel jamo.
    /// </summary>
    internal const int VowelBase = 0x1161;

    /// <summary>
    /// The code point one below the first Hangul trailing jamo, which is how UAX #15
    /// writes the arithmetic.
    /// </summary>
    internal const int TrailingBase = 0x11A7;

    /// <summary>
    /// The number of Hangul leading jamo.
    /// </summary>
    internal const int LeadingCount = 19;

    /// <summary>
    /// The number of Hangul vowel jamo.
    /// </summary>
    internal const int VowelCount = 21;

    /// <summary>
    /// The number of Hangul trailing jamo, counting the absence of one.
    /// </summary>
    internal const int TrailingCount = 28;

    /// <summary>
    /// The number of Hangul syllables sharing a leading jamo.
    /// </summary>
    internal const int SyllableBlock = VowelCount * TrailingCount;

    /// <summary>
    /// The number of Hangul syllables.
    /// </summary>
    internal const int SyllableCount = LeadingCount * SyllableBlock;

    private readonly UnicodeCharacterDatabase _database;

    /// <summary>
    /// Expands the database's one-step decompositions and builds the composition table.
    /// </summary>
    /// <param name="database">The properties of every code point.</param>
    internal Normalization(UnicodeCharacterDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        _database = database;
        Canonical = Expand(database, compatibility: false);
        Compatibility = Expand(database, compatibility: true);
        Composition = Compose(database);
    }

    /// <summary>
    /// The fully expanded canonical decomposition of every code point that has one.
    /// </summary>
    internal Dictionary<int, int[]> Canonical { get; }

    /// <summary>
    /// The fully expanded compatibility decomposition of every code point that has one.
    /// </summary>
    internal Dictionary<int, int[]> Compatibility { get; }

    /// <summary>
    /// The primary composites, by the starter and the combining mark that form them.
    /// </summary>
    internal Dictionary<long, int> Composition { get; }

    /// <summary>
    /// Packs a starter and the code point following it into one key.
    /// </summary>
    /// <param name="starter">The starter.</param>
    /// <param name="following">The code point following it.</param>
    /// <returns>The key of the pair.</returns>
    internal static long Pair(int starter, int following) => ((long)starter << 21) | (uint)following;

    /// <summary>
    /// Normalization Form C.
    /// </summary>
    /// <param name="text">The code points.</param>
    /// <returns>The normalized code points.</returns>
    internal int[] Nfc(IReadOnlyList<int> text) => Recompose(Decompose(text, Canonical));

    /// <summary>
    /// Normalization Form KC.
    /// </summary>
    /// <param name="text">The code points.</param>
    /// <returns>The normalized code points.</returns>
    internal int[] Nfkc(IReadOnlyList<int> text) => Recompose(Decompose(text, Compatibility));

    private static Dictionary<int, int[]> Expand(UnicodeCharacterDatabase database, bool compatibility)
    {
        var expanded = new Dictionary<int, int[]>();

        foreach (int code in Sources(database, compatibility))
        {
            var sequence = new List<int>(4);

            Walk(database, code, compatibility, sequence);
            expanded[code] = [.. sequence];
        }

        return expanded;
    }

    private static IEnumerable<int> Sources(UnicodeCharacterDatabase database, bool compatibility)
    {
        foreach (int code in database.CanonicalDecompositions.Keys)
        {
            yield return code;
        }

        if (!compatibility)
        {
            yield break;
        }

        foreach (int code in database.CompatibilityDecompositions.Keys)
        {
            yield return code;
        }
    }

    private static void Walk(UnicodeCharacterDatabase database, int code, bool compatibility, List<int> into)
    {
        if (!database.CanonicalDecompositions.TryGetValue(code, out int[]? step)
            && (!compatibility || !database.CompatibilityDecompositions.TryGetValue(code, out step)))
        {
            into.Add(code);

            return;
        }

        foreach (int part in step)
        {
            Walk(database, part, compatibility, into);
        }
    }

    private static Dictionary<long, int> Compose(UnicodeCharacterDatabase database)
    {
        var composition = new Dictionary<long, int>();

        foreach ((int code, int[] decomposition) in database.CanonicalDecompositions)
        {
            // A primary composite is a two-part canonical decomposition whose code point
            // the database does not exclude from composition. Singletons and the
            // exclusions are exactly what Full_Composition_Exclusion holds.
            if (decomposition.Length == 2 && !database.FullCompositionExclusions[code])
            {
                composition[Pair(decomposition[0], decomposition[1])] = code;
            }
        }

        return composition;
    }

    private List<int> Decompose(IReadOnlyList<int> text, Dictionary<int, int[]> mappings)
    {
        var decomposed = new List<int>(text.Count + 4);

        for (int index = 0; index < text.Count; index++)
        {
            int code = text[index];
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
            else if (mappings.TryGetValue(code, out int[]? mapping))
            {
                decomposed.AddRange(mapping);
            }
            else
            {
                decomposed.Add(code);
            }
        }

        Order(decomposed);

        return decomposed;
    }

    private void Order(List<int> decomposed)
    {
        // The canonical ordering algorithm of UAX #15: a bubble sort, because it must be
        // stable within one combining class and the runs are two or three long.
        for (int index = 1; index < decomposed.Count; index++)
        {
            int current = _database.CombiningClasses[decomposed[index]];

            if (current == 0)
            {
                continue;
            }

            int position = index;

            while (position > 0 && _database.CombiningClasses[decomposed[position - 1]] > current)
            {
                (decomposed[position - 1], decomposed[position]) = (decomposed[position], decomposed[position - 1]);
                position--;
            }
        }
    }

    private int[] Recompose(List<int> decomposed)
    {
        if (decomposed.Count == 0)
        {
            return [];
        }

        var composed = new List<int>(decomposed.Count) { decomposed[0] };
        int starter = 0;
        int lastClass = _database.CombiningClasses[decomposed[0]] == 0 ? -1 : 256;

        for (int index = 1; index < decomposed.Count; index++)
        {
            int code = decomposed[index];
            int combining = _database.CombiningClasses[code];

            if (lastClass < combining && TryCombine(composed[starter], code, out int result))
            {
                composed[starter] = result;

                continue;
            }

            // A code point is blocked from the starter when something between them is
            // a starter or is of a class not lower than its own (UAX #15, D115). The
            // sentinel below a class of zero is what "immediately after the starter"
            // means: nothing stands between them.
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

        return [.. composed];
    }

    private bool TryCombine(int starter, int following, out int composed)
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

        return Composition.TryGetValue(Pair(starter, following), out composed);
    }
}
