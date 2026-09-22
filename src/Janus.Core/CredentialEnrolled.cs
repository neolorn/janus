using System;

namespace Janus.Core;

/// <summary>
/// An authenticator reached <c>active</c> on an account, which is what the enrolment
/// notice to every recorded channel is about.
/// </summary>
/// <param name="RaisedAt">When the enrolment completed.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Credential">Which authenticator, as the credential endpoints name it.</param>
/// <param name="Kind">Which catalogue entry it is.</param>
/// <remarks>
/// Implements AUTH-STEP-007 and chapter 10 section 5b. The event carries what was
/// enrolled and nothing of the material that proves it.
/// </remarks>
public sealed record CredentialEnrolled(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    AuthenticatorId Credential,
    Factor Kind) : JanusEvent(RaisedAt, IdempotencyKey);
