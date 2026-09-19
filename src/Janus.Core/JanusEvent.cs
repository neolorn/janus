using System;

namespace Janus.Core;

/// <summary>
/// What every event the library emits carries: when it happened, the key a consumer
/// recognises a repeat by, and the identities involved where a person acted.
/// </summary>
/// <param name="RaisedAt">When the fact the event states became true.</param>
/// <param name="IdempotencyKey">
/// The stable key a consumer deduplicates on. Replaying an event produces no
/// duplicate (INT-MAIL-007).
/// </param>
/// <remarks>
/// Implements LIB-API-001, chapter 10 section 5b, INT-MAIL-007 and IDN-LIFE-003a. No
/// event names a consumer or a consumer's domain.
/// </remarks>
public abstract record JanusEvent(DateTimeOffset RaisedAt, string IdempotencyKey)
{
    /// <summary>
    /// Whose account the event is about, where it is about one.
    /// </summary>
    public SubjectId? Subject { get; init; }

    /// <summary>
    /// Who acted, where a person did.
    /// </summary>
    public SubjectId? Actor { get; init; }

    /// <summary>
    /// Whose authority they acted under, where it was not their own.
    /// </summary>
    public SubjectId? Effective { get; init; }
}
