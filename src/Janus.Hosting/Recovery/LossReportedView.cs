using System;
using Janus.Core;

namespace Janus.Hosting.Recovery;

/// <summary>
/// What an accepted loss report tells the person: the credential is refused already,
/// and this is when it goes.
/// </summary>
/// <param name="InvalidatesAt">When the window ends.</param>
/// <remarks>Implements AUTH-RECOV-007 and D-141.</remarks>
internal sealed record LossReportedView(DateTimeOffset InvalidatesAt)
{
    /// <summary>
    /// Reads a report.
    /// </summary>
    /// <param name="reported">What the report left behind.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The report is absent.</exception>
    public static LossReportedView Of(LossReported reported)
    {
        ArgumentNullException.ThrowIfNull(reported);

        return new LossReportedView(reported.InvalidatesAt);
    }
}
