using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Janus.Core;

/// <summary>
/// What a role allows, written as <c>resource:action</c>: lowercase, the resource
/// singular. Coarse by design, because a permission name that carries a scope is how
/// role explosion starts.
/// </summary>
/// <remarks>
/// Implements CONV-NAME-002 and CONV-DESIGN-004. The library's own are
/// <see cref="Permissions"/>; a host declares the rest in the model builder.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "CONV-NAME-002 names the value a permission, and the type carries the word the specification uses.")]
public readonly partial record struct Permission
{
    private const char Separator = ':';

    private readonly string? _value;

    private Permission(string value) => _value = value;

    /// <summary>
    /// The resource half, singular.
    /// </summary>
    public string Resource => Half(0);

    /// <summary>
    /// The action half.
    /// </summary>
    public string Action => Half(1);

    /// <summary>
    /// Reads a permission string, refusing anything outside the format.
    /// </summary>
    /// <param name="value">The permission as <c>resource:action</c>.</param>
    /// <returns>The permission.</returns>
    /// <exception cref="ArgumentException">The value is not a well-formed permission.</exception>
    public static Permission Parse(string value)
    {
        if (!TryParse(value, out Permission permission))
        {
            throw new ArgumentException(
                "A permission is a lowercase resource and action separated by a colon.",
                nameof(value));
        }

        return permission;
    }

    /// <summary>
    /// Reads a permission string, answering whether it is one.
    /// </summary>
    /// <param name="value">The permission as <c>resource:action</c>.</param>
    /// <param name="permission">The permission, where the value is one.</param>
    /// <returns>Whether the value is a well-formed permission.</returns>
    public static bool TryParse(string? value, out Permission permission)
    {
        if (value is null || !Shape().IsMatch(value))
        {
            permission = default;
            return false;
        }

        permission = new Permission(value);
        return true;
    }

    /// <summary>
    /// The permission as it crosses the boundary.
    /// </summary>
    /// <returns>The permission string, or an empty string for an unset permission.</returns>
    public override string ToString() => _value ?? string.Empty;

    [GeneratedRegex(
        "^[a-z][a-z0-9]*(-[a-z0-9]+)*:[a-z][a-z0-9]*(-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Shape();

    private string Half(int index) =>
        _value is null ? string.Empty : _value.Split(Separator)[index];
}
