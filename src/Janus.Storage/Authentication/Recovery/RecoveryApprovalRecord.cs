using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Recovery;

/// <summary>
/// The <c>recovery_approvals</c> row: one approver standing behind one account's
/// re-enrolment.
/// </summary>
/// <remarks>
/// Implements AUTH-RECOV-002 and AUTH-RECOV-003. The channel the confirmation was
/// made on is an identifier of the person, so it is held under their own key and an
/// erasure leaves it unreadable.
/// </remarks>
internal sealed class RecoveryApprovalRecord
{
    /// <summary>The <c>subject</c> column: whose account is being recovered.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>approver</c> column.</summary>
    public SubjectId Approver { get; set; }

    /// <summary>The <c>approved_at</c> column.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>The <c>enc_channel</c> column.</summary>
    public byte[] Channel { get; set; } = [];

    /// <summary>The <c>spent_at</c> column, and nothing while it still stands.</summary>
    public DateTimeOffset? SpentAt { get; set; }
}
