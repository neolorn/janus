using System;

namespace Janus.Core;

/// <summary>
/// A consent was granted, withdrawn or superseded.
/// </summary>
/// <param name="RaisedAt">When it happened.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Purpose">Which purpose, as the host declared it.</param>
/// <param name="Change">Which of the three happened.</param>
/// <remarks>
/// Implements PRIV-CONS-007, PRIV-CONS-008, PRIV-SENS-002a and chapter 10 section 5b.
/// A handler for the purpose is required: on withdrawal it erases what was held
/// solely for that purpose, and it touches nothing held under another basis.
/// </remarks>
public sealed record ConsentChanged(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    string Purpose,
    ConsentChange Change) : JanusEvent(RaisedAt, IdempotencyKey);
