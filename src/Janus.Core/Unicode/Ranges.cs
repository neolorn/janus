using System;

namespace Janus.Core.Unicode;

/// <summary>
/// Reads a property out of the run-length tables the generator writes.
/// </summary>
/// <remarks>
/// A table is the sorted code point each run starts at beside the value that run
/// carries, and the runs cover every code point, so a lookup is one binary search and
/// never misses.
/// </remarks>
internal static class Ranges
{
    /// <summary>
    /// The value a code point carries.
    /// </summary>
    /// <param name="starts">The code point each run starts at.</param>
    /// <param name="values">The value each run carries.</param>
    /// <param name="code">The code point.</param>
    /// <returns>The value.</returns>
    internal static byte Value(int[] starts, byte[] values, int code) => values[Run(starts, code)];

    /// <summary>
    /// The value a code point carries.
    /// </summary>
    /// <param name="starts">The code point each run starts at.</param>
    /// <param name="values">The value each run carries.</param>
    /// <param name="code">The code point.</param>
    /// <returns>The value.</returns>
    internal static short Value(int[] starts, short[] values, int code) => values[Run(starts, code)];

    private static int Run(int[] starts, int code)
    {
        int found = Array.BinarySearch(starts, code);

        return found >= 0 ? found : ~found - 1;
    }
}
