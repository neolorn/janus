using System;
using System.Collections.Generic;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// One preference key a host declares at startup: what it is called, what type it
/// takes, what it is when the person has never set it, and who may set it.
/// </summary>
/// <param name="Name">The key, as a request and the export name it.</param>
/// <param name="Kind">The type its values take.</param>
/// <param name="Default">The value in force where the person has set none.</param>
/// <param name="AdministratorOnly">Whether only an administrator may set it.</param>
/// <param name="Choices">The values a choice admits, and nothing for the other kinds.</param>
/// <remarks>
/// Implements REG-PREF-001. Theme, text size, reduced motion and the like belong to the
/// person rather than to one application, and the set differs per host, so the library
/// holds them without understanding any of them.
/// </remarks>
public sealed record PreferenceDeclaration(
    string Name,
    PreferenceKind Kind,
    string Default,
    bool AdministratorOnly = false,
    IReadOnlySet<string>? Choices = null)
{
    /// <summary>
    /// Whether a value is one this key admits. What the value means is the host's
    /// business; that it is of the declared type is the library's.
    /// </summary>
    /// <param name="value">The value as it was sent.</param>
    /// <returns>Whether the key admits it.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public bool Admits(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Kind switch
        {
            PreferenceKind.Flag => value is "true" or "false",
            PreferenceKind.Number => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            PreferenceKind.Choice => Choices is not null && Choices.Contains(value),
            _ => true,
        };
    }
}
