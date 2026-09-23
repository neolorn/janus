using System;
using System.Collections.Generic;

namespace Janus.Authentication.Sending;

/// <summary>
/// Which of the deployment's languages a message to one person goes out in: the
/// account's stored preference, else the locale of the request the person made, else
/// every language the deployment declares.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-001 and D-055. A value names a declared language by the lookup
/// of RFC 4647 section 3.4, so <c>en-GB</c> finds <c>en</c>; a value that finds none is
/// taken as unset, since no template is written in it (AUTH-ABUSE-005).
/// </remarks>
internal static class RecipientLanguage
{
    /// <summary>
    /// The language a message goes out in.
    /// </summary>
    /// <param name="preference">The account's stored preference, where there is one.</param>
    /// <param name="requested">
    /// The locale of the request the person made, where the message answers one of
    /// theirs.
    /// </param>
    /// <param name="declared">The languages the deployment declares.</param>
    /// <returns>
    /// The declared language, or nothing where the message goes out in every one.
    /// </returns>
    /// <exception cref="ArgumentNullException">The declared languages are absent.</exception>
    public static string? Of(string? preference, string? requested, IReadOnlyList<string> declared) =>
        Found(preference, declared) ?? Found(requested, declared);

    /// <summary>
    /// The declared language a tag finds.
    /// </summary>
    /// <param name="tag">The tag, as the account or the request carried it.</param>
    /// <param name="declared">The languages the deployment declares.</param>
    /// <returns>The declared language, or nothing where the tag finds none.</returns>
    /// <exception cref="ArgumentNullException">The declared languages are absent.</exception>
    public static string? Found(string? tag, IReadOnlyList<string> declared)
    {
        ArgumentNullException.ThrowIfNull(declared);

        string range = tag?.Trim() ?? string.Empty;

        while (range.Length > 0)
        {
            foreach (string language in declared)
            {
                if (string.Equals(language, range, StringComparison.OrdinalIgnoreCase))
                {
                    return language;
                }
            }

            int cut = range.LastIndexOf('-');

            if (cut < 0)
            {
                return null;
            }

            range = range[..cut];

            // A single character left at the end opens an extension, which goes with
            // the subtag it introduced.
            if (range.Length > 2 && range[^2] == '-')
            {
                range = range[..^2];
            }
        }

        return null;
    }
}
