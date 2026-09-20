using System;

namespace Janus.Core;

/// <summary>
/// The primary identifier of a kind changed, so ordinary communications go somewhere
/// else from now on.
/// </summary>
/// <param name="RaisedAt">When the role moved.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Identifier">The identifier that holds the role now.</param>
/// <param name="Kind">Which kind it is.</param>
/// <remarks>
/// Implements REG-IDENT-005 and chapter 10 section 5b. Acknowledging an invitation that
/// makes a corporate address primary fires it too; the value itself is never on the
/// event.
/// </remarks>
public sealed record IdentifierPrimaryChanged(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    IdentifierId Identifier,
    IdentifierKind Kind) : JanusEvent(RaisedAt, IdempotencyKey);
