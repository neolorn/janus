using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The messages undertaken and not yet carried, so a test can read what was written
/// before a transport was asked and what was left behind when one refused.
/// </summary>
internal sealed class SendOutboxInMemory : ISendOutbox
{
    private readonly Dictionary<SendDeliveryId, SendDelivery> _held = [];

    /// <summary>
    /// What is still waiting, oldest first.
    /// </summary>
    public IReadOnlyList<SendDelivery> Waiting =>
        [.. _held.Values.OrderBy(delivery => delivery.RecordedAt)];

    /// <summary>
    /// Every message ever written, in order, whether or not it is still waiting.
    /// </summary>
    public List<SendDelivery> Written { get; } = [];

    /// <inheritdoc/>
    public ValueTask AddAsync(SendDelivery delivery, CancellationToken cancellationToken)
    {
        _held[delivery!.Id] = delivery;
        Written.Add(delivery);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<SendDelivery?> FindAsync(
        SendDeliveryId delivery,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_held.TryGetValue(delivery, out SendDelivery? held) ? held : null);

    /// <inheritdoc/>
    public ValueTask RemoveAsync(SendDeliveryId delivery, CancellationToken cancellationToken)
    {
        _ = _held.Remove(delivery);

        return ValueTask.CompletedTask;
    }
}
