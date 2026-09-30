namespace Janus.Core;

/// <summary>
/// Where a link the library sends lands: the origin of the authentication application
/// and the origin of the account application.
/// </summary>
/// <param name="Authentication">
/// The authentication application's origin, an absolute <c>https</c> origin, which is
/// the origin of the sign-in address too.
/// </param>
/// <param name="Account">The account application's origin, an absolute <c>https</c> origin.</param>
/// <remarks>
/// Implements LIB-HOST-001, API-LAND-001 and FE-VER-001. Every link is
/// <c>&lt;origin&gt;/link#&lt;kind&gt;.&lt;token&gt;</c>, the kind choosing the
/// application, so the token travels in the fragment and reaches no server in the
/// address. There is no default for either: the library knows no origin of the
/// frontend, and a link that names none opens nothing.
/// </remarks>
public sealed record LandingOrigins(string Authentication, string Account);
