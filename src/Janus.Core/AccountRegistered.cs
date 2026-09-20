using System;

namespace Janus.Core;

/// <summary>
/// An account exists. The one transaction of the terms step has committed and the
/// account is active.
/// </summary>
/// <param name="RaisedAt">When the transaction committed.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <remarks>
/// Implements REG-SESS-001, REG-SESS-007 and chapter 10 section 5b. It fires once per
/// account and never for a registration session that was abandoned or expired. No
/// mailbox follows from it: provisioning follows the invitation and the membership.
/// </remarks>
public sealed record AccountRegistered(
    DateTimeOffset RaisedAt,
    string IdempotencyKey) : JanusEvent(RaisedAt, IdempotencyKey);
