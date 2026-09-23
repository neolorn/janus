using System;

namespace Janus.Core;

/// <summary>
/// A loss report was opened against an authenticator, which suspends it at once for
/// the invalidation window.
/// </summary>
/// <param name="RaisedAt">When the report was opened.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Credential">Which authenticator, as the credential endpoints name it.</param>
/// <param name="Kind">Which catalogue entry it is.</param>
/// <param name="InvalidatesAt">
/// When the window ends and the suspension becomes final, unless the report is
/// cancelled first or no notice of it delivered.
/// </param>
/// <remarks>
/// Implements AUTH-RECOV-007 and chapter 10 section 5b. A suspended authenticator is
/// refused at a sign-in and at a gate, and the account still reaches the assurance it
/// reached before: nothing is recomputed until invalidation.
/// </remarks>
public sealed record CredentialSuspended(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    AuthenticatorId Credential,
    Factor Kind,
    DateTimeOffset InvalidatesAt) : DomainEvent(RaisedAt, IdempotencyKey);
