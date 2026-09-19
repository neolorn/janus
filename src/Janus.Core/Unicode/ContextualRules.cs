using System.Collections.Generic;

namespace Janus.Core.Unicode;

/// <summary>
/// The contextual rules of RFC 5892 appendix A, which decide whether a code point whose
/// derived property is CONTEXTJ or CONTEXTO may stand where it stands.
/// </summary>
/// <remarks>
/// RFC 8264 section 8 is explicit that a code point requiring a contextual rule is
/// invalid unless a rule exists and returns true, so a code point this file has no rule
/// for is refused.
/// </remarks>
internal static class ContextualRules
{
    private const int ZeroWidthNonJoiner = 0x200C;
    private const int ZeroWidthJoiner = 0x200D;
    private const int MiddleDot = 0x00B7;
    private const int SmallLetterL = 0x006C;
    private const int GreekLowerNumeralSign = 0x0375;
    private const int HebrewGeresh = 0x05F3;
    private const int HebrewGershayim = 0x05F4;
    private const int KatakanaMiddleDot = 0x30FB;
    private const int Virama = 9;

    /// <summary>
    /// Whether the rule for the code point at a position returns true.
    /// </summary>
    /// <param name="text">The code points.</param>
    /// <param name="index">Where the code point stands.</param>
    /// <returns>Whether the rule allows it there.</returns>
    internal static bool Allows(List<int> text, int index) => text[index] switch
    {
        ZeroWidthNonJoiner => AfterVirama(text, index) || BetweenJoiners(text, index),
        ZeroWidthJoiner => AfterVirama(text, index),
        MiddleDot => Before(text, index) == SmallLetterL && After(text, index) == SmallLetterL,
        GreekLowerNumeralSign => ScriptOf(After(text, index)) == ScriptCode.Grek,
        HebrewGeresh or HebrewGershayim => ScriptOf(Before(text, index)) == ScriptCode.Hebr,
        KatakanaMiddleDot => HasJapaneseOrHan(text),
        >= 0x0660 and <= 0x0669 => !HasRange(text, 0x06F0, 0x06F9),
        >= 0x06F0 and <= 0x06F9 => !HasRange(text, 0x0660, 0x0669),
        _ => false,
    };

    private static bool AfterVirama(List<int> text, int index) =>
        index > 0 && Normalizer.CombiningClass(text[index - 1]) == Virama;

    private static bool BetweenJoiners(List<int> text, int index)
    {
        // RFC 5892 appendix A.1: (Joining_Type:{L,D})(Joining_Type:T)* ZWNJ
        // (Joining_Type:T)*(Joining_Type:{R,D}).
        JoiningType before = FirstJoining(text, index - 1, -1);
        JoiningType after = FirstJoining(text, index + 1, 1);

        return before is JoiningType.L or JoiningType.D && after is JoiningType.R or JoiningType.D;
    }

    private static JoiningType FirstJoining(List<int> text, int from, int step)
    {
        for (int index = from; index >= 0 && index < text.Count; index += step)
        {
            JoiningType joining = JoiningOf(text[index]);

            if (joining != JoiningType.T)
            {
                return joining;
            }
        }

        return JoiningType.U;
    }

    private static bool HasJapaneseOrHan(List<int> text)
    {
        foreach (int code in text)
        {
            if (ScriptOf(code) is ScriptCode.Hira or ScriptCode.Kana or ScriptCode.Hani)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRange(List<int> text, int first, int last)
    {
        foreach (int code in text)
        {
            if (code >= first && code <= last)
            {
                return true;
            }
        }

        return false;
    }

    private static int Before(List<int> text, int index) => index > 0 ? text[index - 1] : -1;

    private static int After(List<int> text, int index) =>
        index + 1 < text.Count ? text[index + 1] : -1;

    private static ScriptCode ScriptOf(int code) => code < 0
        ? ScriptCode.Zzzz
        : (ScriptCode)Ranges.Value(ScriptTables.ScriptStarts, ScriptTables.ScriptValues, code);

    private static JoiningType JoiningOf(int code) =>
        (JoiningType)Ranges.Value(PrecisTables.JoiningTypeStarts, PrecisTables.JoiningTypeValues, code);
}
