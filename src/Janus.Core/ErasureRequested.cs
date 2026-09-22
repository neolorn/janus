using System;

namespace Janus.Core;

/// <summary>
/// A subject was erased. The library's own work has committed: the account is
/// <c>deleted</c>, the wrapped key is overwritten and the fingerprints are
/// neutralised. What remains is the host redacting what it holds in its own tables.
/// </summary>
/// <param name="RaisedAt">When the erasure transaction committed.</param>
/// <param name="IdempotencyKey">The key a handler recognises a repeat by.</param>
/// <param name="Reason">Why the erasure happened.</param>
/// <remarks>
/// Implements PRIV-RIGHT-005b, IDN-LIFE-003a and chapter 10 section 5b. It states
/// that an account was erased and never what a consumer is to do about it.
/// </remarks>
public sealed record ErasureRequested(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    ErasureReason Reason) : SubjectEvent(RaisedAt, IdempotencyKey);
