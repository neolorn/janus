namespace Janus.Core;

/// <summary>
/// Where a browser that holds no session is to be sent to obtain one.
/// </summary>
/// <param name="SignIn">The authentication application's route that signs a person in.</param>
/// <remarks>
/// Implements AUTH-SESS-012, API-LAND-001, LIB-HOST-001 and LIB-HOST-003. The
/// authorization endpoint forwards a browser and never renders a page, so the route it
/// forwards to is the frontend's arrangement and the host declares it; nothing here
/// assumes a path. There is no default: a deployment that registers none does not
/// start, because an interactive request it could not forward is one AUTH-SESS-012 AC3
/// has no answer for.
/// </remarks>
public sealed record AuthenticationAddresses(string SignIn);
