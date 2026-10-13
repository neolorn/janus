using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Callbacks;

namespace Janus.Authentication.Tests.Callbacks;

/// <summary>
/// The claimed provider events, holding each claim so a test can read what is held.
/// </summary>
internal sealed class CallbackEventsInMemory : ICallbackEvents
{
    private readonly List<Claimed> _claimed = [];

    /// <summary>
    /// How many claims are held, settled or not.
    /// </summary>
    public int Held => _claimed.Count;

    /// <summary>
    /// How many claims are settled.
    /// </summary>
    public int Settled => _claimed.FindAll(claimed => claimed.SettledAt is not null).Count;

    /// <inheritdoc/>
    public ValueTask<CallbackClaim> ClaimAsync(
        string callback,
        byte[] identifier,
        DateTimeOffset at,
        DateTimeOffset stale,
        CancellationToken cancellationToken)
    {
        if (_claimed.Find(claimed => claimed.Is(callback, identifier)) is not Claimed held)
        {
            _claimed.Add(new Claimed(callback, identifier, at, null));

            return ValueTask.FromResult(CallbackClaim.Taken);
        }

        if (held.SettledAt is not null)
        {
            return ValueTask.FromResult(CallbackClaim.Settled);
        }

        if (held.ClaimedAt > stale)
        {
            return ValueTask.FromResult(CallbackClaim.InProgress);
        }

        _claimed[_claimed.IndexOf(held)] = held with { ClaimedAt = at };

        return ValueTask.FromResult(CallbackClaim.Taken);
    }

    /// <inheritdoc/>
    public ValueTask<bool> CarryAsync(
        string callback,
        byte[] identifier,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (_claimed.Exists(claimed => claimed.Is(callback, identifier)))
        {
            return ValueTask.FromResult(false);
        }

        _claimed.Add(new Claimed(callback, identifier, at, at));

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask SettleAsync(
        string callback,
        byte[] identifier,
        DateTimeOffset claimed,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        int held = _claimed.FindIndex(one => one.Holds(callback, identifier, claimed));

        if (held >= 0)
        {
            _claimed[held] = _claimed[held] with { SettledAt = at };
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ReleaseAsync(
        string callback,
        byte[] identifier,
        DateTimeOffset claimed,
        CancellationToken cancellationToken)
    {
        _ = _claimed.RemoveAll(one => one.Holds(callback, identifier, claimed));

        return ValueTask.CompletedTask;
    }

    private sealed record Claimed(string Callback, byte[] Identifier, DateTimeOffset ClaimedAt, DateTimeOffset? SettledAt)
    {
        public bool Is(string callback, byte[] identifier) =>
            string.Equals(Callback, callback, StringComparison.Ordinal)
            && Identifier.AsSpan().SequenceEqual(identifier);

        // The unsettled claim the delivery that took it at that instant still holds.
        public bool Holds(string callback, byte[] identifier, DateTimeOffset claimed) =>
            Is(callback, identifier) && ClaimedAt == claimed && SettledAt is null;
    }
}
