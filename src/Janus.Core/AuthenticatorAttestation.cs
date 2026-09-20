using System;

namespace Janus.Core;

/// <summary>
/// What an authenticator answered an enrolment ceremony with, exactly as the browser
/// received it.
/// </summary>
/// <param name="CredentialId">The credential created, base64url.</param>
/// <param name="ClientDataJson">
/// What the browser signed over: the ceremony type, the challenge and the origin,
/// base64url.
/// </param>
/// <param name="AuthenticatorData">
/// What the authenticator asserted: the relying party hash, the flags and the
/// counter, base64url.
/// </param>
/// <param name="PublicKey">
/// The credential's public key in subject public key info form, base64url, which is
/// what the browser hands over for a ceremony that asks for no attestation.
/// </param>
/// <param name="Algorithm">The COSE identifier of the algorithm it signs with.</param>
/// <remarks>
/// Implements AUTH-FACT-013 and AUTH-FACT-014. No attestation statement is asked for
/// or read: an unattested credential enrols, so the key and the authenticator data
/// are the whole of what an enrolment needs.
/// </remarks>
public sealed record AuthenticatorAttestation(
    string CredentialId,
    string ClientDataJson,
    string AuthenticatorData,
    string PublicKey,
    int Algorithm);
