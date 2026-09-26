using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Janus.Core.Configuration;

/// <summary>
/// The name of a configuration key: hierarchical, lowercase, dot-separated, and part
/// of the stable public contract.
/// </summary>
/// <remarks>
/// Implements chapter 10 section 4, LIB-API-001. The catalogue is
/// <see cref="Settings"/>. A default instance was never read, so it has no text to give
/// and no row can carry it (CONV-DESIGN-004).
/// </remarks>
public readonly partial record struct ConfigurationKey
{
    private readonly string? _value;

    private ConfigurationKey(string value) => _value = value;

    /// <summary>
    /// Reads a key, refusing anything outside the format of chapter 10 section 4.
    /// </summary>
    /// <param name="value">The dot-separated key.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentException">The value is not a well-formed key.</exception>
    public static ConfigurationKey Parse(string value)
    {
        if (value is null || !Shape().IsMatch(value))
        {
            throw new ArgumentException(
                "A configuration key is lowercase, dot-separated segments of letters, digits and hyphens.",
                nameof(value));
        }

        return new ConfigurationKey(value);
    }

    /// <summary>
    /// The key as the management application and the audit record name it.
    /// </summary>
    /// <returns>The dot-separated key.</returns>
    /// <exception cref="InvalidOperationException">The key was never set.</exception>
    [SuppressMessage(
        "Design",
        "CA1065:Do not raise exceptions in unexpected locations",
        Justification = "CONV-DESIGN-004 AC3: an unset value has no text, and the empty text it would give is what a store writes.")]
    public override string ToString() => _value ?? throw new InvalidOperationException("The key was never set.");

    // A family key carries the organization identifier or the host's category name as
    // its last segment (D-151), and those are not confined to letters: an identifier is
    // a version 7 value and begins with a digit as often as with a letter.
    [GeneratedRegex("^[a-z][a-z0-9]*(\\.[a-z0-9][a-z0-9-]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
