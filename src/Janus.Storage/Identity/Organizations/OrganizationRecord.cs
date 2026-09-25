using System;
using Janus.Core;

namespace Janus.Storage.Identity.Organizations;

/// <summary>
/// The <c>organizations</c> row.
/// </summary>
/// <remarks>
/// Implements IDN-ORG-001, IDN-ORG-003, IDN-ORG-004, IDN-ACCT-004 and CONV-DESIGN-003.
/// The name is plaintext under the case-insensitive collation, with its comparison key
/// beside it; nothing here distinguishes one kind of organization from another
/// (IDN-ORG-002).
/// </remarks>
internal sealed class OrganizationRecord
{
    /// <summary>
    /// The <c>id</c> column, which is this table's key.
    /// </summary>
    public OrganizationId Id { get; set; }

    /// <summary>
    /// The <c>name</c> column.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The <c>canonical_name</c> column, the name's <c>NFKC_Casefold</c> comparison key
    /// (IDN-ACCT-004). Every write carries it; it is absent only on a row written before
    /// the column was, which OPS-MIG-005 adds nullable.
    /// </summary>
    public string? CanonicalName { get; set; }

    /// <summary>
    /// The <c>created_at</c> column.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The <c>administrative</c> column, true on the one organization bootstrap marks
    /// and on no other.
    /// </summary>
    public bool IsAdministrative { get; set; }

    /// <summary>
    /// The <c>deletion_requested_at</c> column, which is the instant access stopped
    /// and the grace window began.
    /// </summary>
    public DateTimeOffset? DeletionRequestedAt { get; set; }

    /// <summary>
    /// The <c>erased_at</c> column.
    /// </summary>
    public DateTimeOffset? ErasedAt { get; set; }
}
