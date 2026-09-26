using System;
using Janus.Authentication.Sending;
using Janus.Core;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The <c>send_outbox</c> row: one message the library has undertaken to send and no
/// transport has taken in full.
/// </summary>
/// <remarks>
/// Implements D-022, INF-BG-001, IDN-PRIN-003 and PRIV-RIGHT-005a. The row is written in
/// the transaction that made the message necessary and removed once a transport has
/// taken it or its retry budget is spent, so a message is neither lost with the process
/// that undertook it nor kept after it has been carried. The data key is the row's own, because a send may name a
/// subject that holds no key yet (a registration in progress) or no subject at all (a
/// notice to an address no account holds).
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
    /// The <c>taken_languages</c> column, a JSON array of the languages a transport has
    /// taken the message in.
    /// </summary>
    public string TakenLanguages { get; set; } = "[]";

    /// <summary>
    /// The <c>subject</c> column, which the encrypted column names as its subject,
    /// and which is absent where the message concerns no account.
    /// </summary>
    public SubjectId? Subject { get; set; }

    /// <summary>
    /// The <c>key_version</c> column: the key-encryption key version the data key is
    /// wrapped under.
    /// </summary>
    public int KeyVersion { get; set; }

    /// <summary>The <c>wrapped_key</c> column: the row's data key, wrapped.</summary>
    public byte[] WrappedKey { get; set; } = [];

    /// <summary>The <c>enc_message</c> column: the whole of what is to be sent.</summary>
    public byte[] Message { get; set; } = [];
}
