using System;

namespace Janus.Core;

/// <summary>
/// Phase one of a takedown committed: the account is suspended into its grace window,
/// its sessions are ended, and nothing of it is processed from here on. What remains
/// is each host doing its own half for the subject, which it confirms against this
/// delivery.
/// </summary>
/// <param name="RaisedAt">When the trigger transaction committed.</param>
/// <param name="IdempotencyKey">The key a handler recognises a repeat by.</param>
/// <remarks>
/// Implements IDN-LIFE-003, IDN-LIFE-003a and chapter 10 section 5b. It states that the
/// account was taken down and never what a consumer is to do about it.
/// </remarks>
public sealed record TakedownExecuted(
    DateTimeOffset RaisedAt,
    string IdempotencyKey) : SubjectEvent(RaisedAt, IdempotencyKey);
