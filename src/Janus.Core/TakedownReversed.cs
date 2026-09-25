using System;

namespace Janus.Core;

/// <summary>
/// A takedown was reversed inside its window: the account is active again and can sign
/// in. Nothing the hosts undid at the trigger is restored by it.
/// </summary>
/// <param name="RaisedAt">When the reversal committed.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <remarks>Implements IDN-LIFE-003 and chapter 10 section 5b.</remarks>
public sealed record TakedownReversed(
    DateTimeOffset RaisedAt,
    string IdempotencyKey) : DomainEvent(RaisedAt, IdempotencyKey);
