using System;

namespace Janus.Core;

/// <summary>
/// Support added credit to one key under a restriction.
/// </summary>
/// <param name="RaisedAt">When the credit was added.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Restriction">The name the restriction is edited under.</param>
/// <param name="Credit">How many sends the credit admits.</param>
/// <param name="Reason">Why it was granted.</param>
/// <remarks>
/// Implements AUTH-ABUSE-004 and chapter 10 section 5b. The key the credit was added
/// to never crosses the boundary: it is a value the library derives and never
/// returns.
/// </remarks>
public sealed record SendingRestrictionGranted(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    string Restriction,
    int Credit,
    string Reason) : JanusEvent(RaisedAt, IdempotencyKey);
