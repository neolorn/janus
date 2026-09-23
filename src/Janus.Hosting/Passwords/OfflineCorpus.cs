using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Hosting.Passwords;

/// <summary>
/// The curated list of compromised-password hashes the package carries, read once and
/// held by prefix.
/// </summary>
/// <remarks>
/// Implements AUTH-PASS-004 and INT-PWD-002. The list travels in the package, so the
/// fallback answers on a deployment that holds no file of its own; the first line
/// carries the date it was drawn, so a list too old for
/// <c>password.blocklist.corpusmaxage</c> is refused rather than trusted, and every
/// other line is one upper-case SHA-1 in hexadecimal, optionally followed by a colon
/// and the count the provider publishes, which is read past.
/// </remarks>
internal sealed class OfflineCorpus
{
    /// <summary>The resource the list travels as.</summary>
    public const string Resource = "Janus.Hosting.Passwords.leaked-passwords.txt";

    private const int HashLength = 40;
    private const int PrefixLength = 5;
    private const char Marker = '#';

    private readonly Func<Stream?> _open;

    private FrozenDictionary<string, FrozenSet<string>>? _held;
    private DateOnly _drawn;
    private bool _read;

    /// <summary>
    /// The list the package carries.
    /// </summary>
    public OfflineCorpus()
        : this(() => typeof(OfflineCorpus).Assembly.GetManifestResourceStream(Resource))
    {
    }

    /// <summary>
    /// A list from somewhere else, which is how a list the package does not carry is
    /// read in a test.
    /// </summary>
    /// <param name="open">Opens the list, or answers nothing where there is none.</param>
    public OfflineCorpus(Func<Stream?> open) => _open = open;

    /// <summary>
    /// The hashes the list holds under one prefix.
    /// </summary>
    /// <param name="prefix">The first five characters of the hash.</param>
    /// <param name="now">The instant the list's age is judged at.</param>
    /// <param name="maximumAge">How old the list may be.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The remainder of every hash held under that prefix, or nothing where the list
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

    // Two callers arriving together each read the list and reach the same answer, so
    // the fields are assigned without a lock and the cost of the race is one extra read.
    private async ValueTask<FrozenDictionary<string, FrozenSet<string>>?> HeldAsync(
        CancellationToken cancellationToken)
    {
        if (_read)
        {
            return _held;
        }

        using (Stream? reading = _open())
        {
            _held = reading is null
                ? null
                : await ReadAsync(reading, cancellationToken).ConfigureAwait(false);
        }

        _read = true;

        return _held;
    }

    private async ValueTask<FrozenDictionary<string, FrozenSet<string>>?> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        Dictionary<string, HashSet<string>> byPrefix = new(StringComparer.Ordinal);
        bool dated = false;

        using StreamReader reading = new(stream, leaveOpen: true);

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

        // INT-PWD-002: a list with no date cannot be judged against the maximum age,
        // and a list that cannot be judged is one the deployment does not screen on.
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
