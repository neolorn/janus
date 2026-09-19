using System;

namespace Janus.Core.Unicode;

/// <summary>
/// Reads a mapping out of the tables the generator writes.
/// </summary>
/// <remarks>
/// A mapping table is the sorted code points the mapping is given for, the offset each
/// one's replacement begins at, and the replacements laid end to end. Code points the
/// table does not name map to themselves.
/// </remarks>
internal static class Mappings
{
    /// <summary>
    /// The replacement a code point maps to, where the table gives one.
    /// </summary>
    /// <param name="keys">The code points the mapping is given for.</param>
    /// <param name="offsets">Where each replacement begins and ends.</param>
    /// <param name="data">The replacements.</param>
    /// <param name="code">The code point.</param>
    /// <param name="mapping">The replacement, which may be empty.</param>
    /// <returns>Whether the table gives a mapping for the code point.</returns>
    internal static bool TryFind(
        int[] keys,
        int[] offsets,
        int[] data,
        int code,
        out ReadOnlySpan<int> mapping)
    {
        int found = Array.BinarySearch(keys, code);

        if (found < 0)
        {
            mapping = default;

            return false;
        }

        mapping = data.AsSpan(offsets[found], offsets[found + 1] - offsets[found]);

        return true;
    }
}
