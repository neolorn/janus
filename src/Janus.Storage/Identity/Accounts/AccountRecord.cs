using System;
using Janus.Core;

namespace Janus.Storage.Identity.Accounts;

/// <summary>
/// The <c>accounts</c> row.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-003. What EF Core maps is this record and never the account
/// itself; the store translates between the two.
/// </remarks>
internal sealed class AccountRecord
{
    /// <summary>
    /// The subject column, which is this table's key and the column every personal
    /// field of the account names as its subject.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>created_at</c> column.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The <c>state</c> column.
    /// </summary>
    public AccountState State { get; set; }

    /// <summary>
    /// The <c>suspended_by</c> column.
    /// </summary>
    public SuspensionOrigin? SuspendedBy { get; set; }

    /// <summary>
    /// The <c>deleting_by</c> column.
    /// </summary>
    public DeletionOrigin? DeletingBy { get; set; }

    /// <summary>
    /// The <c>deleting_since</c> column.
    /// </summary>
    public DateTimeOffset? DeletingSince { get; set; }
}
