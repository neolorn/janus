using System;
using System.Collections.Generic;
using Janus.Core.Unicode;

namespace Janus.Core;

/// <summary>
/// The one form the staff search holds a name in and reads a query in, so that a name
/// and a query written with another spelling of the same letters meet.
/// </summary>
/// <remarks>
/// Implements IDN-SRCH-001 (D-256). The form is computed from the tables the package
/// carries at the version <see cref="CanonicalForm.UnicodeVersion"/> names, never from
/// the machine's own, so a result never changes with the machine: Unicode NFKC; the
/// Arabic alef forms to the bare alef, teh marbuta to heh and alef maksura to yeh; the
/// harakat and the tatweel removed; Arabic-Indic and Eastern Arabic-Indic digits to the
/// ASCII digits; Latin letters lowercased with their combining marks removed; and every
/// run of whitespace and punctuation one space, none at either end.
/// </remarks>
public static class SearchForm
{
    private const int Space = ' ';

    private const int Alef = 0x0627;

    private const int Heh = 0x0647;

    private const int Yeh = 0x064A;

    /// <summary>
    /// The search form of a text.
    /// </summary>
    /// <param name="text">The text, a name or a query.</param>
    /// <returns>The normalised text, empty where nothing of it is searched.</returns>
    /// <exception cref="ArgumentNullException">The text is absent.</exception>
    public static string Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<int> arabic = Arabic(Normalizer.Nfkc(Text.Read(text)));

        return Text.Write(Separated(Normalizer.Nfc(Latin(Normalizer.Nfd(arabic)))));
    }

    private static List<int> Arabic(List<int> text)
    {
        var mapped = new List<int>(text.Count);

        foreach (int code in text)
        {
            switch (code)
            {
                case 0x0622 or 0x0623 or 0x0625 or 0x0671:
                    mapped.Add(Alef);
                    break;
                case 0x0629:
                    mapped.Add(Heh);
                    break;
                case 0x0649:
                    mapped.Add(Yeh);
                    break;
                case (>= 0x064B and <= 0x065F) or 0x0670 or 0x0640:
                    break;
                case >= 0x0660 and <= 0x0669:
                    mapped.Add('0' + (code - 0x0660));
                    break;
                case >= 0x06F0 and <= 0x06F9:
                    mapped.Add('0' + (code - 0x06F0));
                    break;
                default:
                    mapped.Add(code);
                    break;
            }
        }

        return mapped;
    }

    // Over the canonical decomposition, a Latin letter is lowercased and every mark that
    // follows it is dropped; every other code point is left as it is, and is composed
    // again afterwards.
    private static List<int> Latin(List<int> decomposed)
    {
        var folded = new List<int>(decomposed.Count);
        bool afterLatin = false;

        foreach (int code in decomposed)
        {
            GeneralCategory category = StringClass.CategoryOf(code);

            if (category is GeneralCategory.Mn or GeneralCategory.Mc or GeneralCategory.Me)
            {
                if (!afterLatin)
                {
                    folded.Add(code);
                }

                continue;
            }

            afterLatin = IsLatinLetter(code, category);

            if (afterLatin)
            {
                folded.AddRange(CaseMapper.ToLower([code]));
            }
            else
            {
                folded.Add(code);
            }
        }

        return folded;
    }

    private static List<int> Separated(List<int> text)
    {
        var separated = new List<int>(text.Count);

        foreach (int code in text)
        {
            if (!IsSeparator(code))
            {
                separated.Add(code);
            }
            else if (separated.Count > 0 && separated[^1] != Space)
            {
                separated.Add(Space);
            }
        }

        if (separated.Count > 0 && separated[^1] == Space)
        {
            separated.RemoveAt(separated.Count - 1);
        }

        return separated;
    }

    private static bool IsLatinLetter(int code, GeneralCategory category) =>
        category is GeneralCategory.Lu or GeneralCategory.Ll or GeneralCategory.Lt or GeneralCategory.Lm or GeneralCategory.Lo
            && Ranges.Value(ScriptTables.ScriptStarts, ScriptTables.ScriptValues, code) == (short)ScriptCode.Latn;

    private static bool IsSeparator(int code) =>
        code is (>= 0x09 and <= 0x0D) or 0x85
            || StringClass.CategoryOf(code) is GeneralCategory.Zs or GeneralCategory.Zl or GeneralCategory.Zp
                or GeneralCategory.Pc or GeneralCategory.Pd or GeneralCategory.Ps or GeneralCategory.Pe
                or GeneralCategory.Pi or GeneralCategory.Pf or GeneralCategory.Po;
}
