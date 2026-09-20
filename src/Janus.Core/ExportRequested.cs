using System;

namespace Janus.Core;

/// <summary>
/// A subject's export is being assembled. Each host returns everything it holds
/// about that person, which is the half of the export the library cannot produce.
/// </summary>
/// <param name="RaisedAt">When the export was asked for.</param>
/// <param name="IdempotencyKey">The key a handler recognises a repeat by.</param>
/// <remarks>
/// Implements PRIV-RIGHT-003, PRIV-RIGHT-005b and chapter 10 section 5b.
/// </remarks>
public sealed record ExportRequested(
    DateTimeOffset RaisedAt,
    string IdempotencyKey) : SubjectEvent(RaisedAt, IdempotencyKey);
