using System;
using System.Text;

namespace Janus.Core;

/// <summary>
/// The name shown where a human needs to know who an account is.
/// </summary>
/// <remarks>
/// Implements REG-PROF-001, IDN-ATTR-007 and IDN-ACCT-005. The profile is Nickname
/// (RFC 8266), which settles the spaces and the normalization form and leaves the case
/// as it was entered, because a display name is shown back as the person wrote it. The
/// bound is in bytes, not characters: a name in a script that costs three bytes a
/// character is shorter in characters than one in Latin, which is the point of the
/// byte bound in the profile.
/// </remarks>
public readonly record struct DisplayName
{
    /// <summary>
    /// The fewest bytes a display name carries.
    /// </summary>
    public const int MinimumBytes = 1;

    /// <summary>
    /// The most bytes a display name carries.
    /// </summary>
    public const int MaximumBytes = 64;

    private readonly string? _value;

    private DisplayName(string value) => _value = value;

    /// <summary>
    /// The display name in the profile's form, which is what is shown.
    /// </summary>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Reads a display name as it was entered and returns it in the profile's form.
    /// </summary>
    /// <param name="entered">The display name as it was entered.</param>
    /// <param name="name">The display name, or an unset value.</param>
    /// <returns>Whether the value is a display name this library accepts.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static bool TryParse(string entered, out DisplayName name)
    {
        ArgumentNullException.ThrowIfNull(entered);

        name = default;

        if (!Precis.TryEnforceNickname(entered, out string enforced)
            || !ScriptMixing.IsSingleScriptPerWord(enforced))
        {
            return false;
        }

        int bytes = Encoding.UTF8.GetByteCount(enforced);

        if (bytes is < MinimumBytes or > MaximumBytes)
        {
            return false;
        }

        name = new DisplayName(enforced);

        return true;
    }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
