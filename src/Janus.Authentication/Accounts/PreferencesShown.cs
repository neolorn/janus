using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Accounts;

/// <summary>
/// The preferences of an account as they are read: the two the library owns, and the
/// value in force for each key the host declared.
/// </summary>
/// <remarks>Implements REG-ACCT-001, REG-PREF-001 and IDN-READ-003.</remarks>
internal static class PreferencesShown
{
    /// <summary>
    /// Reads them.
    /// </summary>
    /// <param name="held">What the account holds.</param>
    /// <param name="declarations">The keys the host declared.</param>
    /// <returns>Each declared key with the value the account set, or its default.</returns>
    /// <exception cref="ArgumentNullException">Either is absent.</exception>
    public static PreferenceValues Of(HeldPreferences held, PreferenceDeclarations declarations)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(declarations);

        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (PreferenceDeclaration declaration in declarations.All)
        {
            values[declaration.Name] = held.Values.TryGetValue(declaration.Name, out string? set)
                ? set
                : declaration.Default;
        }

        return new PreferenceValues(held.Language, held.TimeZone, values);
    }
}
