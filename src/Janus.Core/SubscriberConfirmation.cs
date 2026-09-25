using System;

namespace Janus.Core;

/// <summary>
/// How far one registered subscriber has got with one delivery.
/// </summary>
/// <param name="Name">What the subscriber is called.</param>
/// <param name="Required">Whether the delivery stays open until it confirms.</param>
/// <param name="ConfirmedAt">When it confirmed, or nothing while it has not.</param>
/// <remarks>
/// Implements IDN-LIFE-003a and IDN-LIFE-003b. This is the per-subscriber completion an
/// operator reads to know what is still outstanding and whose it is.
/// </remarks>
public sealed record SubscriberConfirmation(
    string Name,
    bool Required,
    DateTimeOffset? ConfirmedAt);
