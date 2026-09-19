using System;
using System.Text;

namespace Janus.Core;

/// <summary>
/// Which kind of identifier a person entered, worked out from the value alone.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-003. One field takes every identifier, so the kind is read from
/// what was typed rather than chosen from a list. The three kinds do not overlap: an
/// address carries the sign, a number is digits once the separators a person writes are
/// taken out, and a username carries a letter (REG-IDENT-009, D-155).
/// </remarks>
public static class IdentifierKinds
{
    private const char At = '@';
    private const char Plus = '+';

    /// <summary>
    /// Reads the kind of an identifier as it was entered.
    /// </summary>
    /// <param name="entered">The identifier as it was entered.</param>
    /// <param name="usernamesEnabled">
    /// Whether the deployment admits usernames (<c>identifiers.username.enabled</c>).
    /// </param>
    /// <returns>
    /// The kind, or nothing where the value is an identifier of no kind this
    /// deployment holds, which is answered exactly as an unknown identifier is.
    /// </returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static IdentifierKind? Detect(string entered, bool usernamesEnabled)
    {
        ArgumentNullException.ThrowIfNull(entered);

        string trimmed = entered.Trim();

        if (trimmed.Contains(At, StringComparison.Ordinal))
        {
            return IdentifierKind.Email;
        }

        if (IsDigits(WithoutSeparators(trimmed)))
        {
            return IdentifierKind.Phone;
        }

        return usernamesEnabled ? IdentifierKind.Username : null;
    }

    // The separators a person writes a number with, and the two prefixes a number
    // carries. Nothing here changes the value; it only decides what shape it is in.
    private static string WithoutSeparators(string trimmed)
    {
        var kept = new StringBuilder(trimmed.Length);

        foreach (Rune rune in trimmed.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune) || rune.Value is '-' or '.' or '(' or ')')
            {
                continue;
            }

            kept.Append(rune);
        }

        string bare = kept.ToString();

        if (bare.StartsWith(Plus))
        {
            return bare[1..];
        }

        return bare.StartsWith("00", StringComparison.Ordinal) ? bare[2..] : bare;
    }

    private static bool IsDigits(string bare)
    {
        if (bare.Length == 0)
        {
            return false;
        }

        foreach (Rune rune in bare.EnumerateRunes())
        {
            if (Unicode.StringClass.CategoryOf(rune.Value) is not Unicode.GeneralCategory.Nd)
            {
                return false;
            }
        }

        return true;
    }
}
