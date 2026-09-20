using System;
using System.Collections.Generic;

namespace Janus.Authentication.Sending;

/// <summary>
/// What the named restrictions decided about one send: what it counts against if it
/// goes, what credit it spends, and when it could be tried again if it does not.
/// </summary>
/// <param name="Counted">The keys the send counts against once the transport takes it.</param>
/// <param name="Spent">The keys whose granted credit carried the send through.</param>
/// <param name="RetryAt">
/// The earliest time an exceeded bucket lifts, or nothing where the send passes.
/// </param>
/// <remarks>Implements AUTH-ABUSE-004.</remarks>
internal sealed record SendPlan(
    IReadOnlyList<SendCount> Counted,
    IReadOnlyList<RestrictionKey> Spent,
    DateTimeOffset? RetryAt);
