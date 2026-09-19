using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Passwords;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Tests.Passwords;

/// <summary>
/// The compromised-password corpus, holding what a test put in it and recording what
/// it was asked.
/// </summary>
internal sealed class LeakedPasswordCorpusInMemory : ILeakedPasswordCorpus
{
    private readonly Dictionary<BlocklistSource, HashSet<string>> _held = [];

    /// <summary>
    /// The corpora that cannot answer.
    /// </summary>
    public HashSet<BlocklistSource> Unreachable { get; } = [];

    /// <summary>
    /// Every value the screening passed out, which is what a test asserts a full hash
    /// is never among.
    /// </summary>
    public List<string> Asked { get; } = [];

    /// <summary>
    /// Puts a password's hash in a corpus.
    /// </summary>
    /// <param name="source">Which corpus.</param>
    /// <param name="password">The password.</param>
    public void Hold(BlocklistSource source, string password)
    {
        if (!_held.TryGetValue(source, out HashSet<string>? hashes))
        {
            hashes = [];
            _held[source] = hashes;
        }

        hashes.Add(Hash(password));
    }

    /// <summary>
    /// The hash of a password, as the corpus holds it.
    /// </summary>
    /// <param name="password">The password.</param>
    /// <returns>The upper-case hexadecimal hash.</returns>
    public static string Hash(string password) =>
#pragma warning disable CA5350 // The published corpus is SHA-1 ranges (INT-PWD-001); the value is a lookup key and never a security claim.
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
#pragma warning restore CA5350

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlySet<string>>> RangeAsync(
        BlocklistSource source,
        string prefix,
        CancellationToken cancellationToken)
    {
        Asked.Add(prefix);

        if (Unreachable.Contains(source))
        {
            return ValueTask.FromResult(
                Result.Failure<IReadOnlySet<string>>(Error.From(ErrorCodes.ScreeningUnavailable)));
        }

        HashSet<string> range = [];

        if (_held.TryGetValue(source, out HashSet<string>? hashes))
        {
            foreach (string hash in hashes)
            {
                if (hash.StartsWith(prefix, StringComparison.Ordinal))
                {
                    range.Add(hash[prefix.Length..]);
                }
            }
        }

        return ValueTask.FromResult(Result.Success<IReadOnlySet<string>>(range));
    }
}
