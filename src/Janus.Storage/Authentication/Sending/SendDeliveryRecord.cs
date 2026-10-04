using System;
using Janus.Authentication.Sending;
using Janus.Core;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The <c>send_outbox</c> row: one message the library has admitted and the handler has
/// not taken.
/// </summary>
/// <remarks>
/// Implements D-022, AUTH-ABUSE-004, INF-BG-001, IDN-PRIN-003 and PRIV-RIGHT-005a. The
/// row is written in the transaction that undertook the message and removed once the
/// handler has taken it or it has failed for good, so a message is neither lost with the
/// process that undertook it nor kept after it has been carried. The data key is the
/// row's own, because a send may name a subject that holds no key yet (a registration in
/// progress) or no subject at all (a notice to an address no account holds).
/// </remarks>
internal sealed class SendDeliveryRecord
{
    /// <summary>The <c>id</c> column, which is this table's key.</summary>
    public SendDeliveryId Id { get; set; }

    /// <summary>The <c>recorded_at</c> column.</summary>
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>The <c>attempts</c> column.</summary>
    public int Attempts { get; set; }

    /// <summary>The <c>next_attempt_at</c> column, which the publisher reads.</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>
    /// The <c>claimed_until</c> column: when the claim of the attempt that holds the row
    /// times out, or nothing where no attempt holds it (CONV-DESIGN-003).
    /// </summary>
    public DateTimeOffset? ClaimedUntil { get; set; }

    /// <summary>
    /// The <c>subject</c> column, which the encrypted column names as its subject,
    /// and which is absent where the message concerns no account.
    /// </summary>
    public SubjectId? Subject { get; set; }

    /// <summary>
    /// The <c>wrapped_key</c> column: the row's data key, wrapped under the deployment's
    /// data key.
    /// </summary>
    public byte[] WrappedKey { get; set; } = [];

    /// <summary>
    /// The <c>enc_message</c> column: the whole of what is to be sent, the reference it
    /// is carried under included.
    /// </summary>
    public byte[] Message { get; set; } = [];
}
