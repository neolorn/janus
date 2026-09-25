using System;

namespace Janus.Core;

/// <summary>
/// A membership began or ended.
/// </summary>
/// <param name="RaisedAt">When it happened.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Membership">Which membership.</param>
/// <param name="Organization">Which organization it is of.</param>
/// <param name="Change">Which of the two happened.</param>
/// <remarks>
/// Implements IDN-MEM-001, INT-MAIL-006 and chapter 10 section 5b. The event names the
/// membership and the organization; whose it is is the subject every event carries.
/// </remarks>
public sealed record MembershipChanged(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    MembershipId Membership,
    OrganizationId Organization,
    MembershipChange Change) : DomainEvent(RaisedAt, IdempotencyKey);
