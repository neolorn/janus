using System.Collections.Generic;

namespace Janus.Core.Unicode;

/// <summary>
/// The two PRECIS string classes of RFC 8264 section 4: which code points a string may
/// hold at all, before any profile of the class narrows it further.
/// </summary>
/// <remarks>
/// The IdentifierClass admits the code points whose derived property is PVALID; the
/// FreeformClass admits those and the ones the derivation leaves as FREE_PVAL. Both
/// admit a code point requiring a contextual rule only where that rule returns true,
/// and both refuse a code point that is disallowed or unassigned, so a string holding
/// anything the pinned version does not know is refused rather than guessed at.
/// </remarks>
internal static class StringClass
{
    /// <summary>
    /// Whether every code point is one the IdentifierClass admits.
    /// </summary>
    /// <param name="text">The code points.</param>
    /// <returns>Whether the string belongs to the class.</returns>
    internal static bool IsIdentifier(List<int> text) => Admits(text, freeform: false);

    /// <summary>
    /// Whether every code point is one the FreeformClass admits.
    /// </summary>
    /// <param name="text">The code points.</param>
    /// <returns>Whether the string belongs to the class.</returns>
    internal static bool IsFreeform(List<int> text) => Admits(text, freeform: true);

    /// <summary>
    /// The General_Category of a code point.
    /// </summary>
    /// <param name="code">The code point.</param>
    /// <returns>The category.</returns>
    internal static GeneralCategory CategoryOf(int code) =>
        (GeneralCategory)Ranges.Value(CategoryTables.CategoryStarts, CategoryTables.CategoryValues, code);

    private static bool Admits(List<int> text, bool freeform)
    {
        for (int index = 0; index < text.Count; index++)
        {
            PrecisProperty carried = PropertyOf(text[index]);

            bool admitted = carried switch
            {
                PrecisProperty.Pvalid => true,
                PrecisProperty.FreeformPvalid => freeform,
                PrecisProperty.ContextJ or PrecisProperty.ContextO => ContextualRules.Allows(text, index),
                _ => false,
            };

            if (!admitted)
            {
                return false;
            }
        }

        return true;
    }

    private static PrecisProperty PropertyOf(int code) =>
        (PrecisProperty)Ranges.Value(PrecisTables.PropertyStarts, PrecisTables.PropertyValues, code);
}
