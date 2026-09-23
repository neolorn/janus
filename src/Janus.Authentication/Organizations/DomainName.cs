using System;
using System.Globalization;
using Janus.Core;

namespace Janus.Authentication.Organizations;

/// <summary>
/// A domain in the one form it is listed, looked up and compared under: the ASCII form
/// of its canonical form, which the case fold has already put in lower case.
/// </summary>
/// <remarks>
/// Implements REG-DOM-001 and IDN-ORG-006. An address's domain is read the same way, so
/// a domain entered in its Unicode form and an address written in its ASCII form, or the
/// other way about, are one domain; what does not read is admitted by no lock.
/// </remarks>
internal static class DomainName
{
    // The name the record is published at, `_identity-verify.` and the domain, is itself
    // a name DNS carries, so the domain leaves room for the prefix within 253 octets.
    private const int MaximumLength = 253 - 17;

    private static readonly IdnMapping Mapping = new() { UseStd3AsciiRules = true };

    /// <summary>
    /// Reads a domain as it was entered.
    /// </summary>
    /// <param name="entered">The domain as entered.</param>
    /// <param name="domain">Its ASCII form, or empty.</param>
    /// <returns>Whether it is a domain of at least two labels a lock can list.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static bool TryRead(string entered, out string domain)
    {
        ArgumentNullException.ThrowIfNull(entered);

        domain = string.Empty;
        string canonical = CanonicalForm.Of(entered.Trim());

        if (canonical.Length is 0 || canonical.EndsWith('.') || !canonical.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        if (Ascii(canonical).Match<string?>(value => value, _ => null) is not { } ascii
            || ascii.Length > MaximumLength)
        {
            return false;
        }

        domain = ascii;

        return true;
    }

    /// <summary>
    /// Reads the domain of an address.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <param name="domain">Its domain's ASCII form, or empty.</param>
    /// <returns>Whether the domain reads.</returns>
    public static bool TryReadOf(EmailAddress address, out string domain)
    {
        string value = address.Value;
        int at = value.LastIndexOf('@');

        domain = string.Empty;

        return at > 0 && TryRead(value[(at + 1)..], out domain);
    }

    private static Result<string> Ascii(string canonical)
    {
        try
        {
            return Result.Success(Mapping.GetAscii(canonical));
        }
        catch (ArgumentException)
        {
            // IdnMapping answers a name it cannot map with this exception and nothing
            // else, and a name it cannot map is not a domain.
            return Result.Failure<string>(Error.From(ErrorCodes.RequestMalformed));
        }
    }
}
