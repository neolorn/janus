using System;

namespace Janus.Privacy.Outbox;

/// <summary>
/// The claim one pass holds on one outbox row: the row, and the instant the claim
/// stands until, which names it among the claims the row has had.
/// </summary>
/// <param name="Delivery">What the row is held under.</param>
/// <param name="Until">When the claim times out, as the row carries it.</param>
/// <remarks>
/// Implements CONV-DESIGN-003, IDN-LIFE-003a and INF-BG-001. The row is claimed whole,
/// for every subscriber still to confirm it, the erasure ledger's line among them. A
/// renewal, a confirmation and the row's outcome are each written only where the row
/// still carries this instant, so a pass whose claim was taken over writes nothing.
/// </remarks>
internal readonly record struct DeliveryClaim(DeliveryId Delivery, DateTimeOffset Until);
