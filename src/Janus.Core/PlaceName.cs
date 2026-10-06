namespace Janus.Core;

/// <summary>
/// The rule a name a deployment or a host chooses is held to where a message carries
/// it: 1 to 64 lower-case letters and digits separated by single <c>.</c>, <c>-</c> or
/// <c>_</c>.
/// </summary>
/// <remarks>
/// Implements INT-SMS-003 and LIB-HOST-001. A restriction's name, a governing
/// document's and a subject-event subscriber's fill a message place, and a template
/// is measured at startup with each place at its widest; bounding the value where it
/// is written keeps every rendered text within the width it was measured at. The
/// letters are those of the default text-message alphabet, <c>a</c> to <c>z</c>, so a
/// name never moves a text into the wider alphabet and its smaller budget.
/// </remarks>
internal static class PlaceName
{
    /// <summary>
    /// The longest a name may be, which is the width its place is measured at.
    /// </summary>
    public const int MaximumLength = 64;

    /// <summary>
    /// Whether one name keeps the rule.
    /// </summary>
    /// <param name="name">The name, or nothing.</param>
    /// <returns>Whether it does; an absent name does not.</returns>
    public static bool Holds(string? name)
    {
        if (name is null || name.Length is 0 or > MaximumLength)
        {
            return false;
        }

        bool separated = true;

        foreach (char character in name)
        {
            if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                separated = false;

                continue;
            }

            // A separator stands between two letters or digits, never at an end and
            // never beside another.
            if (character is not ('.' or '-' or '_') || separated)
            {
                return false;
            }

            separated = true;
        }

        return !separated;
    }
}
