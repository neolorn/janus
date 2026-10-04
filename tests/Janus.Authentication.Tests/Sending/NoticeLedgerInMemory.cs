using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// Where the addresses already told there is no account are remembered, holding the
/// plain address so a test can read which were told and when.
/// </summary>
internal sealed class NoticeLedgerInMemory : INoticeLedger
{
    /// <summary>
    /// Every notice recorded, in order.
    /// </summary>
    public List<(string Destination, DateTimeOffset At)> Told { get; } = [];

    /// <summary>
    /// Gets or sets what another transaction commits while this one waits for an
    /// address's notices, so a test may tell it under an ask about to be judged.
    /// </summary>
    public Action<string>? Holding { get; set; }

    /// <inheritdoc/>
    public ValueTask HoldAsync(string destination, CancellationToken cancellationToken)
    {
        Holding?.Invoke(destination);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<bool> WasToldAsync(
        string destination,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Told.Any(notice =>
            string.Equals(notice.Destination, destination, StringComparison.Ordinal)
            && notice.At > at - window));

    /// <inheritdoc/>
    public ValueTask MarkAsync(
        string destination,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        Told.Add((destination, at));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SweepAsync(DateTimeOffset now, TimeSpan window, CancellationToken cancellationToken)
    {
        TimeSpan kept = window > TimeSpan.FromHours(1) ? window : TimeSpan.FromHours(1);

        Told.RemoveAll(notice => notice.At < now - kept);

        return ValueTask.CompletedTask;
    }

    public ValueTask<int> SinceAsync(DateTimeOffset from, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Told.Count(notice => notice.At >= from));
}
