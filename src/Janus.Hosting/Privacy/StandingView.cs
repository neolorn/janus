using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Privacy;

/// <summary>
/// Where another person stands, as <c>GET /admin/accounts/{subject}/standing</c> answers
/// it: each consent and objection as the person's own read answers it.
/// </summary>
/// <param name="State">Where the account stands.</param>
/// <param name="Consents">The person's consent records.</param>
/// <param name="Objections">The person's objection records.</param>
/// <remarks>Implements IDN-READ-003 and chapter 09 section 8a.</remarks>
internal sealed record StandingView(
    AccountState State,
    IReadOnlyList<ConsentView> Consents,
    IReadOnlyList<ObjectionView> Objections)
{
    /// <summary>
    /// Reads the standing.
    /// </summary>
    /// <param name="standing">The standing.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The standing is absent.</exception>
    public static StandingView Of(SubjectStanding standing)
    {
        ArgumentNullException.ThrowIfNull(standing);

        return new StandingView(
            standing.State,
            [.. standing.Consents.Select(ConsentView.Of)],
            [.. standing.Objections.Select(ObjectionView.Of)]);
    }
}
