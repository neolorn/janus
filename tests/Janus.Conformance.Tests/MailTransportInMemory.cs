using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Conformance.Tests;

/// <summary>
/// The sample host's mail transport, which takes every message into memory.
/// </summary>
internal sealed class MailTransportInMemory : IMailTransport
{
    /// <summary>
    /// What was taken, in the order it was taken.
    /// </summary>
    public ConcurrentQueue<MailMessage> Taken { get; } = new();

    /// <inheritdoc/>
    public ValueTask<Result> SendAsync(MailMessage mail, CancellationToken cancellationToken)
    {
        Taken.Enqueue(mail);

        return ValueTask.FromResult(Result.Success());
    }
}
