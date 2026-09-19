using System;

namespace Janus.Authentication.Factors;

/// <summary>
/// What a WebAuthn credential proves itself with, and what is recorded about the
/// authenticator that holds it.
/// </summary>
/// <param name="CredentialId">The identifier the authenticator returned.</param>
/// <param name="PublicKey">The key a signature is verified against.</param>
/// <param name="Algorithm">The COSE algorithm the key signs with.</param>
/// <param name="RelyingPartyId">
/// The relying party identifier in force when it was enrolled, recorded so that a
/// later configuration change is detectable without comparing configuration to
/// credential at every authentication (AUTH-FACT-011).
/// </param>
/// <param name="Counter">
/// The signature counter the authenticator last reported, and nothing where it
/// supplies none. A counter moving backwards indicates a cloned credential.
/// </param>
/// <param name="BackupEligible">Whether the credential may be synced.</param>
/// <param name="BackupState">Whether it currently is.</param>
/// <remarks>Implements AUTH-FACT-011, AUTH-FACT-013 and AUTH-FACT-014.</remarks>
internal sealed record WebAuthnMaterial(
    ReadOnlyMemory<byte> CredentialId,
    ReadOnlyMemory<byte> PublicKey,
    int Algorithm,
    string RelyingPartyId,
    uint? Counter,
    bool BackupEligible,
    bool BackupState);
