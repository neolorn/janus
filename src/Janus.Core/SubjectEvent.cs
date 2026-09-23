using System;

namespace Janus.Core;

/// <summary>
/// An event about one person that a host has work to do for: erasure, restriction
/// and export, each of which asks which records are about that person, a question
/// the library cannot answer over tables it must not read.
/// </summary>
/// <param name="RaisedAt">When the fact the event states became true.</param>
/// <param name="IdempotencyKey">The key a handler recognises a repeat by.</param>
/// <remarks>
/// Implements IDN-LIFE-003a, PRIV-RIGHT-005b and LIB-HOST-002. These are the events
/// the transactional outbox delivers, each to every registered handler, each
/// confirmed independently, and the request completes only when every required
/// handler has confirmed.
/// </remarks>
public abstract record SubjectEvent(DateTimeOffset RaisedAt, string IdempotencyKey)
    : DomainEvent(RaisedAt, IdempotencyKey);
