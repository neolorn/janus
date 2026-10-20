using System;
using System.Collections.Generic;
using System.Linq;

namespace Janus.Testing;

/// <summary>
/// The token a link the library sent carries, read as the landing page reads it.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-003 and API-LAND-001. Every link the library sends is
/// <c>&lt;origin&gt;/link#&lt;kind&gt;.&lt;token&gt;</c>, and the token is base64url, so
/// it ends where its alphabet does; whatever text a template sets around the link is
/// not read.
/// </remarks>
internal static class Links
{
    private const string Landing = "/link#";

    /// <summary>
    /// The token of the last link of a kind that any of the texts carries.
    /// </summary>
    /// <param name="texts">The texts that were sent, in the order they were sent.</param>
    /// <param name="kind">The link kind, as chapter 10 spells it.</param>
    /// <returns>The token.</returns>
    /// <exception cref="InvalidOperationException">No text carries a link of the kind.</exception>
    public static string Token(IEnumerable<string> texts, string kind)
    {
        string marker = Landing + kind + ".";

        foreach (string text in texts.Reverse())
        {
            int at = text.LastIndexOf(marker, StringComparison.Ordinal);

            if (at >= 0)
            {
                string from = text[(at + marker.Length)..];
                int end = from.TakeWhile(Base64Url).Count();

                if (end is not 0)
                {
                    return from[..end];
                }
            }
        }

        throw new InvalidOperationException("No message carried a link of the kind " + kind + ".");
    }

    /// <summary>
    /// The token of a link the library printed.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <param name="kind">The link kind, as chapter 10 spells it.</param>
    /// <returns>The token.</returns>
    /// <exception cref="ArgumentException">The link is not one of the kind.</exception>
    public static string Token(Uri link, string kind)
    {
        ArgumentNullException.ThrowIfNull(link);

        string fragment = link.Fragment;
        string marker = "#" + kind + ".";

        return fragment.StartsWith(marker, StringComparison.Ordinal)
            && fragment.Length > marker.Length
            && fragment[marker.Length..].All(Base64Url)
                ? fragment[marker.Length..]
                : throw new ArgumentException("The link is not a link of the kind " + kind + ".", nameof(link));
    }

    private static bool Base64Url(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '-' or '_';
}
