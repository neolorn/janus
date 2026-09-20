using System;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// One session the account holds, as the account application lists it.
/// </summary>
/// <param name="Id">What the ending endpoint names it by.</param>
/// <param name="SignedInAt">When it began.</param>
/// <param name="LastUsedAt">When it was last used.</param>
/// <param name="Device">What it was used from.</param>
/// <param name="Location">Where from, no finer than a city, where that is known.</param>
/// <param name="Current">Whether it is the one making this request.</param>
/// <remarks>Implements AUTH-SESS-013 and REG-ACCT-001.</remarks>
internal sealed record SessionView(
    string Id,
    DateTimeOffset SignedInAt,
    DateTimeOffset LastUsedAt,
    DeviceDescription Device,
    SessionLocation? Location,
    bool Current)
{
    /// <summary>
    /// Reads one session.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The session is absent.</exception>
    public static SessionView Of(SessionSummary session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new SessionView(
            session.Id.ToString(),
            session.SignedInAt,
            session.LastUsedAt,
            session.Device,
            session.Location,
            session.Current);
    }
}
