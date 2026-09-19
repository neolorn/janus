using System;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The <c>sends</c> row: one message a transport took, the keys it counted against,
/// and when the last of those counts stops deciding anything.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-004 and INT-SMS-005. The correlation reference is held by
/// its hash, so a table dump yields nothing a forged callback could present.
/// </remarks>
internal sealed class SendRecord
{
    /// <summary>The <c>reference</c> column, which is this table key.</summary>
    public byte[] Reference { get; set; } = [];

    /// <summary>The <c>counted</c> column.</summary>
    public byte[][] Counted { get; set; } = [];

    /// <summary>The <c>sent_at</c> column.</summary>
    public DateTimeOffset SentAt { get; set; }

    /// <summary>The <c>settles_at</c> column.</summary>
    public DateTimeOffset SettlesAt { get; set; }
}
