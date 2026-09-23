using System;

namespace Janus.Core;

/// <summary>
/// What an authenticator answered a sign-in challenge with, exactly as the browser
/// received it.
/// </summary>
/// <param name="CredentialId">Which credential answered, base64url.</param>
/// <param name="ClientDataJson">
/// What the browser signed over: the ceremony type, the challenge and the origin,
/// base64url.
/// </param>
/// <param name="AuthenticatorData">
/// What the authenticator asserted: the relying party hash, the flags and the
/// counter, base64url.
/// </param>
/// <param name="Signature">The signature over the two, base64url.</param>
/// <param name="UserHandle">
/// The handle the authenticator returned, base64url, where it keeps one: a
/// discoverable credential carries the account it belongs to, a second-factor
/// security key carries nothing (REG-PM-001).
/// </param>
/// <remarks>
/// Implements AUTH-FACT-011 and AUTH-FACT-014. The library verifies the signature
/// itself; nothing a caller asserts about the ceremony is taken on trust.
/// </remarks>
public sealed record AuthenticatorAssertion(
    string CredentialId,
    string ClientDataJson,
    string AuthenticatorData,
    string Signature,
    string? UserHandle = null);
