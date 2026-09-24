using System;
using Janus.Core;

namespace Janus.Storage.Identity.Audit;

/// <summary>
/// The <c>audit_records</c> row.
/// </summary>
/// <remarks>
/// Implements IDN-AUD-001, PRIV-RET-002, PRIV-RET-003, PRIV-RET-004 and
/// CONV-DESIGN-003. The row holds identifiers and codes; the one column that may hold
/// an attribute is held under the effective subject's key, so erasure reaches it
/// without the row being touched.
/// </remarks>
internal sealed class AuditRowRecord
{
    /// <summary>
    /// The <c>id</c> column.
    /// </summary>
    public AuditRecordId Id { get; set; }

    /// <summary>
    /// The <c>category</c> column, which is the partition the row is routed to.
    /// </summary>
    public AuditCategory Category { get; set; }

    /// <summary>
    /// The <c>occurred_at</c> column: the instant the event occurred, in UTC, which is
    /// also the range the row's partition covers.
    /// </summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// The <c>action</c> column.
    /// </summary>
    public AuditAction Action { get; set; }

    /// <summary>
    /// The <c>acting_subject</c> column.
    /// </summary>
    public SubjectId ActingSubject { get; set; }

    /// <summary>
    /// The <c>effective_subject</c> column.
    /// </summary>
    public SubjectId EffectiveSubject { get; set; }

    /// <summary>
    /// The <c>organization</c> column, absent where the event belongs to no
    /// organization.
    /// </summary>
    public OrganizationId? Organization { get; set; }

    /// <summary>
    /// The <c>details</c> column: the structured fields of the event.
    /// </summary>
    public string Details { get; set; } = "{}";

    /// <summary>
    /// The <c>enc_details</c> column: the attributes the event records, under the
    /// effective subject's key.
    /// </summary>
    public byte[]? PersonalDetails { get; set; }

    /// <summary>
    /// The <c>principal</c> column: the system principal that took the action, absent
    /// where a person took it.
    /// </summary>
    public string? Principal { get; set; }

    /// <summary>
    /// The <c>principal_reason</c> column: the reason that principal stated.
    /// </summary>
    public string? PrincipalReason { get; set; }
}
