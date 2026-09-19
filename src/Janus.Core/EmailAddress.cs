using System;
using System.Text;
using Janus.Core.Unicode;

namespace Janus.Core;

/// <summary>
/// An email address in the canonical form it is stored and compared under.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-004, IDN-ACCT-006, REG-IDENT-001 and CONV-DESIGN-004. The
/// canonical form is <c>NFKC_Casefold</c> over the whole address, so two addresses that
/// differ only in composition, in width or in case are one address. The form the person
/// entered is kept beside it for display and is not this type's business.
/// </remarks>
public readonly record struct EmailAddress
{
    /// <summary>
    /// The longest address RFC 5321 carries, in octets.
    /// </summary>
    public const int MaximumOctets = 254;

    /// <summary>
    /// The longest local part RFC 5321 carries, in octets.
    /// </summary>
    public const int MaximumLocalPartOctets = 64;

    private const char At = '@';

    private readonly string? _value;

    private EmailAddress(string value) => _value = value;

    /// <summary>
    /// The canonical form, which is what is fingerprinted and compared.
    /// </summary>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Reads an address as it was entered and returns its canonical form.
    /// </summary>
    /// <param name="entered">The address as it was entered.</param>
    /// <param name="address">The canonical address, or an unset value.</param>
    /// <returns>Whether the value is an address this library stores.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static bool TryParse(string entered, out EmailAddress address)
    {
        ArgumentNullException.ThrowIfNull(entered);

        address = default;
        string canonical = CanonicalForm.Of(entered);
        int at = canonical.IndexOf(At, StringComparison.Ordinal);

        if (at <= 0
            || at != canonical.LastIndexOf(At)
            || at == canonical.Length - 1
            || Encoding.UTF8.GetByteCount(canonical) > MaximumOctets
            || Encoding.UTF8.GetByteCount(canonical.AsSpan(..at)) > MaximumLocalPartOctets
            || !IsPrintable(canonical))
        {
            return false;
        }

        address = new EmailAddress(canonical);

        return true;
    }

    /// <inheritdoc/>
    public override string ToString() => Value;

    // An address carries no space and nothing unprintable. The canonical form has
    // already removed the code points that are ignorable by default, so what is left of
    // those categories is there on purpose and is not part of an address.
    private static bool IsPrintable(string canonical)
    {
        foreach (Rune rune in canonical.EnumerateRunes())
        {
            if (StringClass.CategoryOf(rune.Value) is GeneralCategory.Cc
                or GeneralCategory.Cf
                or GeneralCategory.Cn
                or GeneralCategory.Co
                or GeneralCategory.Cs
                or GeneralCategory.Zl
                or GeneralCategory.Zp
                or GeneralCategory.Zs)
            {
                return false;
            }
        }

        return true;
    }
}
