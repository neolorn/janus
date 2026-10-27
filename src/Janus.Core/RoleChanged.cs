using System;

namespace Janus.Core;

/// <summary>
/// A role was defined, changed or removed.
/// </summary>
/// <param name="RaisedAt">When it happened.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Role">The role's name.</param>
/// <param name="Change">Which of the three happened.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-004 and chapter 10 section 5b (D-218). A role is the
/// deployment's and no person's, so the event names no subject; what the role allows is
/// read from the roles, as it is read live wherever access is worked out.
/// </remarks>
public sealed record RoleChanged(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    string Role,
    RoleChange Change) : DomainEvent(RaisedAt, IdempotencyKey);
