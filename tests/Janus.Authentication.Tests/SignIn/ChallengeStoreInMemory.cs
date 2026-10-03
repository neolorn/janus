using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.SignIn;

namespace Janus.Authentication.Tests.SignIn;

/// <summary>
/// The sign-ins in progress, held in memory and keyed by what the caller's handle
/// hashes to, as the table keys them.
/// </summary>
internal sealed class ChallengeStoreInMemory : IChallengeStore
{
    private readonly Dictionary<string, Challenge> _challenges = [];

    /// <inheritdoc/>
    public ValueTask<Challenge?> FindAsync(byte[] fingerprint, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_challenges.GetValueOrDefault(Key(fingerprint)));

    /// <summary>
    /// Whether another transaction removed the challenge while this one waited for its
    /// lock, applied as the lock is taken.
    /// </summary>
    public bool RemovedMeanwhile { get; set; }

    /// <inheritdoc/>
    public ValueTask<Challenge?> FindForUpdateAsync(byte[] fingerprint, CancellationToken cancellationToken)
    {
        if (RemovedMeanwhile)
        {
            _challenges.Remove(Key(fingerprint));
        }

        return FindAsync(fingerprint, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask AddAsync(Challenge challenge, CancellationToken cancellationToken)
    {
        _challenges[Key(challenge.Fingerprint)] = challenge;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(Challenge challenge, CancellationToken cancellationToken)
    {
        _challenges[Key(challenge.Fingerprint)] = challenge;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(byte[] fingerprint, CancellationToken cancellationToken)
    {
        _challenges.Remove(Key(fingerprint));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<string> lapsed = [.. _challenges
            .Where(entry => entry.Value.ExpiresAt <= now)
            .Select(entry => entry.Key)];

        foreach (string key in lapsed)
        {
            _challenges.Remove(key);
        }

        return ValueTask.FromResult(lapsed.Count);
    }

    private static string Key(byte[] fingerprint) => Convert.ToHexString(fingerprint);
}
