using System;

namespace Janus.Authentication.Search;

/// <summary>
/// Whether two words are at most one edit apart: one character inserted, removed or
/// replaced (Levenshtein distance 1).
/// </summary>
/// <remarks>Implements IDN-SRCH-001 (D-256).</remarks>
internal static class EditDistance
{
    /// <summary>
    /// Whether two words are equal or one edit apart.
    /// </summary>
    /// <param name="first">One word.</param>
    /// <param name="second">The other.</param>
    /// <returns>Whether their Levenshtein distance is at most 1.</returns>
    public static bool WithinOne(ReadOnlySpan<char> first, ReadOnlySpan<char> second)
    {
        if (first.Length < second.Length)
        {
            return WithinOne(second, first);
        }

        if (first.Length - second.Length > 1)
        {
            return false;
        }

        int common = first.CommonPrefixLength(second);

        if (common == second.Length)
        {
            return true;
        }

        // The one edit is at the first difference: past it, a replacement leaves the
        // rests equal, and the removal of the longer word's character leaves its rest
        // equal to the shorter word's.
        return first.Length == second.Length
            ? first[(common + 1)..].SequenceEqual(second[(common + 1)..])
            : first[(common + 1)..].SequenceEqual(second[common..]);
    }
}
