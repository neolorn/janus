using System;
using System.Text.RegularExpressions;

namespace Janus.Core;

/// <summary>
/// The name of a role: a bundle of permissions held as data, so that adding one is a
/// row rather than a deployment.
/// </summary>
/// <remarks>Implements AUTHZ-GRANT-004, chapter 10 section 3, CONV-DESIGN-004.</remarks>
public readonly partial record struct RoleName
{
    private readonly string? _value;

    private RoleName(string value) => _value = value;

    /// <summary>
    /// Reads a role name, refusing anything outside the format.
    /// </summary>
    /// <param name="value">The name.</param>
    /// <returns>The role name.</returns>
    /// <exception cref="ArgumentException">The value is not a well-formed name.</exception>
    public static RoleName Parse(string value)
    {
        if (!TryParse(value, out RoleName name))
        {
            throw new ArgumentException(
                "A role name is a lowercase name of letters, digits and hyphens.",
                nameof(value));
        }

        return name;
    }

    /// <summary>
    /// Reads a role name, answering whether it is one.
    /// </summary>
    /// <param name="value">The name.</param>
    /// <param name="name">The role name, where the value is one.</param>
    /// <returns>Whether the value is a well-formed name.</returns>
    public static bool TryParse(string? value, out RoleName name)
    {
        if (value is null || !Shape().IsMatch(value))
        {
            name = default;
            return false;
        }

        name = new RoleName(value);
        return true;
    }

    /// <summary>
    /// The name as it crosses the boundary.
    /// </summary>
    /// <returns>The name, or an empty string for an unset name.</returns>
    public override string ToString() => _value ?? string.Empty;

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
