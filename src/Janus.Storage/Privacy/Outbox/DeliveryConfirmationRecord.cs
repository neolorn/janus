using System;
using Janus.Privacy.Outbox;

namespace Janus.Storage.Privacy.Outbox;

/// <summary>
/// The <c>outbox_confirmations</c> row: one subscriber saying it has done its own
/// half of one delivery.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-003a. One row per subscriber per delivery, so a redelivery
/// after a successful attempt confirms nothing twice.
/// </remarks>
internal sealed class DeliveryConfirmationRecord
{
    /// <summary>
    /// The <c>delivery</c> column, the first half of this table's key.
    /// </summary>
    public DeliveryId Delivery { get; set; }

    /// <summary>
    /// The <c>subscriber</c> column, the second half of this table's key.
    /// </summary>
    public string Subscriber { get; set; } = string.Empty;

    /// <summary>
    /// The <c>confirmed_at</c> column.
    /// </summary>
    public DateTimeOffset ConfirmedAt { get; set; }
}
