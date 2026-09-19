using System;

namespace Janus.Core;

/// <summary>
/// A telephone number in E.164, which is the only form this library stores.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-004 and REG-IDENT-001. A number takes no Unicode form: digits of
/// any script are mapped to their ASCII digits and what remains is the E.164 value, so
/// a number entered in Arabic-Indic digits is the number entered in ASCII digits.
/// </remarks>
public readonly record struct PhoneNumber
{
    /// <summary>
    /// The most digits an E.164 number carries.
    /// </summary>
    public const int MaximumDigits = 15;

    private const char Plus = '+';

    private readonly string? _value;

    private PhoneNumber(string value) => _value = value;

    /// <summary>
    /// The E.164 form, leading plus included, which is what is fingerprinted and
    /// compared.
    /// </summary>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Reads a number as it was entered and returns its E.164 form.
    /// </summary>
    /// <param name="entered">The number as it was entered.</param>
    /// <param name="number">The E.164 number, or an unset value.</param>
    /// <returns>Whether the value is a number this library stores.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static bool TryParse(string entered, out PhoneNumber number)
    {
        ArgumentNullException.ThrowIfNull(entered);

        number = default;
        string mapped = CanonicalForm.AsciiDigits(entered);
        ReadOnlySpan<char> digits = mapped.StartsWith(Plus) ? mapped.AsSpan(1) : mapped;

        if (digits.Length is 0 or > MaximumDigits)
        {
            return false;
        }

        foreach (char digit in digits)
        {
            if (!char.IsAsciiDigit(digit))
            {
                return false;
            }
        }

        number = new PhoneNumber(Plus + digits.ToString());

        return true;
    }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
