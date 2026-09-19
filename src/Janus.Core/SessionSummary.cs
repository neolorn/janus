using System;

namespace Janus.Core;

/// <summary>
/// One live session as the account sees it.
/// </summary>
/// <param name="Id">Which session, so that it can be ended on its own.</param>
/// <param name="SignedInAt">When it began.</param>
/// <param name="LastUsedAt">When it was last used.</param>
/// <param name="Device">What it was used from.</param>
/// <param name="Location">
/// Where it was last used, no finer than a city, absent where the local database
/// could not say.
/// </param>
/// <param name="Current">Whether it is the session asking.</param>
/// <remarks>Implements AUTH-SESS-013.</remarks>
public sealed record SessionSummary(
    SessionId Id,
    DateTimeOffset SignedInAt,
    DateTimeOffset LastUsedAt,
    DeviceDescription Device,
    SessionLocation? Location,
    bool Current);
