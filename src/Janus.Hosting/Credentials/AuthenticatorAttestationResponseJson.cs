using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Janus.Hosting.Credentials;

/// <summary>
/// The <c>response</c> of a <c>RegistrationResponseJSON</c> (WebAuthn Level 3 section
/// 5.1): what the authenticator answered a creation ceremony with.
/// </summary>
/// <param name="ClientDataJson">The client data the browser wrote, base64url.</param>
/// <param name="AttestationObject">
/// The authenticator data and the attestation statement, base64url.
/// </param>
/// <param name="Transports">The ways the authenticator can be reached.</param>
/// <remarks>Implements AUTH-FACT-014.</remarks>
internal sealed record AuthenticatorAttestationResponseJson(
    [property: JsonPropertyName("clientDataJSON")] string? ClientDataJson,
    string? AttestationObject,
    IReadOnlyList<string>? Transports);
