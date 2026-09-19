using System.Collections.Generic;

namespace Janus.Core.Unicode;

/// <summary>
/// The Bidi Rule of RFC 5893 section 2, which the UsernameCaseMapped profile applies to
/// a string holding right-to-left code points (RFC 8265 section 3.3.1).
/// </summary>
/// <remarks>
/// The rule keeps a name from displaying differently depending on what stands beside
/// it, which is what makes a right-to-left username recognisable at all.
/// </remarks>
internal static class BidiRule
{
    /// <summary>
    /// Whether the string holds a right-to-left code point, which is what makes the
    /// rule apply to it.
    /// </summary>
    /// <param name="text">The code points.</param>
    /// <returns>Whether the rule applies.</returns>
    internal static bool Applies(List<int> text)
    {
        foreach (int code in text)
        {
            if (ClassOf(code) is BidiClass.R or BidiClass.AL or BidiClass.AN)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether all six conditions of the rule hold.
    /// </summary>
    /// <param name="text">The code points.</param>
    /// <returns>Whether the string satisfies the rule.</returns>
    internal static bool IsSatisfied(List<int> text)
    {
        if (text.Count == 0)
        {
            return false;
        }

        // Condition 1: the first code point decides the direction, and a string that
        // starts with anything else satisfies nothing.
        return ClassOf(text[0]) switch
        {
            BidiClass.R or BidiClass.AL => RightToLeft(text),
            BidiClass.L => LeftToRight(text),
            _ => false,
        };
    }

    private static bool RightToLeft(List<int> text)
    {
        bool european = false;
        bool arabic = false;

        foreach (int code in text)
        {
            BidiClass carried = ClassOf(code);

            // Condition 2.
            if (carried is not (BidiClass.R or BidiClass.AL or BidiClass.AN or BidiClass.EN
                or BidiClass.ES or BidiClass.CS or BidiClass.ET or BidiClass.ON
                or BidiClass.BN or BidiClass.NSM))
            {
                return false;
            }

            european |= carried == BidiClass.EN;
            arabic |= carried == BidiClass.AN;
        }

        // Conditions 3 and 4.
        return Ending(text) is BidiClass.R or BidiClass.AL or BidiClass.EN or BidiClass.AN
            && !(european && arabic);
    }

    private static bool LeftToRight(List<int> text)
    {
        foreach (int code in text)
        {
            // Condition 5.
            if (ClassOf(code) is not (BidiClass.L or BidiClass.EN or BidiClass.ES
                or BidiClass.CS or BidiClass.ET or BidiClass.ON or BidiClass.BN
                or BidiClass.NSM))
            {
                return false;
            }
        }

        // Condition 6.
        return Ending(text) is BidiClass.L or BidiClass.EN;
    }

    private static BidiClass Ending(List<int> text)
    {
        // Conditions 3 and 6 look past the non-spacing marks a string may end with.
        for (int index = text.Count - 1; index >= 0; index--)
        {
            BidiClass carried = ClassOf(text[index]);

            if (carried != BidiClass.NSM)
            {
                return carried;
            }
        }

        return BidiClass.NSM;
    }

    private static BidiClass ClassOf(int code) =>
        (BidiClass)Ranges.Value(PrecisTables.BidiClassStarts, PrecisTables.BidiClassValues, code);
}
