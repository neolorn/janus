using Janus.Core;

namespace Janus.Hosting.Credentials;

/// <summary>
/// What a completed creation ceremony produced, and what the person calls it.
/// </summary>
/// <param name="Credential">What the browser sent back.</param>
/// <param name="Label">What the person calls it.</param>
/// <remarks>Implements AUTH-FACT-001 and AUTH-FACT-011.</remarks>
internal sealed record CompleteKeyRequest(AuthenticatorAttestation? Credential, string? Label);
