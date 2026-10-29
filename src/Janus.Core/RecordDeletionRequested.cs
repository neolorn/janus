using System;

namespace Janus.Core;

/// <summary>
/// A person record's deletion grace window has begun, by the person or by a request that
/// arrived out of band. Nothing is erased yet: the erasure runs when the window elapses
/// without cancellation.
/// </summary>
/// <param name="RaisedAt">When the window began.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="By">Why the window was entered.</param>
/// <param name="ErasesAt">When the erasure runs if nothing cancels it.</param>
/// <remarks>
/// Implements REC-ACC-002 and chapter 10 section 5b. It does not fire for a takedown,
/// whose trigger raises its own event (REC-TKD-001).
/// </remarks>
public sealed record RecordDeletionRequested(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    DeletionOrigin By,
    DateTimeOffset ErasesAt) : DomainEvent(RaisedAt, IdempotencyKey);
