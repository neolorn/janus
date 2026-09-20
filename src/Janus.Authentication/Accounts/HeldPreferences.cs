using System.Collections.Generic;

namespace Janus.Authentication.Accounts;

/// <summary>
/// The preferences as the account directory holds them: the two the library owns and
/// the values the account has set, which is fewer than the host declared.
/// </summary>
/// <param name="Language">The BCP 47 tag, where the account settled one.</param>
/// <param name="TimeZone">The IANA zone identifier, where the account settled one.</param>
/// <param name="Values">What the account set, by the key the host declared it under.</param>
/// <remarks>Implements REG-PREF-001 and CONV-LAYOUT-001.</remarks>
internal sealed record HeldPreferences(
    string? Language,
    string? TimeZone,
    IReadOnlyDictionary<string, string> Values);
