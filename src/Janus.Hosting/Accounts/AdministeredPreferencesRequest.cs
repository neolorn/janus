using System.Collections.Generic;

namespace Janus.Hosting.Accounts;

/// <summary>
/// Another person's preference set, replaced whole by an administrator, as
/// <c>PUT /admin/accounts/{subject}/preferences</c> takes it.
/// </summary>
/// <param name="Language">The BCP 47 tag, or nothing to clear it.</param>
/// <param name="TimeZone">The IANA zone identifier, or nothing to clear it.</param>
/// <param name="Declared">The values to hold, by the key they were declared under.</param>
/// <param name="Reason">Why, which the change is recorded with.</param>
/// <remarks>Implements IDN-READ-003, REG-PREF-001 and chapter 09 section 8a.</remarks>
internal sealed record AdministeredPreferencesRequest(
    string? Language,
    string? TimeZone,
    IReadOnlyDictionary<string, string>? Declared,
    string? Reason);
