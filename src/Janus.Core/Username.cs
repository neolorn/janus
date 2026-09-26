using System;
using System.Text;
using Janus.Core.Unicode;

namespace Janus.Core;

/// <summary>
/// A username in the form the profile of RFC 8265 leaves it in.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-001, REG-IDENT-009, IDN-ACCT-004, IDN-ACCT-005 and
/// CONV-DESIGN-004. The profile
/// is UsernameCaseMapped and nothing else, narrowed to letters and digits: no space, no
/// punctuation and no symbol. At least one of those characters is a letter, so that no
/// username is also a phone number under the kind detection of REG-IDENT-003 (D-155).
/// Whether a well-formed username is free, reserved or held after an erasure is not
/// this type's business. A default instance was never read, so it has no form to give
/// and no row can carry it.
/// </remarks>
public readonly record struct Username
{
    /// <summary>
    /// The fewest characters a username carries.
    /// </summary>
    public const int MinimumLength = 3;

    /// <summary>
    /// The most characters a username carries.
    /// </summary>
    public const int MaximumLength = 32;

    private readonly string? _value;

    private Username(string value) => _value = value;

    /// <summary>
    /// The username in the profile's form, which is what is shown and what is
    /// fingerprinted.
    /// </summary>
    /// <exception cref="InvalidOperationException">The username was never set.</exception>
    public string Value => _value ?? throw new InvalidOperationException("The username was never set.");

    /// <summary>
    /// Reads a username as it was entered and returns it in the profile's form.
    /// </summary>
    /// <param name="entered">The username as it was entered.</param>
    /// <param name="username">The username, or an unset value.</param>
    /// <returns>Whether the value is a username this library accepts.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static bool TryParse(string entered, out Username username)
    {
        ArgumentNullException.ThrowIfNull(entered);

        username = default;

        if (!Precis.TryEnforceUsername(entered, out string enforced)
            || !IsLettersAndDigits(enforced)
            || !HoldsALetter(enforced)
            || !ScriptMixing.IsSingleScriptPerWord(enforced))
        {
            return false;
        }

        int length = 0;

        foreach (Rune _ in enforced.EnumerateRunes())
        {
            length++;
        }

        if (length is < MinimumLength or > MaximumLength)
        {
            return false;
        }

        username = new Username(enforced);

        return true;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The username was never set.</exception>
    public override string ToString() => Value;

    // REG-IDENT-009: an all-digit choice is a phone number to the kind detection, so it
    // is not a username. A mark is not a letter on its own; it is written with one.
    private static bool HoldsALetter(string enforced)
    {
        foreach (Rune rune in enforced.EnumerateRunes())
        {
            if (StringClass.CategoryOf(rune.Value) is GeneralCategory.Ll
                or GeneralCategory.Lm
                or GeneralCategory.Lo
                or GeneralCategory.Lt
                or GeneralCategory.Lu)
            {
                return true;
            }
        }

        return false;
    }

    // Letters and digits as RFC 8264 section 9.1 counts them: the letter categories,
    // the decimal digits, and the marks a letter is written with in the scripts that
    // need them.
    private static bool IsLettersAndDigits(string enforced)
    {
        foreach (Rune rune in enforced.EnumerateRunes())
        {
            if (StringClass.CategoryOf(rune.Value) is not (GeneralCategory.Ll
                or GeneralCategory.Lm
                or GeneralCategory.Lo
                or GeneralCategory.Lt
                or GeneralCategory.Lu
                or GeneralCategory.Mc
                or GeneralCategory.Mn
                or GeneralCategory.Nd))
            {
                return false;
            }
        }

        return true;
    }
}
