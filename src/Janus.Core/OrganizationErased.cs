using System;

namespace Janus.Core;

/// <summary>
/// An organization's deletion grace window elapsed and the erasure executed. The row
/// is where it was and the identifier goes on resolving; what the organization held
/// is what has gone.
/// </summary>
/// <param name="RaisedAt">When the erasure executed.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Organization">Which organization was erased.</param>
/// <param name="MembershipsEnded">How many memberships the erasure ended.</param>
/// <remarks>
/// Implements IDN-ORG-003 and chapter 10 section 5b. The event is about an
/// organization and no person, so it names no subject.
/// </remarks>
public sealed record OrganizationErased(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    OrganizationId Organization,
    int MembershipsEnded) : JanusEvent(RaisedAt, IdempotencyKey);
