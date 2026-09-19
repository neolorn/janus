using System;

namespace Janus.Authentication.Factors;

/// <summary>
/// What a completed creation ceremony produced, verified by the caller and judged
/// against the relying party here.
/// </summary>
/// <param name="CredentialId">The identifier the authenticator returned.</param>
/// <param name="PublicKey">The key a later signature is verified against.</param>
/// <param name="Algorithm">The COSE algorithm the key signs with.</param>
/// <param name="RelyingPartyId">What the ceremony ran under.</param>
/// <param name="UserVerified">Whether the person proved themselves to the authenticator.</param>
/// <param name="BackupEligible">Whether the credential may be synced.</param>
/// <param name="BackupState">Whether it currently is.</param>
/// <param name="Counter">
/// The signature counter the authenticator reported, nought where it keeps none.
/// </param>
/// <remarks>Implements AUTH-FACT-013 and AUTH-FACT-014.</remarks>
internal sealed record WebAuthnRegistration(
    ReadOnlyMemory<byte> CredentialId,
    ReadOnlyMemory<byte> PublicKey,
    int Algorithm,
    string RelyingPartyId,
    bool UserVerified,
    bool BackupEligible,
    bool BackupState,
    uint Counter);
