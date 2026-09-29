using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Passwords;
using Janus.Core;

namespace Janus.Hosting.Passwords;

/// <summary>
/// The words the dictionary rejection source reads: the lists the package carries and
/// the words the host declares beside them.
/// </summary>
/// <remarks>
/// Implements AUTH-PASS-004. The lists travel in the package, as the leaked list does,
/// and nothing is read from the deployment's files. A list the package cannot open gets
/// the screening failure, so the password is refused rather than accepted unscreened.
/// </remarks>
internal sealed class WordList : IWordList
{
    /// <summary>The resource the English list travels as.</summary>
    public const string EnglishResource = "Janus.Hosting.Passwords.english-words.txt";

    /// <summary>The resource the Arabic transliteration list travels as.</summary>
    public const string ArabicResource = "Janus.Hosting.Passwords.arabic-words.txt";

    private const int ShortestWord = 4;

    private static readonly string[] Resources = [EnglishResource, ArabicResource];

    private readonly DictionaryWords _declared;
    private readonly Func<string, Stream?> _open;

    private FrozenSet<string>? _held;

    /// <summary>
    /// The lists the package carries, with the host's words beside them.
    /// </summary>
    /// <param name="declared">The words the host adds.</param>
    public WordList(DictionaryWords declared)
        : this(declared, resource => typeof(WordList).Assembly.GetManifestResourceStream(resource))
    {
    }

    /// <summary>
    /// Lists from somewhere else, which is how a list the package cannot open is read in
    /// a test.
    /// </summary>
    /// <param name="declared">The words the host adds.</param>
    /// <param name="open">Opens a list by its resource, or answers nothing where there is none.</param>
    public WordList(DictionaryWords declared, Func<string, Stream?> open)
    {
        _declared = declared;
        _open = open;
    }

    /// <inheritdoc/>
    public async ValueTask<Result<bool>> MatchesAsync(
        [NeverLogged] string password,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);

        FrozenSet<string>? listed = await HeldAsync(cancellationToken).ConfigureAwait(false);

        if (listed is null)
        {
            return Result.Failure<bool>(Error.From(ErrorCodes.ScreeningUnavailable));
        }

        // A word the password merely contains is a word the password is built from,
        // which is what the source rejects. Digits are kept, so an Arabizi word matches
        // as it is written.
        string folded = password.ToUpperInvariant();

        foreach (string word in listed)
        {
            if (folded.Contains(word, StringComparison.Ordinal))
            {
                return Result.Success(true);
            }
        }

        return Result.Success(false);
    }

    // Two callers arriving together each read the lists and reach the same answer, so
    // the field is assigned without a lock and the cost of the race is one extra read.
    // A list that cannot be opened is looked for again next time.
    private async ValueTask<FrozenSet<string>?> HeldAsync(CancellationToken cancellationToken)
    {
        if (_held is not null)
        {
            return _held;
        }

        HashSet<string> words = new(StringComparer.Ordinal);

        foreach (string resource in Resources)
        {
            using Stream? list = _open(resource);

            if (list is null)
            {
                return null;
            }

            using StreamReader reading = new(list);

            while (await reading.ReadLineAsync(cancellationToken).ConfigureAwait(false) is string line)
            {
                Hold(words, line);
            }
        }

        foreach (string word in _declared.Added)
        {
            Hold(words, word);
        }

        return _held = words.ToFrozenSet(StringComparer.Ordinal);
    }

    // Every character counts toward the length a match needs, digits included; a
    // shorter fragment matches everything and is not a word for this purpose.
    private static void Hold(HashSet<string> words, string line)
    {
        string word = line.Trim();

        if (word.Length >= ShortestWord)
        {
            _ = words.Add(word.ToUpperInvariant());
        }
    }
}
