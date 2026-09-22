using System;

namespace Janus.Core;

/// <summary>
/// An objection was recorded or withdrawn for a purpose on an objectable basis.
/// </summary>
/// <param name="RaisedAt">When it happened.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Purpose">Which purpose, as the host declared it.</param>
/// <param name="Objecting">
/// Whether the subject now objects. Processing of that subject for that purpose stops
/// when the event is handled, and resumes only when the objection is withdrawn.
/// </param>
/// <remarks>
/// Implements PRIV-RIGHT-001a and chapter 10 section 5b. An objection is always
/// honoured: no handler is offered a ground on which to refuse it.
/// </remarks>
public sealed record ObjectionChanged(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    string Purpose,
    bool Objecting) : JanusEvent(RaisedAt, IdempotencyKey);
