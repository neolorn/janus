using Janus.Core;

namespace Janus.Authentication.Sessions;

/// <summary>
/// Where a session was used from: the address it came from, what it was used with,
/// and the city the local database resolved.
/// </summary>
/// <param name="Address">The address the request came from.</param>
/// <param name="Device">What it was used from.</param>
/// <remarks>
/// Implements AUTH-SESS-001, AUTH-SESS-013 and INT-GEN-006. The location is not a
/// part a caller supplies: it is what <see cref="ILocationResolver"/> made of the
/// address when the session was recorded, or what the row carries when one is read
/// back.
/// </remarks>
internal sealed record SessionOrigin(string Address, DeviceDescription Device)
{
    /// <summary>
    /// Where it was, no finer than a city, absent where the local database could not
    /// say.
    /// </summary>
    public SessionLocation? Location { get; init; }
}
