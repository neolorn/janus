using System;

namespace Janus.Core;

/// <summary>
/// An identifier was removed from an account and stops resolving at once.
/// </summary>
/// <param name="RaisedAt">When it was removed.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Identifier">Which identifier.</param>
/// <param name="Kind">Which kind it was.</param>
/// <remarks>
/// Implements REG-IDENT-006 and chapter 10 section 5b. An undo inside the cooling-off
/// restores it and fires <see cref="IdentifierAdded"/>; the value itself is never on
/// the event.
/// </remarks>
public sealed record IdentifierRemoved(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    IdentifierId Identifier,
    IdentifierKind Kind) : JanusEvent(RaisedAt, IdempotencyKey);
