using Janus.Core;

namespace Janus.Authentication.Sessions;

/// <summary>
/// Where a session was used from: the address it came from, what it was used with,
/// and the city the local database resolved.
/// </summary>
/// <param name="Address">The address the request came from.</param>
/// <param name="Device">What it was used from.</param>
/// <param name="Location">
/// Where it was, no finer than a city, absent where the local database could not say.
/// </param>
/// <remarks>Implements AUTH-SESS-001 and AUTH-SESS-013.</remarks>
internal sealed record SessionOrigin(
    string Address,
    DeviceDescription Device,
    SessionLocation? Location);
