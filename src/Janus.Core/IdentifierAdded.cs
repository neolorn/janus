using System;

namespace Janus.Core;

/// <summary>
/// An added email or phone was verified and counts from now on.
/// </summary>
/// <param name="RaisedAt">When it was verified.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Identifier">Which identifier.</param>
/// <param name="Kind">Which kind it is.</param>
/// <remarks>
/// Implements REG-IDENT-004, REG-IDENT-007 and chapter 10 section 5b. A replace that
/// completed and an undo that restored a removal both fire it; the value itself is
/// never on the event.
/// </remarks>
public sealed record IdentifierAdded(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    IdentifierId Identifier,
    IdentifierKind Kind) : DomainEvent(RaisedAt, IdempotencyKey);
