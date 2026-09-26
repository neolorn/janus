using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Conformance.Tests;

/// <summary>
/// The sample host's SMS transport, which takes every message into memory, holds a
/// balance above any floor, and reads no delivery report.
/// </summary>
internal sealed class SmsTransportInMemory : ISmsTransport
{
    /// <summary>
    /// What was taken, in the order it was taken.
    /// </summary>
    public ConcurrentQueue<SmsMessage> Taken { get; } = new();

    /// <inheritdoc/>
    public ValueTask<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        Taken.Enqueue(message);

        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public ValueTask<Result<decimal>> BalanceAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(decimal.MaxValue));

    /// <inheritdoc/>
    public Result<SmsDeliveryReport> ReadReport(IReadOnlyDictionary<string, string> parameters) =>
        Result.Failure<SmsDeliveryReport>(Error.From(ErrorCodes.CallbackRejected));
}
