using System;
using System.Collections.Generic;
using System.Text;

namespace Janus.Authentication.Passwords;

/// <summary>
/// Reads a password for what is worth saying about it.
/// </summary>
/// <remarks>Implements AUTH-PASS-005.</remarks>
internal static class PasswordAdvice
{
    // Below this many distinct characters a password is repetitive however long it is,
    // which is what the heuristic exists to notice. It refuses nothing.
    private const int DistinctCharactersOfAStrongPassword = 8;

    /// <summary>
    /// What is worth telling the person.
    /// </summary>
    /// <param name="password">The password, in UTF-8.</param>
    /// <param name="ownWords">
    /// The person's own identifiers and profile fields, and the service name.
    /// </param>
    /// <returns>The feedback, which never refuses.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static PasswordFeedback On(byte[] password, IReadOnlyCollection<string> ownWords)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(ownWords);

        string text = Encoding.UTF8.GetString(password);

        return new PasswordFeedback(
            Weak(text),
            [.. Appearing(text, ownWords)]);
    }

    /// <summary>
    /// The person's own words that appear in a password, which is what the context
    /// rejection source refuses on where a host has enabled it.
    /// </summary>
    /// <param name="text">The password.</param>
    /// <param name="ownWords">The person's own words and the service name.</param>
    /// <returns>Those that appear.</returns>
    public static IEnumerable<string> Appearing(string text, IReadOnlyCollection<string> ownWords)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(ownWords);

        foreach (string word in ownWords)
        {
            if (word.Length > 2 && text.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                yield return word;
            }
        }
    }

    private static bool Weak(string text)
    {
        HashSet<char> distinct = [.. text];

        return distinct.Count < DistinctCharactersOfAStrongPassword;
    }
}
