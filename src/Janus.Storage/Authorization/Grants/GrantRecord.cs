using System;
using Janus.Core;

namespace Janus.Storage.Authorization.Grants;

/// <summary>
/// The <c>grants</c> row: one subject, one role, one resource or none.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-001, AUTHZ-GRANT-002, AUTHZ-GRANT-003 and CONV-DESIGN-003.
/// One table carries an account's grant and a group's, an allow and a deny, a grant on
/// one record and a grant on a whole organization.
/// </remarks>
internal sealed class GrantRecord
{
    /// <summary>
    /// The <c>id</c> column, which is this table's key.
    /// </summary>
    public GrantId Id { get; set; }

    /// <summary>
    /// The <c>subject_type</c> column.
    /// </summary>
    public SubjectType SubjectType { get; set; }

    /// <summary>
    /// The <c>subject_id</c> column.
    /// </summary>
    public Guid SubjectId { get; set; }

    /// <summary>
    /// The <c>role</c> column.
    /// </summary>
    public RoleName Role { get; set; }

    /// <summary>
    /// The <c>organization</c> column.
    /// </summary>
    public OrganizationId Organization { get; set; }

    /// <summary>
    /// The <c>resource_type</c> column, absent where the grant is on the organization.
    /// </summary>
    public ResourceType? ResourceType { get; set; }

    /// <summary>
    /// The <c>resource_id</c> column, absent where the grant is on the organization.
    /// </summary>
    public ResourceId? ResourceId { get; set; }

    /// <summary>
    /// The <c>deny</c> column.
    /// </summary>
    public bool Deny { get; set; }

    /// <summary>
    /// The <c>kind</c> column.
    /// </summary>
    public GrantKind Kind { get; set; }

    /// <summary>
    /// The <c>expires_at</c> column.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// The <c>granted_by</c> column.
    /// </summary>
    public SubjectId GrantedBy { get; set; }

    /// <summary>
    /// The <c>granted_at</c> column.
    /// </summary>
    public DateTimeOffset GrantedAt { get; set; }

    /// <summary>
    /// The <c>reason</c> column.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// The <c>revoked_by</c> column.
    /// </summary>
    public SubjectId? RevokedBy { get; set; }

    /// <summary>
    /// The <c>revoked_at</c> column.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// The <c>revocation_reason</c> column.
    /// </summary>
    public string? RevocationReason { get; set; }
}
