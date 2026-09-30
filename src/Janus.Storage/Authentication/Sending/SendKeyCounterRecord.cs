using System;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The <c>send_key_counters</c> row: one account, source, global or host key and the
/// times counted against it, and nothing else.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-004 and PRIV-RET-005. A destination is kept apart, in
/// <c>send_counters</c>, so that it is swept by the longest interval of the destination
/// restrictions alone and never outlives them because a restriction on another key
/// counts over a longer one. The key is an HMAC of the restriction name and the value,
/// as it is there.
/// </remarks>
internal sealed class SendKeyCounterRecord
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
    /// was last counted and what the sweep reads.
    /// </summary>
    public DateTimeOffset[] SentAt { get; set; } = [];
}
