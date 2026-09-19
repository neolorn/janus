using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Hosting.Passwords;

/// <summary>
/// One file of compromised-password hashes, read once and held by prefix.
/// </summary>
/// <remarks>
/// Implements INT-PWD-002 and INT-PWD-003. The first line carries the date the corpus
/// was drawn, so a file too old for <c>password.blocklist.corpusmaxage</c> is refused
/// rather than trusted; every other line is one upper-case SHA-1 in hexadecimal,
/// optionally followed by a colon and the count the provider publishes, which is read
/// past.
/// </remarks>
internal sealed class CorpusFile
{
    private const int HashLength = 40;
    private const int PrefixLength = 5;
    private const char Marker = '#';

    private readonly string _path;

    private FrozenDictionary<string, FrozenSet<string>>? _held;
    private DateOnly _drawn;

    /// <summary>
    /// A file at a path, which is read the first time it is asked for.
    /// </summary>
    /// <param name="path">Where the file is.</param>
    public CorpusFile(string path) => _path = path;

    /// <summary>
    /// The hashes the file holds under one prefix.
    /// </summary>
    /// <param name="prefix">The first five characters of the hash.</param>
    /// <param name="now">The instant the file's age is judged at.</param>
    /// <param name="maximumAge">How old the corpus may be.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The remainder of every hash held under that prefix, or nothing where the file
    /// is absent, unreadable or older than the deployment admits.
    /// </returns>
    public async ValueTask<IReadOnlySet<string>?> RangeAsync(
        string prefix,
        DateTimeOffset now,
        TimeSpan maximumAge,
        CancellationToken cancellationToken)
    {
        FrozenDictionary<string, FrozenSet<string>>? held =
            await HeldAsync(cancellationToken).ConfigureAwait(false);

        if (held is null || DateOnly.FromDateTime(now.UtcDateTime).DayNumber - _drawn.DayNumber
            > maximumAge.TotalDays)
        {
            return null;
        }

        return held.TryGetValue(prefix, out FrozenSet<string>? range) ? range : FrozenSet<string>.Empty;
    }

    // Two callers arriving together each read the file and reach the same answer, so
    // the field is assigned without a lock and the cost of the race is one extra read.
    // A file that is not there yet is looked for again next time.
    private async ValueTask<FrozenDictionary<string, FrozenSet<string>>?> HeldAsync(
        CancellationToken cancellationToken) =>
        _held ??= File.Exists(_path)
            ? await ReadAsync(cancellationToken).ConfigureAwait(false)
            : null;

    private async ValueTask<FrozenDictionary<string, FrozenSet<string>>?> ReadAsync(
        CancellationToken cancellationToken)
    {
        Dictionary<string, HashSet<string>> byPrefix = new(StringComparer.Ordinal);
        bool dated = false;

        using StreamReader reading = File.OpenText(_path);

        while (await reading.ReadLineAsync(cancellationToken).ConfigureAwait(false) is string line)
        {
            string entry = line.Trim();

            if (entry.Length is 0)
            {
                continue;
            }

            if (entry[0] is Marker)
            {
                dated = dated || Dated(entry);

                continue;
            }

            if (entry.Length < HashLength)
            {
                continue;
            }

            string hash = entry[..HashLength].ToUpperInvariant();

            if (!byPrefix.TryGetValue(hash[..PrefixLength], out HashSet<string>? range))
            {
                range = new HashSet<string>(StringComparer.Ordinal);
                byPrefix[hash[..PrefixLength]] = range;
            }

            _ = range.Add(hash[PrefixLength..]);
        }

        // INT-PWD-002: a corpus with no date cannot be judged against the maximum age,
        // and a corpus that cannot be judged is one the deployment does not screen on.
        return dated
            ? byPrefix.ToFrozenDictionary(
                held => held.Key,
                held => held.Value.ToFrozenSet(StringComparer.Ordinal),
                StringComparer.Ordinal)
            : null;
    }

    private bool Dated(string line)
    {
        if (!DateOnly.TryParseExact(
            line[1..].Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateOnly drawn))
        {
            return false;
        }

        _drawn = drawn;

        return true;
    }
}
