using System;
using Janus.Core;

namespace Janus.Storage.Privacy.Requests;

/// <summary>
/// The <c>privacy_requests</c> row.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-001 and PRIV-RIGHT-002. A decided request keeps its row: the
/// record persists whatever the decision was, including a deadline that passed.
/// </remarks>
internal sealed class PrivacyRequestRecord
{
    /// <summary>The <c>id</c> column.</summary>
    public PrivacyRequestId Id { get; set; }

    /// <summary>The <c>subject</c> column.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>type</c> column.</summary>
    public PrivacyRequestType Type { get; set; }

    /// <summary>The <c>detail</c> column: what the request said.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>The <c>received_at</c> column, a calendar date in the deployment zone.</summary>
    public DateOnly ReceivedAt { get; set; }

    /// <summary>The <c>created_at</c> column.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The <c>decision_due</c> column.</summary>
    public DateTimeOffset DecisionDue { get; set; }

    /// <summary>The <c>warn_at</c> column.</summary>
    public DateTimeOffset WarnAt { get; set; }

    /// <summary>The <c>escalate_at</c> column.</summary>
    public DateTimeOffset EscalateAt { get; set; }

    /// <summary>The <c>status</c> column.</summary>
    public PrivacyRequestStatus Status { get; set; }

    /// <summary>The <c>decided_at</c> column.</summary>
    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>The <c>decision_reason</c> column.</summary>
    public string? DecisionReason { get; set; }

    /// <summary>The <c>channel</c> column: how an out-of-band request arrived.</summary>
    public string? Channel { get; set; }

    /// <summary>The <c>identity_confirmation</c> column.</summary>
    public string? IdentityConfirmation { get; set; }

    /// <summary>The <c>warned_at</c> column.</summary>
    public DateTimeOffset? WarnedAt { get; set; }

    /// <summary>The <c>escalated_at</c> column.</summary>
    public DateTimeOffset? EscalatedAt { get; set; }
}
