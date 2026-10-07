using System;

namespace Janus.Core;

/// <summary>
/// What an authenticator answered a sign-in challenge with: the members of the
/// <c>AuthenticationResponseJSON</c> that <c>PublicKeyCredential.toJSON()</c> gives
/// (WebAuthn Level 3 section 5.1), exactly as the browser wrote them.
/// </summary>
/// <param name="Id">Which credential answered, base64url.</param>
/// <param name="RawId">The same identifier as the browser's <c>rawId</c>, base64url.</param>
/// <param name="Type">The credential type, <c>public-key</c>.</param>
/// <param name="ClientDataJson">
/// The response's <c>clientDataJSON</c>, what the browser signed over: the ceremony
/// type, the challenge and the origin, base64url.
/// </param>
/// <param name="AuthenticatorData">
/// The response's <c>authenticatorData</c>, what the authenticator asserted: the
/// relying party hash, the flags and the counter, base64url.
/// </param>
/// <param name="Signature">The response's <c>signature</c> over the two, base64url.</param>
/// <param name="UserHandle">
/// The response's <c>userHandle</c>, base64url, where the authenticator keeps one: a
/// discoverable credential carries the account it belongs to, a second-factor
/// security key carries nothing (REG-PM-001).
/// </param>
/// <remarks>
/// Implements AUTH-FACT-011 and AUTH-FACT-014. The library verifies the assertion
/// itself; nothing a caller asserts about the ceremony is taken on trust. The
/// extension outputs a browser adds are no member of it: the library asks for none
/// and reads none (D-197).
/// </remarks>
public sealed record AuthenticatorAssertion(
    string Id,
    string RawId,
    string Type,
    string ClientDataJson,
    string AuthenticatorData,
    string Signature,
    string? UserHandle = null);
