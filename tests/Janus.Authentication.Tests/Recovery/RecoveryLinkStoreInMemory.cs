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

    /// <summary>
    /// The unit of work the operations under test run in. Where a test names it, a link
    /// replaced inside one that rolls back is put back as it stood.
    /// </summary>
    public UnitOfWorkInMemory? Work { get; set; }

    /// <summary>
    /// How many links the store holds.
    /// </summary>
    public int Count => _links.Count;

    /// <inheritdoc/>
    public ValueTask<RecoveryLink?> FindAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_links.GetValueOrDefault(Key(fingerprint)));

    /// <inheritdoc/>
    public ValueTask<RecoveryLink?> FindForUpdateAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken) =>
        FindAsync(fingerprint, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<RecoveryLink?> FindAsync(
        EnrolmentSessionId session,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_links.Values.SingleOrDefault(link => link.Session == session));

    /// <inheritdoc/>
    public ValueTask<RecoveryLink?> FindForUpdateAsync(
        EnrolmentSessionId session,
        CancellationToken cancellationToken) =>
        FindAsync(session, cancellationToken);

    /// <inheritdoc/>
    public ValueTask ReplaceAsync(RecoveryLink link, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);

        if (Work is { Open: true })
        {
            var held = new Dictionary<string, RecoveryLink>(_links);

            Work.Undoing(() =>
            {
                _links.Clear();

                foreach (KeyValuePair<string, RecoveryLink> one in held)
                {
                    _links[one.Key] = one.Value;
                }
            });
        }

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
