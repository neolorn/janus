using System.Buffers.Text;
using Janus.Core;

namespace Janus.Hosting.Credentials;

/// <summary>
/// The <c>RegistrationResponseJSON</c> that <c>PublicKeyCredential.toJSON()</c> gives
/// for a created credential (WebAuthn Level 3 section 5.1), as the body carries it.
/// </summary>
/// <param name="Id">The credential created, base64url.</param>
/// <param name="RawId">The same identifier as the browser's <c>rawId</c>, base64url.</param>
/// <param name="Type">The credential type.</param>
/// <param name="Response">What the authenticator answered.</param>
/// <remarks>
/// Implements AUTH-FACT-014. The members the library does not read, the client
/// extension outputs among them, are not members of it and are passed over (D-197).
/// </remarks>
internal sealed record RegistrationResponseJson(
    string? Id,
    string? RawId,
    string? Type,
    AuthenticatorAttestationResponseJson? Response)
{
    /// <summary>
    /// Reads what the browser sent back.
    /// </summary>
    /// <returns>
    /// The attestation, or nothing where a member the ceremony needs is absent or is
    /// not base64url.
    /// </returns>
    public AuthenticatorAttestation? Read() =>
        Id is { Length: > 0 }
        && RawId is { Length: > 0 }
        && Type is { Length: > 0 }
        && Response is { ClientDataJson: { Length: > 0 }, AttestationObject: { Length: > 0 } }
        && Base64Url.IsValid(Id)
        && Base64Url.IsValid(RawId)
        && Base64Url.IsValid(Response.ClientDataJson)
        && Base64Url.IsValid(Response.AttestationObject)
            ? new AuthenticatorAttestation(
                Id,
                RawId,
                Type,
                Response.ClientDataJson,
                Response.AttestationObject,
                Response.Transports ?? [])
            : null;
}
