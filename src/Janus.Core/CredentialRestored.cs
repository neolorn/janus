using System;

namespace Janus.Core;

/// <summary>
/// A loss report was cancelled, from the link every notice carried or from a session
/// of the account, and the authenticator is active again.
/// </summary>
/// <param name="RaisedAt">When the report was cancelled.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Credential">Which authenticator, as the credential endpoints name it.</param>
/// <param name="Kind">Which catalogue entry it is.</param>
/// <remarks>Implements AUTH-RECOV-007 and chapter 10 section 5b.</remarks>
public sealed record CredentialRestored(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    AuthenticatorId Credential,
    Factor Kind) : DomainEvent(RaisedAt, IdempotencyKey);
