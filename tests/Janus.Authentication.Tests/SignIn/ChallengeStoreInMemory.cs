using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.SignIn;

namespace Janus.Authentication.Tests.SignIn;

/// <summary>
/// The sign-ins in progress, held in memory and keyed by what the caller's handle
/// hashes to, as the table keys them, and found by the second handle a pressed link
/// answered as by the first (D-238).
/// </summary>
internal sealed class ChallengeStoreInMemory : IChallengeStore
{
    private readonly Dictionary<string, Challenge> _challenges = [];

    /// <inheritdoc/>
    public ValueTask<Challenge?> FindAsync(byte[] fingerprint, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            _challenges.GetValueOrDefault(Key(fingerprint))
            ?? _challenges.Values.FirstOrDefault(challenge =>
                challenge.LandingFingerprint is { } landing && landing.AsSpan().SequenceEqual(fingerprint)));

    /// <summary>
    /// What each sign-in in progress holds, as the table would show it.
    /// </summary>
    public IReadOnlyCollection<Challenge> Held => _challenges.Values;

    /// <summary>
    /// Whether another transaction removed the challenge while this one waited for its
    /// lock, applied as the lock is taken.
    /// </summary>
    public bool RemovedMeanwhile { get; set; }

    /// <inheritdoc/>
    public ValueTask<Challenge?> FindForUpdateAsync(byte[] fingerprint, CancellationToken cancellationToken)
    {
        if (RemovedMeanwhile && _challenges.GetValueOrDefault(Key(fingerprint)) is null)
        {
            foreach (Challenge landed in _challenges.Values
                .Where(challenge => challenge.LandingFingerprint is { } landing && landing.AsSpan().SequenceEqual(fingerprint))
                .ToList())
            {
                _challenges.Remove(Key(landed.Fingerprint));
            }
        }
        else if (RemovedMeanwhile)
        {
            _challenges.Remove(Key(fingerprint));
        }

        return FindAsync(fingerprint, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<Challenge?> LatestUnansweredAsync(
        byte[] browser,
        byte[] identifier,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_challenges.Values
            .Where(challenge => challenge.BoundTo(browser)
                && challenge.Identifier.AsSpan().SequenceEqual(identifier)
                && !challenge.HasExpired(now)
                && challenge.Presented.Count == 0)
            .OrderByDescending(challenge => challenge.CreatedAt)
            .FirstOrDefault());

    /// <inheritdoc/>
    public ValueTask RemoveUnnamedAsync(byte[] browser, CancellationToken cancellationToken)
    {
        foreach (Challenge unnamed in _challenges.Values
            .Where(challenge => challenge.Unnamed && challenge.BoundTo(browser))
            .ToList())
        {
            _challenges.Remove(Key(unnamed.Fingerprint));
        }

        return ValueTask.CompletedTask;
    }

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
