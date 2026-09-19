using Janus.Core;

namespace Janus.Authentication.Sessions;

/// <summary>
/// A session just created and the secret the browser is to carry for it. The secret
/// is returned once and never read back: the row holds its fingerprint.
/// </summary>
/// <param name="Id">Which session.</param>
/// <param name="Secret">What the cookie carries.</param>
/// <remarks>Implements AUTH-SESS-003 and AUTH-SESS-006.</remarks>
internal sealed record IssuedSession(SessionId Id, OpaqueToken Secret);
