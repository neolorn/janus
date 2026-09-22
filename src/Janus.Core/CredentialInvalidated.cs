using System;

namespace Janus.Core;

/// <summary>
/// A loss report completed after its window, and the authenticator is invalidated.
/// </summary>
/// <param name="RaisedAt">When the window completed.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Credential">Which authenticator, as the credential endpoints name it.</param>
/// <param name="Kind">Which catalogue entry it is.</param>
/// <remarks>
/// Implements AUTH-RECOV-007 and chapter 10 section 5b. This is the one point at
/// which the account's reachable assurance is recomputed (AUTH-STEP-006).
/// </remarks>
public sealed record CredentialInvalidated(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    AuthenticatorId Credential,
    Factor Kind) : JanusEvent(RaisedAt, IdempotencyKey);
