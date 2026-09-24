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
/// The word list the dictionary rejection source reads, from the file beside the
/// application.
/// </summary>
/// <param name="directory">The directory the list is read from.</param>
/// <remarks>
/// Implements AUTH-PASS-004. The source is off by default; a deployment that turns it
/// on and holds no list gets the screening failure, so the password is refused rather
/// than accepted unscreened.
/// </remarks>
internal sealed class WordList(string directory) : IWordList
{
    /// <summary>
    /// The directory beside the application that a deployment holds its list in.
    /// </summary>
    public const string Directory = "identity-corpus";

    /// <summary>The file the listed words are read from.</summary>
    public const string WordsFile = "words.txt";

    private const int ShortestWord = 4;
    private const char Marker = '#';

    private readonly string _path = Path.Combine(directory, WordsFile);

    private FrozenSet<string>? _held;

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
        // which is what the source rejects; shorter fragments match everything and
        // are not words for this purpose.
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

    // Two callers arriving together each read the file and reach the same answer, so
    // the field is assigned without a lock and the cost of the race is one extra read.
    // A list that is not there yet is looked for again next time.
    private async ValueTask<FrozenSet<string>?> HeldAsync(CancellationToken cancellationToken) =>
        _held ??= File.Exists(_path)
            ? await ReadAsync(cancellationToken).ConfigureAwait(false)
            : null;

    private async ValueTask<FrozenSet<string>> ReadAsync(CancellationToken cancellationToken)
    {
        HashSet<string> words = new(StringComparer.Ordinal);

        using StreamReader reading = File.OpenText(_path);

        while (await reading.ReadLineAsync(cancellationToken).ConfigureAwait(false) is string line)
        {
            string word = line.Trim();

            if (word.Length >= ShortestWord && word[0] is not Marker)
            {
                _ = words.Add(word.ToUpperInvariant());
            }
        }

        return words.ToFrozenSet(StringComparer.Ordinal);
    }
}
