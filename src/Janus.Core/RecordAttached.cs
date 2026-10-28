using System;

namespace Janus.Core;

/// <summary>
/// A person record was attached to an account. The record is the event's subject and
/// keeps that subject, so nothing a host holds under it moves.
/// </summary>
/// <param name="RaisedAt">When the record was attached.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Account">The account it was attached to.</param>
/// <remarks>Implements REC-ATT-001 and chapter 10 section 5b.</remarks>
public sealed record RecordAttached(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    SubjectId Account) : DomainEvent(RaisedAt, IdempotencyKey);
