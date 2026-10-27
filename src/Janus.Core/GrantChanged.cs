using System;

namespace Janus.Core;

/// <summary>
/// A grant was made or revoked, a materialised derivation's grant written or taken back
/// included.
/// </summary>
/// <param name="RaisedAt">When it happened.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Grant">Which grant.</param>
/// <param name="Kind">Whether someone wrote the grant or a derivation materialised it.</param>
/// <param name="SubjectType">Whether the holder is an account or a group.</param>
/// <param name="SubjectId">The holder's identifier.</param>
/// <param name="Role">The role the grant confers or withholds, as its name is written.</param>
/// <param name="Deny">Whether the grant denies the role rather than allowing it.</param>
/// <param name="Organization">Which organization the grant is scoped to.</param>
/// <param name="ResourceType">The type of the record the grant is on, <c>organization</c> for the whole organization.</param>
/// <param name="ResourceId">The record's identifier, the organization's for the whole organization.</param>
/// <param name="Change">Which of the two happened.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-001, AUTHZ-DERIVE-005 and chapter 10 section 5b (D-218). A
/// holder that is an account is also the event's subject; a group is no data subject, so
/// a grant to a group leaves the subject empty. A grant on the whole organization is
/// written as <c>GET /admin/grants</c> writes it (chapter 09 section 8a).
/// </remarks>
public sealed record GrantChanged(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    GrantId Grant,
    GrantKind Kind,
    SubjectType SubjectType,
    Guid SubjectId,
    string Role,
    bool Deny,
    OrganizationId Organization,
    string ResourceType,
    string ResourceId,
    GrantChange Change) : DomainEvent(RaisedAt, IdempotencyKey);
