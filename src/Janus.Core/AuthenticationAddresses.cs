namespace Janus.Core;

/// <summary>
/// Where a browser that holds no session is to be sent to obtain one.
/// </summary>
/// <param name="SignIn">The authentication application's route that signs a person in.</param>
/// <remarks>
/// Implements AUTH-SESS-012, API-LAND-001 and LIB-HOST-003. The authorization endpoint
/// forwards a browser and never renders a page, so the route it forwards to is the
/// frontend's arrangement and the host declares it; nothing here assumes a path.
/// </remarks>
public sealed record AuthenticationAddresses(string SignIn)
{
    /// <summary>
    /// What a deployment that has declared none holds, which makes the authorization
    /// endpoint refuse a browser holding no session rather than forward it to a route
    /// that is not there.
    /// </summary>
    public static AuthenticationAddresses None { get; } = new(string.Empty);
}
