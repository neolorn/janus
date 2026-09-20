using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The text-message transport, keeping every payload it was handed and answering
/// with whatever balance a test gave the gateway.
/// </summary>
internal sealed class SmsTransportInMemory : ISmsTransport
{
    /// <summary>
    /// Every message handed over, in order.
    /// </summary>
    public List<SmsMessage> Taken { get; } = [];

    /// <summary>
    /// Whether the transport takes what it is handed.
    /// </summary>
    public bool Accepts { get; set; } = true;

    /// <summary>
    /// What the gateway says is left on the account.
    /// </summary>
    public decimal Balance { get; set; } = 1000m;

    /// <summary>
    /// How many times the balance was read.
    /// </summary>
    public int Reads { get; private set; }

    /// <inheritdoc/>
    public ValueTask<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        if (!Accepts)
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.SystemFault)));
        }

        Taken.Add(message);

        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public ValueTask<Result<decimal>> BalanceAsync(CancellationToken cancellationToken)
    {
        Reads++;

        return ValueTask.FromResult(Result.Success(Balance));
    }
}
