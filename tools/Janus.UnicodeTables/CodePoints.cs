using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Janus.UnicodeTables;

/// <summary>
/// Reads the shapes the Unicode Character Database writes code points in.
/// </summary>
internal static class CodePoints
{
    /// <summary>
    /// The last code point.
    /// </summary>
    internal const int Last = 0x10FFFF;

    /// <summary>
    /// The number of code points.
    /// </summary>
    internal const int Count = Last + 1;

    /// <summary>
    /// Reads one hexadecimal code point.
    /// </summary>
    /// <param name="value">The hexadecimal digits.</param>
    /// <returns>The code point.</returns>
    internal static int Parse(string value) =>
        int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads a field holding either one code point or an inclusive range.
    /// </summary>
    /// <param name="value">The field.</param>
    /// <returns>The first and last code point of the range.</returns>
    internal static (int First, int Last) Range(string value)
    {
        int separator = value.IndexOf("..", StringComparison.Ordinal);

        return separator < 0
            ? (Parse(value), Parse(value))
            : (Parse(value[..separator]), Parse(value[(separator + 2)..]));
    }

    /// <summary>
    /// Reads a field holding a space-separated sequence of code points.
    /// </summary>
    /// <param name="value">The field.</param>
    /// <returns>The sequence.</returns>
    internal static int[] Sequence(string value)
    {
        string[] parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int[] sequence = new int[parts.Length];

        for (int index = 0; index < parts.Length; index++)
        {
            sequence[index] = Parse(parts[index]);
        }

        return sequence;
    }

    /// <summary>
    /// Writes a code point sequence as the string it stands for.
    /// </summary>
    /// <param name="sequence">The sequence.</param>
    /// <returns>The string.</returns>
    internal static string Text(IReadOnlyList<int> sequence)
    {
        var text = new StringBuilder(sequence.Count);

        for (int index = 0; index < sequence.Count; index++)
        {
            text.Append(char.ConvertFromUtf32(sequence[index]));
        }

        return text.ToString();
    }
}
