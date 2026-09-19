using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Passwords;
using Janus.Core;

namespace Janus.Authentication.Tests.Passwords;

/// <summary>
/// The word list, holding what a test put in it.
/// </summary>
internal sealed class WordListInMemory : IWordList
{
    /// <summary>
    /// The words it holds.
    /// </summary>
    public List<string> Words { get; } = [];

    /// <summary>
    /// Whether the list can be read at all.
    /// </summary>
    public bool Unreachable { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<bool>> MatchesAsync(string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);

        if (Unreachable)
        {
            return ValueTask.FromResult(Result.Failure<bool>(Error.From(ErrorCodes.ScreeningUnavailable)));
        }

        return ValueTask.FromResult(Result.Success(
            Words.Exists(word => password.Contains(word, StringComparison.OrdinalIgnoreCase))));
    }
}
