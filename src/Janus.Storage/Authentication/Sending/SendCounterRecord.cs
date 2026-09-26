using System;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The <c>send_counters</c> row: one restriction key and the times counted against
/// it, and nothing else.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-004. The plain address is never here: the key is an HMAC of
/// the restriction name and the value, so a dump of the table yields no address. Nothing
/// derived is held beside them: the row is deleted when its newest time is older than
/// the longest interval any restriction now declares, so a tightened interval reaches
/// the sends already counted.
/// </remarks>
internal sealed class SendCounterRecord
{
    /// <summary>The <c>key</c> column, which is this table key.</summary>
    public byte[] Key { get; set; } = [];

    /// <summary>
    /// The <c>fingerprint_version</c> column: the version of the fingerprint key the
    /// key is hashed under.
    /// </summary>
    public int FingerprintVersion { get; set; }

    /// <summary>
    /// The <c>sent_at</c> column, oldest first, so the last of them is when the key
    /// was last sent to and what the sweep reads.
    /// </summary>
    public DateTimeOffset[] SentAt { get; set; } = [];
}
