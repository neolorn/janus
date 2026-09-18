using System;
using System.Text.RegularExpressions;

namespace Janus.Core.Configuration;

/// <summary>
/// The name of a configuration key: hierarchical, lowercase, dot-separated, and part
/// of the stable public contract.
/// </summary>
/// <remarks>
/// Implements chapter 10 section 4, LIB-API-001. The catalogue is
/// <see cref="Settings"/>.
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
    /// <returns>The dot-separated key, or an empty string for an unset key.</returns>
    public override string ToString() => _value ?? string.Empty;

    // A family key carries the organization identifier or the host's category name as
    // its last segment (D-151), and those are not confined to letters.
    [GeneratedRegex("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9-]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
