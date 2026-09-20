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

    /// <inheritdoc/>
    public ValueTask<bool> FirstAsync(
        string destination,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        if (Told.Any(notice =>
            string.Equals(notice.Destination, destination, StringComparison.Ordinal)
            && notice.At > at - window))
        {
            return ValueTask.FromResult(false);
        }

        Told.Add((destination, at));

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<int> SinceAsync(DateTimeOffset from, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Told.Count(notice => notice.At >= from));
}
