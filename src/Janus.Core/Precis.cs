using System;
using Janus.Core.Unicode;

namespace Janus.Core;

/// <summary>
/// The two PRECIS profiles an identifier must satisfy to be accepted: a username takes
/// UsernameCaseMapped (RFC 8265), a display name takes Nickname (RFC 8266).
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-004 and REG-PROF-001. A profile decides what a value may look
/// like; it is not the comparison key. The comparison key of every identifier is
/// <see cref="CanonicalForm.Of(string)"/>.
/// </remarks>
public static class Precis
{
    /// <summary>
    /// Enforces the UsernameCaseMapped profile: fullwidth and halfwidth code points
    /// take their decompositions, capitals become small letters, the result is in
    /// Normalization Form C, and a value holding right-to-left code points satisfies
    /// the Bidi Rule.
    /// </summary>
    /// <param name="value">The username as it was entered.</param>
    /// <param name="enforced">
    /// The username in the profile's form, or an empty string where it does not
    /// conform.
    /// </param>
    /// <returns>Whether the username conforms to the profile.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static bool TryEnforceUsername(string value, out string enforced)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Profiles.TryUsername(value, out enforced);
    }

    /// <summary>
    /// Enforces the Nickname profile: a space of any kind becomes an ASCII space, the
    /// spaces at either end go, a run inside becomes one, and the result is in
    /// Normalization Form KC. Case is left as it was entered, because a display name is
    /// shown back as the person wrote it.
    /// </summary>
    /// <param name="value">The display name as it was entered.</param>
    /// <param name="enforced">
    /// The display name in the profile's form, or an empty string where it does not
    /// conform.
    /// </param>
    /// <returns>Whether the display name conforms to the profile.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static bool TryEnforceNickname(string value, out string enforced)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Profiles.TryNickname(value, out enforced);
    }
}
