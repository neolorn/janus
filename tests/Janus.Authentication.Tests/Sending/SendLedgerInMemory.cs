using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The sending ledger, holding what each key has been sent and what credit it has,
/// so a test can read exactly what a send counted and what a report released.
/// </summary>
internal sealed class SendLedgerInMemory : ISendLedger
{
    private readonly Dictionary<RestrictionKey, List<DateTimeOffset>> _sends = [];
    private readonly Dictionary<RestrictionKey, int> _credit = [];
    private readonly Dictionary<string, (RestrictionKey[] Counted, DateTimeOffset At)> _records = [];

    /// <summary>
    /// The keys the ledger holds a record for.
    /// </summary>
    public IReadOnlyCollection<RestrictionKey> Keys => _sends.Keys;

    /// <summary>
    /// The keys whose granted credit a send has spent, in order.
    /// </summary>
    public List<RestrictionKey> Spent { get; } = [];

    /// <summary>
    /// What one key has been sent.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The times, oldest first.</returns>
    public IReadOnlyList<DateTimeOffset> Sends(RestrictionKey key) =>
        _sends.TryGetValue(key, out List<DateTimeOffset>? sent) ? sent : [];

    /// <summary>
    /// Records times against a key without a send, which sets up a bucket a test
    /// wants already full.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="sends">The times.</param>
    public void Given(RestrictionKey key, params DateTimeOffset[] sends) =>
        _sends[key] = [.. sends];

    /// <inheritdoc/>
    public ValueTask<IReadOnlyDictionary<RestrictionKey, SendCounter>> CountersAsync(
        IReadOnlyCollection<RestrictionKey> keys,
        DateTimeOffset stale,
        CancellationToken cancellationToken)
    {
        foreach (RestrictionKey held in _sends
            .Where(one => one.Value.Count > 0 && one.Value[^1] < stale)
            .Select(one => one.Key)
            .ToArray())
        {
            _ = _sends.Remove(held);
        }

        var standing = new Dictionary<RestrictionKey, SendCounter>();

        foreach (RestrictionKey key in keys)
        {
            bool sent = _sends.TryGetValue(key, out List<DateTimeOffset>? times);
            bool granted = _credit.TryGetValue(key, out int credit);

            if (sent || granted)
            {
                standing[key] = new SendCounter(times ?? [], credit);
            }
        }

        return ValueTask.FromResult<IReadOnlyDictionary<RestrictionKey, SendCounter>>(standing);
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(
        byte[] reference,
        IReadOnlyCollection<SendCount> counted,
        IReadOnlyCollection<RestrictionKey> spent,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        foreach (SendCount count in counted)
        {
            if (!_sends.TryGetValue(count.Key, out List<DateTimeOffset>? times))
            {
                times = [];
                _sends[count.Key] = times;
            }

            times.RemoveAll(sent => sent <= at - count.Retain);
            times.Add(at);
        }

        foreach (RestrictionKey key in spent)
        {
            Spent.Add(key);

            if (_credit.TryGetValue(key, out int credit) && credit <= 1)
            {
                _credit.Remove(key);
            }
            else if (credit > 1)
            {
                _credit[key] = credit - 1;
            }
        }

        _records[Convert.ToHexString(reference)] = ([.. counted.Select(count => count.Key)], at);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<bool> ReleaseAsync(byte[] reference, CancellationToken cancellationToken)
    {
        string held = Convert.ToHexString(reference);

        if (!_records.TryGetValue(held, out (RestrictionKey[] Counted, DateTimeOffset At) record))
        {
            return ValueTask.FromResult(false);
        }

        foreach (RestrictionKey key in record.Counted)
        {
            if (!_sends.TryGetValue(key, out List<DateTimeOffset>? times))
            {
                continue;
            }

            times.Remove(record.At);

            if (times.Count == 0)
            {
                _sends.Remove(key);
            }
        }

        _records.Remove(held);

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask GrantAsync(RestrictionKey key, int credit, CancellationToken cancellationToken)
    {
        _credit[key] = (_credit.TryGetValue(key, out int standing) ? standing : 0) + credit;

        return ValueTask.CompletedTask;
    }
}
