using System;

namespace Janus.Core;

/// <summary>
/// What a browser needs to ask an authenticator for an assertion.
/// </summary>
/// <param name="RelyingPartyId">The identifier the credential is scoped to.</param>
/// <param name="Challenge">The one-time value the authenticator signs, base64url.</param>
/// <remarks>
/// Implements AUTH-FACT-011 and AUTH-FACT-014. One is returned by every
/// <c>POST /auth/begin</c>, existing identifier or not: a discoverable credential
/// needs no allow-list, so issuing a challenge says nothing about what the account
/// holds (`09` section 3).
/// </remarks>
public sealed record WebAuthnChallenge(string RelyingPartyId, string Challenge);
