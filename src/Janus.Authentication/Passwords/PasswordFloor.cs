using System;
using System.Text;
using Janus.Core;

namespace Janus.Authentication.Passwords;

/// <summary>
/// The length rule. The shorter floor is reached by holding a second factor, never by
/// choosing it, so the rule is a property of the password rather than a fact about
/// enrolment and holds at every point a password is set.
/// </summary>
/// <remarks>Implements AUTH-PASS-001, AUTH-PASS-001a, AUTH-PASS-002.</remarks>
internal static class PasswordFloor
{
    /// <summary>
    /// How long a password is. Length is counted in code points, so a password is
    /// never shortened by how it happens to be encoded.
    /// </summary>
    /// <param name="password">The password, in UTF-8.</param>
    /// <returns>Its length.</returns>
    /// <exception cref="ArgumentNullException">The password is absent.</exception>
    public static int Characters([NeverLogged] byte[] password)
    {
        ArgumentNullException.ThrowIfNull(password);

        int characters = 0;

        foreach (Rune unused in Encoding.UTF8.GetString(password).EnumerateRunes())
        {
            characters++;
        }

        return characters;
    }

    /// <summary>
    /// Whether a password of this length may stand on an account that reaches this
    /// assurance.
    /// </summary>
    /// <param name="characters">The password's length.</param>
    /// <param name="singleFactor">The floor where the password could sign in alone.</param>
    /// <param name="withMfa">The floor where it never could.</param>
    /// <param name="maximum">The longest password the deployment accepts.</param>
    /// <param name="reachable">The account's reachable assurance.</param>
    /// <returns>Success, or the failure naming what the length misses.</returns>
    public static Result Admits(
        int characters,
        int singleFactor,
        int withMfa,
        int maximum,
        AssuranceLevel reachable)
    {
        if (characters > maximum)
        {
            return Result.Failure(Error.From(ErrorCodes.PasswordTooLong));
        }

        // Below the single-factor floor the password may exist only while it can never
        // complete a sign-in by itself, which is what reaching AAL2 says.
        int floor = reachable >= AssuranceLevel.Aal2 ? withMfa : singleFactor;

        return characters < floor
            ? Result.Failure(Error.From(ErrorCodes.PasswordTooShort))
            : Result.Success();
    }

    /// <summary>
    /// Whether the password stands on its own, which is recorded beside the hash
    /// because it cannot be recomputed from the hash later.
    /// </summary>
    /// <param name="characters">The password's length.</param>
    /// <param name="singleFactor">The floor where the password could sign in alone.</param>
    /// <returns>Whether it meets that floor.</returns>
    public static bool MeetsSingleFactorFloor(int characters, int singleFactor) =>
        characters >= singleFactor;
}
