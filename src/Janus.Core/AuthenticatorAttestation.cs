using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What an authenticator answered an enrolment ceremony with: the members of the
/// <c>RegistrationResponseJSON</c> that <c>PublicKeyCredential.toJSON()</c> gives
/// (WebAuthn Level 3 section 5.1), exactly as the browser wrote them.
/// </summary>
/// <param name="Id">The credential created, base64url.</param>
/// <param name="RawId">The same identifier as the browser's <c>rawId</c>, base64url.</param>
/// <param name="Type">The credential type, <c>public-key</c>.</param>
/// <param name="ClientDataJson">
/// The response's <c>clientDataJSON</c>, what the browser signed over: the ceremony
/// type, the challenge and the origin, base64url.
/// </param>
/// <param name="AttestationObject">
/// The response's <c>attestationObject</c>: the authenticator data, which carries the
/// credential's identifier and public key, and the attestation statement, base64url.
/// </param>
/// <param name="Transports">
/// The response's <c>transports</c>, the ways the authenticator can be reached, as the
/// browser named them; empty where it named none.
/// </param>
/// <remarks>
/// Implements AUTH-FACT-013 and AUTH-FACT-014. No attestation is required: the
/// statement of a format the verification knows is checked and chains to nothing. The
/// extension outputs a browser adds are no member of it: the library asks for none
/// and reads none (D-197).
/// </remarks>
public sealed record AuthenticatorAttestation(
    string Id,
    string RawId,
    string Type,
    string ClientDataJson,
    string AttestationObject,
    IReadOnlyList<string> Transports);
