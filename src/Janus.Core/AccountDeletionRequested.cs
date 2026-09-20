using System;

namespace Janus.Core;

/// <summary>
/// An account's deletion grace window has begun. Nothing is erased yet: the erasure
/// runs when the window elapses without cancellation.
/// </summary>
/// <param name="RaisedAt">When the window began.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="By">Why the window was entered.</param>
/// <param name="ErasesAt">When the erasure runs if nothing cancels it.</param>
/// <remarks>
/// Implements IDN-LIFE-014, IDN-ACCT-007 and chapter 10 section 5b. It does not fire
/// for a takedown, whose trigger raises its own event (IDN-LIFE-003).
/// </remarks>
public sealed record AccountDeletionRequested(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    DeletionOrigin By,
    DateTimeOffset ErasesAt) : JanusEvent(RaisedAt, IdempotencyKey);
