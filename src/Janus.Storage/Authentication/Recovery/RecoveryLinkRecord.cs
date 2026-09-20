using System;
using Janus.Authentication.Recovery;
using Janus.Core;

namespace Janus.Storage.Authentication.Recovery;

/// <summary>
/// The <c>recovery_links</c> row: one link that has gone out, and, once spent, the
/// enrolment session it opened.
/// </summary>
/// <remarks>
/// Implements AUTH-RECOV-002 and AUTH-RECOV-005. The token is held as its
/// fingerprint, so a dump of the table opens nothing. A spent enrolment link stays as
/// the session it opened, which is what caps that session at the link's lifetime.
/// </remarks>
internal sealed class RecoveryLinkRecord
{
    /// <summary>The <c>token</c> column: what the link's token hashes to.</summary>
    public byte[] Token { get; set; } = [];

    /// <summary>The <c>subject</c> column.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>purpose</c> column: what the link may do.</summary>
    public RecoveryPurpose Purpose { get; set; }

    /// <summary>The <c>issued_at</c> column.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>The <c>expires_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>The <c>approver</c> column, and nothing where the person asked.</summary>
    public SubjectId? Approver { get; set; }

    /// <summary>The <c>mailbox_lost</c> column.</summary>
    public bool MailboxLost { get; set; }

    /// <summary>The <c>session</c> column, and nothing while the link is unspent.</summary>
    public EnrolmentSessionId? Session { get; set; }

    /// <summary>The <c>spent_at</c> column, and nothing while the link is unspent.</summary>
    public DateTimeOffset? SpentAt { get; set; }
}
