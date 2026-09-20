using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.SignIn;
using Janus.Core;

namespace Janus.Authentication.Tests.SignIn;

/// <summary>
/// The sign-in links and codes that have gone out, held in memory. One stands per
/// account per catalogue entry, as the unique index holds it.
/// </summary>
internal sealed class PendingSignInStoreInMemory : IPendingSignInStore
{
    private readonly Dictionary<string, PendingSignIn> _pending = [];

    /// <inheritdoc/>
    public ValueTask<PendingSignIn?> FindAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_pending.GetValueOrDefault(Key(fingerprint)));

    /// <inheritdoc/>
    public ValueTask<PendingSignIn?> FindAsync(
        SubjectId subject,
        Factor factor,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_pending.Values
            .SingleOrDefault(held => held.Subject == subject && held.Factor == factor));

    /// <inheritdoc/>
    public ValueTask ReplaceAsync(PendingSignIn pending, CancellationToken cancellationToken)
    {
        foreach (string key in _pending
            .Where(entry =>
                entry.Value.Subject == pending.Subject && entry.Value.Factor == pending.Factor)
            .Select(entry => entry.Key)
            .ToList())
        {
            _pending.Remove(key);
        }

        _pending[Key(pending.Fingerprint)] = pending;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(PendingSignIn pending, CancellationToken cancellationToken)
    {
        _pending[Key(pending.Fingerprint)] = pending;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(byte[] fingerprint, CancellationToken cancellationToken)
    {
        _pending.Remove(Key(fingerprint));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<string> lapsed = [.. _pending
            .Where(entry => entry.Value.ExpiresAt <= now)
            .Select(entry => entry.Key)];

        foreach (string key in lapsed)
        {
            _pending.Remove(key);
        }

        return ValueTask.FromResult(lapsed.Count);
    }

    private static string Key(byte[] fingerprint) => Convert.ToHexString(fingerprint);
}
