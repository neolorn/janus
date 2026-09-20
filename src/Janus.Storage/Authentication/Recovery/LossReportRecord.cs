using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Recovery;

/// <summary>
/// The <c>loss_reports</c> row: one credential its holder has said is gone, and how
/// far through the notified window it is.
/// </summary>
/// <remarks>
/// Implements AUTH-RECOV-007 and D-141. The cancel token is held as the notices
/// carry it, because every repeat of the notice has to carry the same link, so it is
/// held under the account's own key and an erasure leaves it unreadable.
/// </remarks>
internal sealed class LossReportRecord
{
    /// <summary>The <c>credential</c> column, which is this table key.</summary>
    public AuthenticatorId Credential { get; set; }

    /// <summary>The <c>subject</c> column.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>enc_cancel</c> column.</summary>
    public byte[] Cancel { get; set; } = [];

    /// <summary>The <c>reported_at</c> column.</summary>
    public DateTimeOffset ReportedAt { get; set; }

    /// <summary>The <c>invalidates_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset InvalidatesAt { get; set; }

    /// <summary>The <c>notified_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset NotifiedAt { get; set; }

    /// <summary>The <c>any_delivered</c> column.</summary>
    public bool AnyDelivered { get; set; }

    /// <summary>The <c>held_at</c> column, and nothing where the window completed.</summary>
    public DateTimeOffset? HeldAt { get; set; }
}
