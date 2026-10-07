using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What a browser asks an authenticator with where the answer it was given offers a
/// second-factor security key: the request options for the challenge, naming the
/// credentials that may answer it.
/// </summary>
/// <param name="RelyingPartyId">The identifier the credentials are scoped to.</param>
/// <param name="Challenge">
/// The one-time value the authenticator signs, base64url: the one the challenge was
/// opened with, which the assertion that follows is verified against.
/// </param>
/// <param name="AllowCredentials">
/// Each active second-factor security key of the account and, at a step-up that also
/// offers the passkey, each active passkey of the account, so that a passkey can still
/// answer a request that names credentials.
/// </param>
/// <remarks>
/// Implements AUTH-FACT-002b and AUTH-FACT-014. A second-factor security key is a
/// credential its authenticator does not keep, so it answers only a request that names
/// it. No answer before a first factor carries one of these: a credential named there
/// would say what the account holds (AUTH-ABUSE-003).
/// </remarks>
public sealed record WebAuthnRequestOptions(
    string RelyingPartyId,
    string Challenge,
    IReadOnlyList<WebAuthnCredentialDescriptor> AllowCredentials)
{
    /// <summary>
    /// What is asked of the authenticator about the person: every ceremony requires
    /// user verification, so there is nothing here that could ask for less.
    /// </summary>
    public string UserVerification { get; } = "required";
}
