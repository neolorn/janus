using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Testing;

/// <summary>
/// An SMS transport that keeps every message it takes, readable by destination, fails a
/// delivery only where the test says so, holds a balance no floor reaches, and is sent
/// no delivery report.
/// </summary>
/// <remarks>
/// Implements LIB-TEST-003. A delivery that fails is not kept, and the library retries
/// it as it retries any transport's failure.
/// </remarks>
public sealed class InMemorySmsTransport : ISmsTransport
{
    private readonly ConcurrentQueue<SmsMessage> _sent = new();

    private readonly ConcurrentDictionary<PhoneNumber, bool> _failing = new();

    /// <summary>
    /// Every message taken for a destination, in the order it was taken.
    /// </summary>
    /// <param name="destination">The number the messages were sent to.</param>
    /// <returns>The messages.</returns>
    public IReadOnlyList<SmsMessage> SentTo(PhoneNumber destination) =>
        [.. _sent.Where(message => message.Destination == destination)];

    /// <summary>
    /// Fails every delivery to a destination from now until <see cref="AcceptDeliveriesTo"/>.
    /// </summary>
    /// <param name="destination">The number whose deliveries fail.</param>
    public void FailDeliveriesTo(PhoneNumber destination) => _failing[destination] = true;

    /// <summary>
    /// Takes deliveries to a destination again.
    /// </summary>
    /// <param name="destination">The number whose deliveries are taken.</param>
    public void AcceptDeliveriesTo(PhoneNumber destination) => _failing.TryRemove(destination, out _);

    /// <inheritdoc/>
    public ValueTask<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (_failing.ContainsKey(message.Destination))
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.SystemFault)));
        }

        _sent.Enqueue(message);

        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public ValueTask<Result<decimal>> BalanceAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(decimal.MaxValue));

    /// <inheritdoc/>
    public Result<SmsDeliveryReport> ReadReport(IReadOnlyDictionary<string, string> parameters) =>
        Result.Failure<SmsDeliveryReport>(Error.From(ErrorCodes.CallbackRejected));
}
