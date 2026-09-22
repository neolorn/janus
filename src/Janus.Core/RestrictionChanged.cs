using System;

namespace Janus.Core;

/// <summary>
/// A subject's processing restriction was set or lifted. Restriction suspends
/// action, never visibility: the host stops acting on that person's records until it
/// is lifted, and deletes nothing.
/// </summary>
/// <param name="RaisedAt">When the state changed.</param>
/// <param name="IdempotencyKey">The key a handler recognises a repeat by.</param>
/// <param name="Restricted">Whether the subject is now restricted.</param>
/// <remarks>
/// Implements PRIV-RIGHT-004, PRIV-RIGHT-005b and chapter 10 section 5b.
/// </remarks>
public sealed record RestrictionChanged(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    bool Restricted) : SubjectEvent(RaisedAt, IdempotencyKey);
