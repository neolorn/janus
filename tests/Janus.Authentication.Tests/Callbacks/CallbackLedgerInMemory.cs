using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Callbacks;

namespace Janus.Authentication.Tests.Callbacks;

/// <summary>
/// Where inbound callbacks are counted, holding the source of each so a test can
/// read what was received and what was rejected.
/// </summary>
internal sealed class CallbackLedgerInMemory : ICallbackLedger
{
    /// <summary>
    /// Every callback counted, in order.
    /// </summary>
    public List<(string Source, DateTimeOffset At, bool Rejected)> Counted { get; } = [];

    /// <inheritdoc/>
    public ValueTask<int> ReceivedAsync(
        string source,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        DateTimeOffset opened = new(at.UtcTicks - (at.UtcTicks % window.Ticks), TimeSpan.Zero);

        Counted.Add((source, at, false));

        return ValueTask.FromResult(Counted.Count(callback =>
            string.Equals(callback.Source, source, StringComparison.Ordinal)
            && !callback.Rejected
            && callback.At >= opened));
    }

    /// <inheritdoc/>
    public ValueTask<int> RejectedAsync(
        string source,
        DateTimeOffset at,
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        Counted.Add((source, at, true));

        return ValueTask.FromResult(Counted.Count(callback =>
            string.Equals(callback.Source, source, StringComparison.Ordinal)
            && callback.Rejected
            && callback.At >= from));
    }
}
