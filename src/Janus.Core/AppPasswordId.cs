using System;
using System.Diagnostics.CodeAnalysis;

namespace Janus.Core;

/// <summary>
/// What the mail server calls one app password: the server's own identifier, kept in
/// the server's form, 1 to 255 octets of the URL and filename safe base64 alphabet
/// with the pad <c>=</c> excluded.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-004, INT-MAIL-010 and chapter 09 section 6. The form is the
/// JMAP <c>Id</c> of RFC 8620 section 1.2: the mail server draws the identifier, so it
/// is a value over that form and never over a UUID. A default instance was never read,
/// so it has no text to give and no call can carry it.
/// </remarks>
public readonly record struct AppPasswordId : IParsable<AppPasswordId>
{
    // RFC 8620 section 1.2: the longest an identifier may be, in octets.
    private const int MaximumLength = 255;

    private readonly string? _value;

    private AppPasswordId(string value) => _value = value;

    /// <summary>
    /// Reads an app-password identifier, refusing anything outside its form.
    /// </summary>
    /// <param name="value">The identifier.</param>
    /// <returns>The app-password identifier.</returns>
    /// <exception cref="ArgumentException">The value is not an app-password identifier.</exception>
    public static AppPasswordId Parse(string value)
    {
        if (!TryParse(value, out AppPasswordId id))
        {
            throw new ArgumentException(
                "An app-password identifier is 1 to 255 letters, digits, hyphens and underscores.",
                nameof(value));
        }

        return id;
    }

    /// <summary>
    /// Reads an app-password identifier, answering whether it is one.
    /// </summary>
    /// <param name="value">The identifier.</param>
    /// <param name="id">The app-password identifier, where the value is one.</param>
    /// <returns>Whether the value keeps the form.</returns>
    public static bool TryParse(string? value, out AppPasswordId id)
    {
        if (value is null || value.Length is 0 or > MaximumLength)
        {
            id = default;
            return false;
        }

        // Every character of the alphabet is one octet, so the length in characters is
        // the length in octets once each is known to be of it.
        foreach (char character in value)
        {
            if (character is not ((>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_'))
            {
                id = default;
                return false;
            }
        }

        id = new AppPasswordId(value);
        return true;
    }

    /// <summary>
    /// Reads an app-password identifier as a route or a query carries it.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Unused: an app-password identifier is written one way.</param>
    /// <returns>The value the text names.</returns>
    /// <exception cref="FormatException">The text is not an app-password identifier.</exception>
    static AppPasswordId IParsable<AppPasswordId>.Parse(string s, IFormatProvider? provider) =>
        TryParse(s, out AppPasswordId result)
            ? result
            : throw new FormatException("The text is not an app-password identifier.");

    /// <summary>
    /// Reads an app-password identifier as a route or a query carries it.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Unused: an app-password identifier is written one way.</param>
    /// <param name="result">The value, where the text names one.</param>
    /// <returns>Whether the text is an app-password identifier.</returns>
    static bool IParsable<AppPasswordId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out AppPasswordId result) =>
        TryParse(s, out result);

    /// <summary>
    /// The identifier as the mail server wrote it.
    /// </summary>
    /// <returns>The identifier.</returns>
    /// <exception cref="InvalidOperationException">The identifier was never set.</exception>
    [SuppressMessage(
        "Design",
        "CA1065:Do not raise exceptions in unexpected locations",
        Justification = "CONV-DESIGN-004 AC3: an unset value has no text, and the empty text it would give is what a call would carry.")]
    public override string ToString() => _value ?? throw new InvalidOperationException("The app-password identifier was never set.");
}
