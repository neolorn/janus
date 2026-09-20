using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The language, the time zone and the host-declared values.
/// </summary>
/// <param name="Language">The BCP 47 tag.</param>
/// <param name="TimeZone">The IANA zone identifier.</param>
/// <param name="Declared">The values, by the key the host declared them under.</param>
/// <remarks>Implements REG-PREF-001.</remarks>
internal sealed record PreferencesView(
    string? Language,
    string? TimeZone,
    IReadOnlyDictionary<string, string> Declared)
{
    /// <summary>
    /// Reads the preferences.
    /// </summary>
    /// <param name="preferences">The preferences.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The preferences are absent.</exception>
    public static PreferencesView Of(PreferenceValues preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        return new PreferencesView(
            preferences.Language,
            preferences.TimeZone,
            preferences.Declared);
    }
}
