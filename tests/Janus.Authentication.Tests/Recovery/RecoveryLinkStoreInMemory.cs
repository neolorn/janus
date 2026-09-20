using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Recovery;
using Janus.Core;

namespace Janus.Authentication.Tests.Recovery;

/// <summary>
/// The links recovery has sent, held in memory. One stands per account per purpose,
/// as the unique index holds it.
/// </summary>
internal sealed class RecoveryLinkStoreInMemory : IRecoveryLinkStore
{
    private readonly Dictionary<string, RecoveryLink> _links = [];

    /// <inheritdoc/>
    public ValueTask<RecoveryLink?> FindAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_links.GetValueOrDefault(Key(fingerprint)));

    /// <inheritdoc/>
    public ValueTask<RecoveryLink?> FindAsync(
        EnrolmentSessionId session,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_links.Values.SingleOrDefault(link => link.Session == session));

    /// <inheritdoc/>
    public ValueTask ReplaceAsync(RecoveryLink link, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);

        foreach (string key in _links
            .Where(entry =>
                entry.Value.Subject == link.Subject
                && entry.Value.Purpose == link.Purpose
                && entry.Value.SpentAt is null)
            .Select(entry => entry.Key)
            .ToList())
        {
            _ = _links.Remove(key);
        }

        _links[Key(link.Fingerprint)] = link;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(RecoveryLink link, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);

        _links[Key(link.Fingerprint)] = link;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(byte[] fingerprint, CancellationToken cancellationToken)
    {
        _ = _links.Remove(Key(fingerprint));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<string> lapsed = [.. _links
            .Where(entry => entry.Value.ExpiresAt <= now)
            .Select(entry => entry.Key)];

        foreach (string key in lapsed)
        {
            _ = _links.Remove(key);
        }

        return ValueTask.FromResult(lapsed.Count);
    }

    private static string Key(byte[] fingerprint) => Convert.ToHexString(fingerprint);
}
