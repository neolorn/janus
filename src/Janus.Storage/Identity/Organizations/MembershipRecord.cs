using System;
using Janus.Core;

namespace Janus.Storage.Identity.Organizations;

/// <summary>
/// The <c>memberships</c> row.
/// </summary>
/// <remarks>
/// Implements IDN-MEM-001, IDN-MEM-002 and CONV-DESIGN-003. Nothing in the table's
/// shape limits an account to one membership; the limit is a setting.
/// </remarks>
internal sealed class MembershipRecord
{
    /// <summary>
    /// The <c>id</c> column, which is this table's key.
    /// </summary>
    public MembershipId Id { get; set; }

    /// <summary>
    /// The <c>subject</c> column.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>organization</c> column.
    /// </summary>
    public OrganizationId Organization { get; set; }

    /// <summary>
    /// The <c>created_at</c> column.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The <c>ended_at</c> column.
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }
}
