using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// Where a delivery of host-side work has got, an erasure's, a takedown's, a
/// restriction change's, a consent change's or an objection change's: each subscriber
/// or purpose handler it is owed to and whether it confirmed.
/// </summary>
/// <param name="Id">What the delivery is held under, whatever its kind.</param>
/// <param name="Kind">The kind of host-side work it carries.</param>
/// <param name="Subject">Whose it is.</param>
/// <param name="Purpose">The purpose a consent or an objection delivery concerns, and nothing for another kind.</param>
/// <param name="Change">The change its event announces, and nothing for an erasure or a takedown.</param>
/// <param name="Reason">Why an erasure ran, and nothing for another kind.</param>
/// <param name="Status">How far the subscribers or handlers have got.</param>
/// <param name="Attempts">How many delivery attempts have been made across them.</param>
/// <param name="Subscribers">Each subscriber or purpose handler it is owed to and whether it confirmed.</param>
/// <remarks>
/// Implements IDN-LIFE-003a and IDN-LIFE-003b (D-203). The library's own steps
/// committed with the change, so what is read here is only what the hosts still owe.
/// </remarks>
public sealed record ErasureProgress(
    ErasureId Id,
    DeliveryKind Kind,
    SubjectId Subject,
    string? Purpose,
    DeliveryChange? Change,
    ErasureReason? Reason,
    ErasureStatus Status,
    int Attempts,
    IReadOnlyList<SubscriberConfirmation> Subscribers);
