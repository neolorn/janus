using Janus.Core;

namespace Janus.Authentication.Sessions;

/// <summary>
/// A session just created and the two values the browser is to carry for it. Neither
/// is returned twice and neither is read back: the row holds their fingerprints.
/// </summary>
/// <param name="Id">Which session.</param>
/// <param name="Secret">What the session cookie carries.</param>
/// <param name="CsrfToken">
/// What the script-readable cookie carries, which a state-changing request presents
/// back and which is bound to this session and no other.
/// </param>
/// <remarks>Implements AUTH-SESS-003, AUTH-SESS-006 and BFF-CSRF-001.</remarks>
internal sealed record IssuedSession(SessionId Id, OpaqueToken Secret, OpaqueToken CsrfToken);
