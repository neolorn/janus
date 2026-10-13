using System;

namespace Janus.Authentication.Sending;

/// <summary>
/// The claim one attempt holds on one admitted message: the row, and the instant the
/// claim stands until, which names it among the claims the row has had.
/// </summary>
/// <param name="Delivery">What the row is held under.</param>
/// <param name="Until">When the claim times out, as the row carries it.</param>
/// <remarks>
/// Implements CONV-DESIGN-003 and INF-BG-001. An outcome is written only where the row
/// still carries this instant, so an attempt whose claim was taken over writes nothing.
/// </remarks>
internal readonly record struct SendClaim(SendDeliveryId Delivery, DateTimeOffset Until);
