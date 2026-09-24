using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The counts of records each person was given, and the daily means drawn from them,
/// held in memory.
/// </summary>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class ReadVolumeStoreInMemory : IReadVolumeStore
{
    private readonly Dictionary<(SubjectId Actor, DateOnly Day), long> _counted = [];

    private readonly Dictionary<SubjectId, decimal> _means = [];

    /// <summary>
    /// The counts held, by person and day.
    /// </summary>
    public IReadOnlyDictionary<(SubjectId Actor, DateOnly Day), long> Counted => _counted;

    /// <summary>
    /// Records a count for a day already past, as a person's earlier reading would have.
    /// </summary>
    /// <param name="actor">Who read.</param>
    /// <param name="day">The day they read on.</param>
    /// <param name="records">How many records they were given.</param>
    public void Read(SubjectId actor, DateOnly day, long records) => _counted[(actor, day)] = records;

    /// <inheritdoc/>
    public ValueTask<long> AddAsync(
        SubjectId actor,
        DateOnly day,
        int records,
        CancellationToken cancellationToken)
    {
        long counted = _counted.GetValueOrDefault((actor, day)) + records;

        _counted[(actor, day)] = counted;

        return ValueTask.FromResult(counted);
    }

    /// <inheritdoc/>
    public ValueTask<decimal> BaselineAsync(SubjectId actor, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_means.GetValueOrDefault(actor));

    /// <inheritdoc/>
    public ValueTask<int> RebaselineAsync(DateOnly today, int days, CancellationToken cancellationToken)
    {
        DateOnly oldest = today.AddDays(-days);

        foreach ((SubjectId Actor, DateOnly Day) expired in _counted.Keys.Where(key => key.Day < oldest).ToList())
        {
            _ = _counted.Remove(expired);
        }

        _means.Clear();

        foreach (IGrouping<SubjectId, KeyValuePair<(SubjectId Actor, DateOnly Day), long>> actor in _counted
            .Where(held => held.Key.Day < today)
            .GroupBy(held => held.Key.Actor))
        {
            _means[actor.Key] = actor.Sum(held => held.Value) / (decimal)days;
        }

        return ValueTask.FromResult(_means.Count);
    }
}
