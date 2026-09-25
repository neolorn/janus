using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Mailboxes;

/// <summary>
/// The <c>mailboxes</c> row.
/// </summary>
/// <remarks>
/// Implements INT-MAIL-006, INT-MAIL-006a, INT-MAIL-007, PRIV-RIGHT-005a and
/// PRIV-RIGHT-005c. The row carries its own outstanding push and nothing of what the
/// mail server stores. The address is encrypted under its holder's data key, or under
/// a key of the row's own while nobody holds it, and is found by its fingerprint.
/// </remarks>
internal sealed class MailboxRecord
{
    /// <summary>
    /// The <c>id</c> column, which is this table's key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The <c>fingerprint</c> column.
    /// </summary>
    public byte[] Fingerprint { get; set; } = [];

    /// <summary>
    /// The <c>canonicalisation_version</c> column: the Unicode version the canonical
    /// form the fingerprint was derived from was computed under.
    /// </summary>
    public string CanonicalisationVersion { get; set; } = string.Empty;

    /// <summary>
    /// The <c>enc_canonical</c> column.
    /// </summary>
    public byte[] EncryptedCanonical { get; set; } = [];

    /// <summary>
    /// The <c>key_version</c> column: the key-encryption key the row's own key is
    /// wrapped under, while nobody holds the mailbox.
    /// </summary>
    public int? KeyVersion { get; set; }

    /// <summary>
    /// The <c>wrapped_key</c> column: the row's own key, while nobody holds the mailbox.
    /// </summary>
    public byte[]? WrappedKey { get; set; }

    /// <summary>
    /// The <c>reserved_at</c> column.
    /// </summary>
    public DateTimeOffset ReservedAt { get; set; }

    /// <summary>
    /// The <c>holder</c> column.
    /// </summary>
    public SubjectId? Holder { get; set; }

    /// <summary>
    /// The <c>retired_at</c> column.
    /// </summary>
    public DateTimeOffset? RetiredAt { get; set; }

    /// <summary>
    /// The <c>released_at</c> column.
    /// </summary>
    public DateTimeOffset? ReleasedAt { get; set; }

    /// <summary>
    /// The <c>pushed</c> column.
    /// </summary>
    public MailboxState? Pushed { get; set; }

    /// <summary>
    /// The <c>pending</c> column.
    /// </summary>
    public MailboxState? Pending { get; set; }

    /// <summary>
    /// The <c>pending_key</c> column.
    /// </summary>
    public Guid? PendingKey { get; set; }

    /// <summary>
    /// The <c>attempts</c> column.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// The <c>next_attempt_at</c> column.
    /// </summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    /// <summary>
    /// The <c>failed_at</c> column.
    /// </summary>
    public DateTimeOffset? FailedAt { get; set; }
}
