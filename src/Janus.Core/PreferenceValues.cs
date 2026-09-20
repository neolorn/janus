using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// The preference group of an account: the two the library owns, and the values of the
/// keys the host declared.
/// </summary>
/// <param name="Language">The language the person reads, as a BCP 47 tag.</param>
/// <param name="TimeZone">The zone their times are rendered in, as an IANA identifier.</param>
/// <param name="Declared">
/// The value in force for each key the host declared, whether the account set it or it
/// stands at the declared default.
/// </param>
/// <remarks>Implements REG-ACCT-001 and REG-PREF-001.</remarks>
public sealed record PreferenceValues(
    string? Language,
    string? TimeZone,
    IReadOnlyDictionary<string, string> Declared);
