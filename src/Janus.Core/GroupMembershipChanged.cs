using System;

namespace Janus.Core;

/// <summary>
/// An account or a group was added to a group or taken out of it.
/// </summary>
/// <param name="RaisedAt">When it happened.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Group">Which group.</param>
/// <param name="Organization">Which organization the group belongs to.</param>
/// <param name="SubjectType">Whether the member is an account or a group.</param>
/// <param name="SubjectId">The member's identifier.</param>
/// <param name="Change">Which of the two happened.</param>
/// <remarks>
/// Implements AUTHZ-GROUP-001, IDN-MEM-001 and chapter 10 section 5b (D-218). A member
/// that is an account is also the event's subject; a group is no data subject, so a
/// group member leaves the subject empty.
/// </remarks>
public sealed record GroupMembershipChanged(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    GroupId Group,
    OrganizationId Organization,
    SubjectType SubjectType,
    Guid SubjectId,
    GroupMembershipChange Change) : DomainEvent(RaisedAt, IdempotencyKey);
