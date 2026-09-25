using System;
using System.Collections.Generic;

namespace Janus.Privacy.Outbox;

/// <summary>
/// A delivery as an operator reads it: the delivery, and when each subscriber that
/// confirmed it did so.
/// </summary>
/// <param name="Delivery">The delivery.</param>
/// <param name="ConfirmedAt">Each confirming subscriber, by name, and when it confirmed.</param>
/// <remarks>Implements IDN-LIFE-003a and IDN-LIFE-003b.</remarks>
internal sealed record DeliveryProgress(
    Delivery Delivery,
    IReadOnlyDictionary<string, DateTimeOffset> ConfirmedAt);
