using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// Where the gateway balance readings are kept.
/// </summary>
internal sealed class SmsBalanceLedgerInMemory : ISmsBalanceLedger
{
    private readonly List<BalanceReading> _readings = [];

    /// <summary>
    /// Every reading kept, oldest first.
    /// </summary>
    public IReadOnlyList<BalanceReading> Readings => _readings;

    /// <summary>
    /// Records readings without polling the gateway, which sets up a history a test
    /// wants to measure a drain against.
    /// </summary>
    /// <param name="readings">The readings.</param>
    public void Given(params BalanceReading[] readings) => _readings.AddRange(readings);

    /// <inheritdoc/>
    public ValueTask RecordAsync(
        BalanceReading reading,
        TimeSpan retain,
        CancellationToken cancellationToken)
    {
        _readings.RemoveAll(kept => kept.At < reading.At - retain);
        _readings.Add(reading);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<BalanceReading>> SinceAsync(
        DateTimeOffset from,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<BalanceReading>>(
            [.. _readings.Where(reading => reading.At >= from).OrderBy(reading => reading.At)]);
}
