using System;
using System.Collections.Generic;
using System.Text;

namespace Janus.Core.Unicode;

/// <summary>
/// Reads a string as the code points the Unicode operations work on, and writes them
/// back.
/// </summary>
/// <remarks>
/// A string reaching the library is whatever a caller sent, so it can hold a surrogate
/// without its pair. Such a code unit is carried through as its own code point, where
/// the tables give it the general category of a surrogate and every profile refuses it,
/// rather than throwing before the refusal is reached.
/// </remarks>
internal static class Text
{
    /// <summary>
    /// The code points of a string.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <returns>The code points.</returns>
    internal static List<int> Read(string value)
    {
        var codes = new List<int>(value.Length);

        for (int index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index])
                && index + 1 < value.Length
                && char.IsLowSurrogate(value[index + 1]))
            {
                codes.Add(char.ConvertToUtf32(value[index], value[index + 1]));
                index++;

                continue;
            }

            codes.Add(value[index]);
        }

        return codes;
    }

    /// <summary>
    /// The string a sequence of code points stands for.
    /// </summary>
    /// <param name="codes">The code points.</param>
    /// <returns>The string.</returns>
    internal static string Write(IReadOnlyList<int> codes)
    {
        var text = new StringBuilder(codes.Count);

        for (int index = 0; index < codes.Count; index++)
        {
            Append(text, codes[index]);
        }

        return text.ToString();
    }

    private static void Append(StringBuilder text, int code)
    {
        if (code <= char.MaxValue)
        {
            text.Append((char)code);

            return;
        }

        int offset = code - 0x10000;

        text.Append((char)(0xD800 + (offset >> 10)));
        text.Append((char)(0xDC00 + (offset & 0x3FF)));
    }
}
