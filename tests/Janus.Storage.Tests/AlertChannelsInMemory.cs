using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;

namespace Janus.Storage.Tests;

/// <summary>
/// The alert channels as a test reads them, keeping each condition raised.
/// </summary>
internal sealed class AlertChannelsInMemory : IAlertChannels
{
    /// <summary>
    /// Every condition raised, in order.
    /// </summary>
    public ConcurrentQueue<AlertRaised> Raised { get; } = new();

    /// <inheritdoc/>
    public ValueTask<Result> RaiseAsync(AlertRaised raised, CancellationToken cancellationToken)
    {
        Raised.Enqueue(raised);

        return ValueTask.FromResult(Result.Success());
    }
}
