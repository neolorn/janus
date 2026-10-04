using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// What attempts a message once its transaction has committed, keeping which messages
/// it was asked to attempt. How a message is carried is tested where the publisher is.
/// </summary>
internal sealed class SendCarrierInMemory : ISendCarrier
{
    /// <summary>
    /// Every message an attempt was asked for, in order.
    /// </summary>
    public List<SendDeliveryId> Attempted { get; } = [];

    /// <inheritdoc/>
    public ValueTask AttemptAsync(SendDeliveryId delivery, CancellationToken cancellationToken)
    {
        Attempted.Add(delivery);

        return ValueTask.CompletedTask;
    }
}
