using Janus.Core;

namespace Janus.Authentication.Sessions;

/// <summary>
/// A pre-authentication session just issued and the two values the browser is to
/// carry for it. Neither is returned twice and neither is read back: the row holds
/// their fingerprints.
/// </summary>
/// <param name="Secret">What the pre-authentication cookie carries.</param>
/// <param name="CsrfToken">
/// What the script-readable cookie carries, which a state-changing request presents
/// back and which is bound to this browser and no other.
/// </param>
/// <remarks>Implements BFF-CSRF-005a and BFF-CSRF-006.</remarks>
internal sealed record IssuedPreAuthentication(OpaqueToken Secret, OpaqueToken CsrfToken);
