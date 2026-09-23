namespace Janus.Core;

/// <summary>
/// Where the authentication application is: the route a browser holding no session is
/// sent to, and the address the library is mounted at beside it.
/// </summary>
/// <param name="SignIn">The authentication application's route that signs a person in.</param>
/// <param name="Provider">
/// The address the library is mounted at on the authentication application, which is
/// where its OpenID Connect endpoints answer (BFF-SESS-006).
/// </param>
/// <remarks>
/// Implements AUTH-SESS-012, BFF-SESS-006, API-LAND-001, LIB-HOST-001 and
/// LIB-HOST-003. The authorization endpoint forwards a browser and never renders a
/// page, so the route it forwards to is the frontend's arrangement and the host
/// declares it; nothing here assumes a path. There is no default for either: a
/// deployment that registers none does not start, because an interactive request it
/// could not forward is one AUTH-SESS-012 AC3 has no answer for, and an application
/// that does not know where the provider is cannot establish a session at all.
/// </remarks>
public sealed record AuthenticationAddresses(string SignIn, string Provider);
