using System;

namespace Janus.Core;

/// <summary>
/// An account entered the suspended state, by its own hand or an administrator's.
/// Nothing is deleted and no grant is removed: what stops is signing in and acting.
/// </summary>
/// <param name="RaisedAt">When the state changed.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="By">Who suspended it, which decides how it is stood back up.</param>
/// <remarks>Implements IDN-LIFE-013, IDN-ACCT-007 and chapter 10 section 5b.</remarks>
public sealed record AccountSuspended(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    SuspensionOrigin By) : JanusEvent(RaisedAt, IdempotencyKey);
