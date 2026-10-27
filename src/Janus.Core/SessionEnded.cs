using System;

namespace Janus.Core;

/// <summary>
/// A session ended before its expiry.
/// </summary>
/// <param name="RaisedAt">When it ended.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Session">Which session.</param>
/// <param name="Reason">Why it ended.</param>
/// <remarks>
/// Implements AUTH-SESS-010 and chapter 10 section 5b (D-218). The account whose session
/// it was is the event's subject. A session that expires by time raises none, since
/// nothing ends it at an instant: it ends at its next use.
/// </remarks>
public sealed record SessionEnded(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    SessionId Session,
    SessionEndReason Reason) : DomainEvent(RaisedAt, IdempotencyKey);
