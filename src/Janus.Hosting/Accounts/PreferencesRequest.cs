using System.Collections.Generic;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The preference set, replaced whole.
/// </summary>
/// <param name="Language">The BCP 47 tag, or nothing to clear it.</param>
/// <param name="TimeZone">The IANA zone identifier, or nothing to clear it.</param>
/// <param name="Declared">The values to hold, by the key they were declared under.</param>
/// <remarks>Implements REG-PREF-001.</remarks>
internal sealed record PreferencesRequest(
    string? Language,
    string? TimeZone,
    IReadOnlyDictionary<string, string>? Declared);
