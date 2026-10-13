using System;
using System.Collections.Generic;

namespace Janus.Authentication.Factors;

/// <summary>
/// What a WebAuthn credential proves itself with, and what is recorded about the
/// authenticator that holds it.
/// </summary>
/// <param name="CredentialId">The identifier the attested credential data carried.</param>
/// <param name="PublicKey">
/// The key a signature is verified against, as the attested credential data carried
/// it: a COSE key.
/// </param>
/// <param name="Algorithm">The COSE algorithm the key signs with.</param>
/// <param name="RelyingPartyId">
/// The relying party identifier in force when it was enrolled, recorded so that a
/// later configuration change is detectable without comparing configuration to
/// credential at every authentication (AUTH-FACT-011).
/// </param>
/// <param name="Counter">
/// The signature counter the authenticator last reported, and nothing where it
/// supplies none. A counter that does not advance indicates a cloned credential.
/// </param>
/// <param name="BackupEligible">Whether the credential may be synced.</param>
/// <param name="BackupState">Whether it currently is, as its last assertion said.</param>
/// <param name="Transports">
/// The ways the authenticator can be reached, as the enrolment's response named them,
/// which is what a request naming the credential passes back to the browser.
/// </param>
/// <remarks>Implements AUTH-FACT-011, AUTH-FACT-013 and AUTH-FACT-014.</remarks>
internal sealed record WebAuthnMaterial(
    ReadOnlyMemory<byte> CredentialId,
    ReadOnlyMemory<byte> PublicKey,
    int Algorithm,
    string RelyingPartyId,
    uint? Counter,
    bool BackupEligible,
    bool BackupState,
    IReadOnlyList<string> Transports);
