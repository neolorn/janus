using System.Collections.Generic;

namespace Janus.Core.Unicode;

/// <summary>
/// Mixed-script detection as UTS #39 section 5.1 defines it, applied to one word at a
/// time (IDN-ACCT-005).
/// </summary>
/// <remarks>
/// A string is single-script when the augmented script sets of its characters have a
/// script in common. A character of Common or Inherited script, which is every digit,
/// every mark and every piece of punctuation, carries the set of all scripts and so
/// never narrows the intersection; that is why an apostrophe or a hyphen is not a
/// foreign script.
/// </remarks>
internal static class Scripts
{
    /// <summary>
    /// Whether no word of a string mixes scripts.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <returns>Whether every word is single-script.</returns>
    internal static bool IsSingleScriptPerWord(string value)
    {
        List<int> codes = Text.Read(value);
        var word = new List<int>(codes.Count);

        foreach (int code in codes)
        {
            if (IsSeparator(code))
            {
                if (!IsSingleScript(word))
                {
                    return false;
                }

                word.Clear();

                continue;
            }

            word.Add(code);
        }

        return IsSingleScript(word);
    }

    private static bool IsSeparator(int code) =>
        code is >= 0x09 and <= 0x0D
        || StringClass.CategoryOf(code) is GeneralCategory.Zs or GeneralCategory.Zl or GeneralCategory.Zp;

    private static bool IsSingleScript(List<int> word)
    {
        List<short>? resolved = null;

        foreach (int code in word)
        {
            short set = Ranges.Value(ScriptTables.SetStarts, ScriptTables.SetValues, code);

            if (set == 0)
            {
                continue;
            }

            if (resolved is null)
            {
                resolved = Members(set);

                continue;
            }

            Retain(resolved, Members(set));

            if (resolved.Count == 0)
            {
                return false;
            }
        }

        return true;
    }

    private static List<short> Members(short set)
    {
        int from = ScriptTables.SetOffsets[set];
        int to = ScriptTables.SetOffsets[set + 1];
        var members = new List<short>(to - from);

        for (int index = from; index < to; index++)
        {
            members.Add(ScriptTables.SetMembers[index]);
        }

        return members;
    }

    private static void Retain(List<short> resolved, List<short> members)
    {
        for (int index = resolved.Count - 1; index >= 0; index--)
        {
            if (!members.Contains(resolved[index]))
            {
                resolved.RemoveAt(index);
            }
        }
    }
}
