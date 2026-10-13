using System.Collections.Generic;

namespace Janus.Authentication.Sending;

/// <summary>
/// What one admitted message counts against and what credit it spends, as the named
/// restrictions judged it.
/// </summary>
/// <param name="Counted">The keys the message counts against from its admission.</param>
/// <param name="Spent">The keys whose granted credit carried the message through.</param>
/// <remarks>Implements AUTH-ABUSE-004.</remarks>
internal sealed record SendPlan(
    IReadOnlyList<SendCount> Counted,
    IReadOnlyList<RestrictionKey> Spent);
