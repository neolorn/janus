using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One credential a WebAuthn request names: which one, and how its authenticator is
/// reached.
/// </summary>
/// <param name="Id">The credential identifier, base64url.</param>
/// <param name="Transports">
/// The transports the credential's enrolment reported, as the browser named them, and
/// none where it reported none.
/// </param>
/// <remarks>Implements AUTH-FACT-002b and AUTH-FACT-014.</remarks>
public sealed record WebAuthnCredentialDescriptor(string Id, IReadOnlyList<string> Transports);
