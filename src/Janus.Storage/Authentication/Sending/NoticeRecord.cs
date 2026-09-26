using System;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The <c>nonexistence_notices</c> row: one telling of one address that no account
/// holds it.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-003. The address is held by its hash: the notice exists to
/// protect the mailbox, not to record who asked about it.
/// </remarks>
internal sealed class NoticeRecord
{
    /// <summary>The <c>id</c> column, which is this table key.</summary>
    public Guid Id { get; set; }

    /// <summary>The <c>destination</c> column.</summary>
    public byte[] Destination { get; set; } = [];

    /// <summary>
    /// The <c>fingerprint_version</c> column: the version of the fingerprint key the
    /// destination is hashed under.
    /// </summary>
    public int FingerprintVersion { get; set; }

    /// <summary>The <c>at</c> column.</summary>
    public DateTimeOffset At { get; set; }
}
