using System;
using System.Text.RegularExpressions;

namespace Janus.Core;

/// <summary>
/// The name a host gives one of its own kinds of thing. The library ships none of
/// them and understands none of them: an enumeration here would make every new kind
/// of thing a library change.
/// </summary>
/// <remarks>Implements AUTHZ-MODEL-002 and CONV-DESIGN-004.</remarks>
public readonly partial record struct ResourceType
{
    private readonly string? _value;

    private ResourceType(string value) => _value = value;

    /// <summary>
    /// Reads a resource type name, refusing anything a permission string could not
    /// name as its resource.
    /// </summary>
    /// <param name="value">The name the host chose.</param>
    /// <returns>The resource type.</returns>
    /// <exception cref="ArgumentException">The value is not a well-formed name.</exception>
    public static ResourceType Parse(string value)
    {
        if (!TryParse(value, out ResourceType type))
        {
            throw new ArgumentException(
                "A resource type is a lowercase name of letters, digits and hyphens.",
                nameof(value));
        }

        return type;
    }

    /// <summary>
    /// Reads a resource type name, answering whether it is one.
    /// </summary>
    /// <param name="value">The name the host chose.</param>
    /// <param name="type">The resource type, where the value is one.</param>
    /// <returns>Whether the value is a well-formed name.</returns>
    public static bool TryParse(string? value, out ResourceType type)
    {
        if (value is null || !Shape().IsMatch(value))
        {
            type = default;
            return false;
        }

        type = new ResourceType(value);
        return true;
    }

    /// <summary>
    /// The name as it crosses the boundary.
    /// </summary>
    /// <returns>The name, or an empty string for an unset type.</returns>
    public override string ToString() => _value ?? string.Empty;

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
