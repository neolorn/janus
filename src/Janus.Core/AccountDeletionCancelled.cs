using System;

namespace Janus.Core;

/// <summary>
/// An account's deletion grace window was cancelled inside it, and the account is
/// active again with everything it held.
/// </summary>
/// <param name="RaisedAt">When it was cancelled.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <remarks>Implements IDN-LIFE-014, IDN-ACCT-007 and chapter 10 section 5b.</remarks>
public sealed record AccountDeletionCancelled(
    DateTimeOffset RaisedAt,
    string IdempotencyKey) : JanusEvent(RaisedAt, IdempotencyKey);
