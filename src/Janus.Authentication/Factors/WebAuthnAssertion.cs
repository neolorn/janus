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
/// <remarks>Implements AUTH-FACT-014.</remarks>
internal sealed record WebAuthnAssertion(
    ReadOnlyMemory<byte> CredentialId,
    string RelyingPartyId,
    bool UserVerified,
    uint Counter);
