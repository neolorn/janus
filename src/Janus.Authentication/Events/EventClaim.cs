using System;

namespace Janus.Authentication.Events;

/// <summary>
/// The claim one pass holds on one emitted event: the row, and the instant the claim
/// stands until, which names it among the claims the row has had.
/// </summary>
/// <param name="Event">What the row is held under.</param>
/// <param name="Until">When the claim times out, as the row carries it.</param>
/// <remarks>
/// Implements CONV-DESIGN-003 and INF-BG-001. The row is claimed whole, for every
/// consumer still to take it. A renewal, a consumer's take and the row's outcome are each
/// written only where the row still carries this instant, so a pass whose claim was taken
/// over writes nothing.
/// </remarks>
internal readonly record struct EventClaim(PendingEventId Event, DateTimeOffset Until);
