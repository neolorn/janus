using System;

namespace Janus.Core;

/// <summary>
/// An account left the suspended state, with the access it had before it.
/// </summary>
/// <param name="RaisedAt">When the state changed.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <remarks>Implements IDN-LIFE-013 and chapter 10 section 5b.</remarks>
public sealed record AccountReactivated(
    DateTimeOffset RaisedAt,
    string IdempotencyKey) : JanusEvent(RaisedAt, IdempotencyKey);
