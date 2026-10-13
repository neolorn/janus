using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// The words a host adds to the lists the <c>dictionary</c> password source ships: the
/// English list and the Arabic transliteration list.
/// </summary>
/// <remarks>
/// Implements AUTH-PASS-004 and LIB-HOST-001. A host extends the shipped lists by this
/// declaration only, never by a file the deployment holds; absent, the shipped lists
/// alone answer. A word is compared without regard to case, and a word shorter than a
/// match needs is held and matches nothing.
/// </remarks>
public sealed class DictionaryWords
{
    private readonly HashSet<string> _added;

    private DictionaryWords(HashSet<string> added) => _added = added;

    /// <summary>
    /// The declaration of a host that adds nothing to the shipped lists.
    /// </summary>
    public static DictionaryWords Default { get; } = Of([]);

    /// <summary>
    /// Every word the host adds.
    /// </summary>
    public IReadOnlyCollection<string> Added => _added;

    /// <summary>
    /// Reads a host's additions to the shipped lists.
    /// </summary>
    /// <param name="added">The words the host adds.</param>
    /// <returns>The declaration.</returns>
    /// <exception cref="ArgumentNullException">The collection, or a word in it, is absent.</exception>
    public static DictionaryWords Of(IEnumerable<string> added)
    {
        ArgumentNullException.ThrowIfNull(added);

        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string word in added)
        {
            ArgumentNullException.ThrowIfNull(word);

            words.Add(word.Trim());
        }

        return new DictionaryWords(words);
    }
}
