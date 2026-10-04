using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The sending ledger, holding what each key has been sent and what credit it has,
/// so a test can read exactly what a send counted and what a release gave back.
/// </summary>
internal sealed class SendLedgerInMemory : ISendLedger
{
    private readonly Dictionary<RestrictionKey, List<DateTimeOffset>> _sends = [];
    private readonly Dictionary<RestrictionKey, int> _credit = [];
    private readonly Dictionary<string, Counted> _records = [];

    /// <summary>
    /// The unit of work the operations under test run in. Where a test names it, what
    /// the ledger wrote inside a unit of work that rolled back is put back, and a
    /// judgement made outside one is a fault, as it is in the library.
    /// </summary>
    public UnitOfWorkInMemory? Work { get; set; }

    /// <summary>
    /// The keys the ledger holds a record for.
    /// </summary>
    public IReadOnlyCollection<RestrictionKey> Keys => _sends.Keys;

    /// <summary>
    /// The keys whose granted credit a send has spent, in order.
    /// </summary>
    public List<RestrictionKey> Spent { get; } = [];

    /// <summary>
    /// The keys whose counters a judgement held, in the order they were held.
    /// </summary>
    public List<RestrictionKey> Held { get; } = [];

    /// <summary>
    /// What one key has been sent.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The times, oldest first.</returns>
    public IReadOnlyList<DateTimeOffset> Sends(RestrictionKey key) =>
        _sends.TryGetValue(key, out List<DateTimeOffset>? sent) ? sent : [];

    /// <summary>
    /// The credit one key still has.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The unspent grants.</returns>
    public int Credit(RestrictionKey key) => _credit.TryGetValue(key, out int credit) ? credit : 0;

    /// <summary>
    /// Records times against a key without a send, which sets up a bucket a test
    /// wants already full.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="sends">The times.</param>
    public void Given(RestrictionKey key, params DateTimeOffset[] sends) =>
        _sends[key] = [.. sends];

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyDictionary<RestrictionKey, SendCounter>> HoldAsync(
        IReadOnlyCollection<RestrictionKey> keys,
        CounterStaleness stale,
        byte[]? setAside,
        CancellationToken cancellationToken)
    {
        if (Work is { Open: false })
        {
            throw new InvalidOperationException("The counters are held inside the caller's unit of work.");
        }

        await SweepAsync(stale, cancellationToken);

        Held.AddRange(keys);

        if (setAside is not null)
        {
            _ = await ReleaseAsync(setAside, cancellationToken);
        }

        var standing = new Dictionary<RestrictionKey, SendCounter>();

        foreach (RestrictionKey key in keys)
        {
            standing[key] = new SendCounter(
                _sends.TryGetValue(key, out List<DateTimeOffset>? times) ? [.. times] : [],
                Credit(key));
        }

        return standing;
    }

    /// <inheritdoc/>
    public ValueTask SweepAsync(CounterStaleness stale, CancellationToken cancellationToken)
    {
        Undoing();

        foreach (RestrictionKey held in _sends
            .Where(one => one.Value.Count == 0 || one.Value[one.Value.Count - 1] < Before(one.Key))
            .Select(one => one.Key)
            .ToArray())
        {
            _ = _sends.Remove(held);
        }

        return ValueTask.CompletedTask;

        DateTimeOffset Before(RestrictionKey key) =>
            key.Kind is RestrictionKeyKind.Destination ? stale.Destinations : stale.Keys;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(
        byte[] reference,
        IReadOnlyCollection<SendCount> counted,
        IReadOnlyCollection<RestrictionKey> spent,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Undoing();

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

        TimeSpan settles = counted.Select(count => count.Retain).DefaultIfEmpty(TimeSpan.Zero).Max();

        _records[Convert.ToHexString(reference)] =
            new Counted([.. counted.Select(count => count.Key)], [.. spent], at, at + settles);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SweepSettledAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        Undoing();

        foreach (string settled in _records
            .Where(record => record.Value.SettlesAt < now)
            .Select(record => record.Key)
            .ToArray())
        {
            _ = _records.Remove(settled);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<bool> HoldsAsync(byte[] reference, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_records.ContainsKey(Convert.ToHexString(reference)));

    /// <inheritdoc/>
    public ValueTask<bool> ReleaseAsync(byte[] reference, CancellationToken cancellationToken)
    {
        string held = Convert.ToHexString(reference);

        if (!_records.TryGetValue(held, out Counted? record))
        {
            return ValueTask.FromResult(false);
        }

        Undoing();

        foreach (RestrictionKey key in record.Keys)
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

        foreach (RestrictionKey key in record.Spent)
        {
            _credit[key] = Credit(key) + 1;
        }

        _records.Remove(held);

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask GrantAsync(RestrictionKey key, int credit, CancellationToken cancellationToken)
    {
        Undoing();

        _credit[key] = Credit(key) + credit;

        return ValueTask.CompletedTask;
    }

    private static void Restore<TKey, TValue>(Dictionary<TKey, TValue> held, Dictionary<TKey, TValue> was)
        where TKey : notnull
    {
        held.Clear();

        foreach (KeyValuePair<TKey, TValue> one in was)
        {
            held[one.Key] = one.Value;
        }
    }

    // What the ledger holds now is what a rollback of the unit of work in progress puts
    // back.
    private void Undoing()
    {
        if (Work is not { Open: true })
        {
            return;
        }

        var sends = _sends.ToDictionary(one => one.Key, one => one.Value.ToList());
        var credit = new Dictionary<RestrictionKey, int>(_credit);
        var records = new Dictionary<string, Counted>(_records);

        Work.Undoing(() =>
        {
            Restore(_sends, sends);
            Restore(_credit, credit);
            Restore(_records, records);
        });
    }

    // One send as the ledger counts it: the keys, the credit spent and the instants.
    private sealed record Counted(
        RestrictionKey[] Keys,
        RestrictionKey[] Spent,
        DateTimeOffset At,
        DateTimeOffset SettlesAt);
}
