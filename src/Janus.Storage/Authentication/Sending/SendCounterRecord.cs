using System;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The <c>send_counters</c> row: one restriction key and the times counted against
/// it, and nothing else.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-004. The plain address is never here: the key is an HMAC of
/// the restriction name and the value, so a dump of the table yields no address, and
/// the row is deleted once its times have all aged out.
/// </remarks>
internal sealed class SendCounterRecord
{
    /// <summary>The <c>key</c> column, which is this table key.</summary>
    public byte[] Key { get; set; } = [];

    /// <summary>The <c>sent_at</c> column.</summary>
    public DateTimeOffset[] SentAt { get; set; } = [];
}
