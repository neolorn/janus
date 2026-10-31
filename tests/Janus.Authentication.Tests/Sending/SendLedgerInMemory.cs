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

        Counted? retried = null;

        if (setAside is not null)
        {
            await SetAsideAsync(setAside, cancellationToken);
            _ = _records.TryGetValue(Convert.ToHexString(setAside), out retried);
        }

        var standing = new Dictionary<RestrictionKey, SendCounter>();

        foreach (RestrictionKey key in keys)
        {
            List<DateTimeOffset> counted = _sends.TryGetValue(key, out List<DateTimeOffset>? times) ? [.. times] : [];

            // What the send of a message judged again counted once is left out of what
            // it is judged on, and stands until the message is admitted or fails.
            if (retried is { ResendAt: DateTimeOffset once } && retried.Resend.Contains(key))
            {
                _ = counted.Remove(once);
            }

            standing[key] = new SendCounter(counted, Credit(key));
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
        IReadOnlyList<CountedMessage> messages,
        SendPlan once,
        bool secondFactor,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Undoing();

        // A message judged again stays of the send it was first counted under; at a
        // first admission the first message names the send.
        string first = Convert.ToHexString(messages[0].Reference);
        string send = _records.TryGetValue(first, out Counted? standing) ? standing.Send : first;
        string? answer = Of(send).Select(one => one.Value.Answer).FirstOrDefault(one => one is not null);

        TakeBackOnce(send);

        foreach (SendCount count in once.Counted)
        {
            Count(count, at);
        }

        foreach (RestrictionKey key in once.Spent)
        {
            Spend(key);
        }

        RestrictionKey[] resend = [.. once.Counted.Select(count => count.Key)];
        TimeSpan kept = once.Counted.Select(count => count.Retain).DefaultIfEmpty(TimeSpan.Zero).Max();

        foreach (CountedMessage message in messages)
        {
            foreach (SendCount count in message.Plan.Counted)
            {
                Count(count, at);
            }

            foreach (RestrictionKey key in message.Plan.Spent)
            {
                Spend(key);
            }

            TimeSpan settles = message.Plan.Counted.Select(count => count.Retain).Append(kept).Max();

            _records[Convert.ToHexString(message.Reference)] = new Counted(
                send,
                [.. message.Plan.Counted.Select(count => count.Key)],
                [.. message.Plan.Spent],
                at,
                at + settles,
                Admitted: true,
                resend,
                resend.Length > 0 ? at : null,
                secondFactor,
                answer,
                Released: false);
        }

        foreach ((string reference, Counted other) in Of(send))
        {
            _records[reference] = other with
            {
                Resend = resend,
                ResendAt = resend.Length > 0 ? at : null,
                SettlesAt = other.SettlesAt > at + kept ? other.SettlesAt : at + kept,
            };
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetAsideAsync(byte[] reference, CancellationToken cancellationToken)
    {
        string held = Convert.ToHexString(reference);

        if (!_records.TryGetValue(held, out Counted? record))
        {
            return ValueTask.CompletedTask;
        }

        Undoing();
        TakeBack(record);

        _records[held] = record with { Keys = [], Spent = [], Admitted = false };

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SweepSettledAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        Undoing();

        // A send is forgotten whole: a message of it stands while any other still could
        // be reported on.
        foreach (string settled in _records
            .Where(record => Of(record.Value.Send).All(one => one.Value.SettlesAt < now))
            .Select(record => record.Key)
            .ToArray())
        {
            _ = _records.Remove(settled);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<bool> HoldsAsync(byte[] reference, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            _records.TryGetValue(Convert.ToHexString(reference), out Counted? record) && record.Admitted);

    /// <inheritdoc/>
    public ValueTask<bool> ReleaseAsync(byte[] reference, CancellationToken cancellationToken)
    {
        string held = Convert.ToHexString(reference);

        if (!_records.TryGetValue(held, out Counted? record))
        {
            return ValueTask.FromResult(false);
        }

        // D-227: a message released already stays known until its send settles, and is
        // released once.
        if (record.Released)
        {
            return ValueTask.FromResult(true);
        }

        Undoing();
        TakeBack(record);

        // The last message of a send to fail for good takes what the send counted once
        // with it.
        if (Of(record.Send).Count(one => !one.Value.Released) == 1)
        {
            TakeBackOnce(record.Send);
        }

        _records[held] = _records[held] with { Keys = [], Spent = [], Admitted = false, Released = true };

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeliveredAsync(byte[] reference, CancellationToken cancellationToken)
    {
        if (!_records.TryGetValue(Convert.ToHexString(reference), out Counted? record))
        {
            return ValueTask.FromResult(false);
        }

        if (record.SecondFactor && !record.Released)
        {
            Undoing();
            TakeBackOnce(record.Send);
        }

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask BindAsync(byte[] reference, byte[] answer, CancellationToken cancellationToken)
    {
        Undoing();

        foreach ((string held, Counted record) in Of(Convert.ToHexString(reference)))
        {
            _records[held] = record with { Answer = Convert.ToHexString(answer) };
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask AnsweredAsync(byte[] answer, CancellationToken cancellationToken)
    {
        string presented = Convert.ToHexString(answer);

        Undoing();

        foreach (string send in _records.Values
            .Where(record => string.Equals(record.Answer, presented, StringComparison.Ordinal))
            .Select(record => record.Send)
            .Distinct(StringComparer.Ordinal)
            .ToArray())
        {
            TakeBackOnce(send);
        }

        return ValueTask.CompletedTask;
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

    // The messages counted under one send, each with the reference it answers to.
    private KeyValuePair<string, Counted>[] Of(string send) =>
        [.. _records.Where(record => string.Equals(record.Value.Send, send, StringComparison.Ordinal))];

    private void Count(SendCount count, DateTimeOffset at)
    {
        if (!_sends.TryGetValue(count.Key, out List<DateTimeOffset>? times))
        {
            times = [];
            _sends[count.Key] = times;
        }

        times.RemoveAll(sent => sent <= at - count.Retain);
        times.Add(at);
    }

    private void Spend(RestrictionKey key)
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

    private void Uncount(RestrictionKey key, DateTimeOffset at)
    {
        if (!_sends.TryGetValue(key, out List<DateTimeOffset>? times))
        {
            return;
        }

        times.Remove(at);

        if (times.Count == 0)
        {
            _sends.Remove(key);
        }
    }

    // One message taken back out of every bucket it counted against, and the credit it
    // spent given back.
    private void TakeBack(Counted record)
    {
        foreach (RestrictionKey key in record.Keys)
        {
            Uncount(key, record.At);
        }

        foreach (RestrictionKey key in record.Spent)
        {
            _credit[key] = Credit(key) + 1;
        }
    }

    // What a send counted once taken back, and no message of it holding it any longer.
    private void TakeBackOnce(string send)
    {
        KeyValuePair<string, Counted>[] messages = Of(send);

        if (messages.Select(one => one.Value).FirstOrDefault(one => one.ResendAt is not null) is not Counted counted
            || counted.ResendAt is not DateTimeOffset at)
        {
            return;
        }

        foreach (RestrictionKey key in counted.Resend)
        {
            Uncount(key, at);
        }

        foreach ((string reference, Counted record) in messages)
        {
            _records[reference] = record with { Resend = [], ResendAt = null };
        }
    }

    // One message as the ledger counts it: the send it is of, the keys, the credit
    // spent, the instants, whether it holds the count of an admission, what its send
    // counted once and when, whether a delivered report releases that, what the send
    // is answered by, and whether it failed for good (D-227).
    private sealed record Counted(
        string Send,
        RestrictionKey[] Keys,
        RestrictionKey[] Spent,
        DateTimeOffset At,
        DateTimeOffset SettlesAt,
        bool Admitted,
        RestrictionKey[] Resend,
        DateTimeOffset? ResendAt,
        bool SecondFactor,
        string? Answer,
        bool Released);
}
