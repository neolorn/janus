using System;

namespace Janus.Core;

/// <summary>
/// A person record's deletion grace window was cancelled inside it, and the record is
/// back in the state it left, with everything it held.
/// </summary>
/// <param name="RaisedAt">When it was cancelled.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <remarks>Implements REC-ACC-002 and chapter 10 section 5b.</remarks>
public sealed record RecordDeletionCancelled(
    DateTimeOffset RaisedAt,
    string IdempotencyKey) : DomainEvent(RaisedAt, IdempotencyKey);
