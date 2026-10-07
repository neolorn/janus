using System;

namespace Janus.Authentication.Factors;

/// <summary>
/// What a completed assertion ceremony produced, verified by the caller and judged
/// against the stored credential here.
/// </summary>
/// <param name="CredentialId">Which credential answered.</param>
/// <param name="RelyingPartyId">What the ceremony ran under.</param>
/// <param name="UserVerified">Whether the person proved themselves to the authenticator.</param>
/// <param name="Counter">
/// The signature counter the authenticator reported, nought where it keeps none.
/// </param>
/// <param name="BackupEligible">
/// Whether the authenticator says the credential may be synced, which never changes
/// for a credential (WebAuthn Level 3 section 7.2).
/// </param>
/// <param name="BackupState">Whether the authenticator says it currently is.</param>
/// <param name="UserHandle">
/// The handle the authenticator returned, where it keeps one (REG-PM-001).
/// </param>
/// <remarks>Implements AUTH-FACT-013 and AUTH-FACT-014.</remarks>
internal sealed record WebAuthnAssertion(
    ReadOnlyMemory<byte> CredentialId,
    string RelyingPartyId,
    bool UserVerified,
    uint Counter,
    bool BackupEligible,
    bool BackupState,
    string? UserHandle = null);
