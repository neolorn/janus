using System;
using Janus.Core;

namespace Janus.Authentication.Identifiers;

/// <summary>
/// An identifier as a person enters it, read as sign-in reads it.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-004 and IDN-READ-003. The kind is read off the shape of what was
/// entered, and the value is taken to the canonical form an account holds it in.
/// </remarks>
internal static class EnteredIdentifiers
{
    /// <summary>
    /// Reads one.
    /// </summary>
    /// <param name="entered">What was entered, trimmed.</param>
    /// <param name="usernames">Whether usernames are enabled.</param>
    /// <returns>Its kind and canonical form, or nothing where it reads as neither.</returns>
    /// <exception cref="ArgumentNullException">The text is absent.</exception>
    public static (IdentifierKind Kind, string Canonical)? Canonical(string entered, bool usernames)
    {
        ArgumentNullException.ThrowIfNull(entered);

        if (IdentifierKinds.Detect(entered, usernames) is not { } kind)
        {
            return null;
        }

        string? canonical = kind switch
        {
            IdentifierKind.Email => EmailAddress.TryParse(entered, out EmailAddress address)
                ? address.Value
                : null,
            IdentifierKind.Phone => PhoneNumber.TryParse(entered, out PhoneNumber number)
                ? number.Value
                : null,
            _ => Username.TryParse(entered, out Username username) ? username.Value : null,
        };

        return canonical is null ? null : (kind, canonical);
    }
}
