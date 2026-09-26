using System;
using Janus.Core;

namespace Janus.Storage.Identity.Identifiers;

/// <summary>
/// The <c>identifier_removals</c> row.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-006, PRIV-RIGHT-005a and CONV-DESIGN-003. The row holds the
/// value out of another account's reach while the undo is good for it, so its
/// fingerprint is carried here exactly as the identifiers table carried it.
/// </remarks>
internal sealed class IdentifierRemovalRecord
{
    /// <summary>
    /// The <c>identifier_id</c> column, which is this table's key and the identifier
    /// the restored one is again.
    /// </summary>
    public IdentifierId Id { get; set; }

    /// <summary>
    /// The subject column, which both encrypted columns name as their subject.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>kind</c> column.
    /// </summary>
    public IdentifierKind Kind { get; set; }

    /// <summary>
    /// The <c>fingerprint</c> column: the keyed fingerprint of the canonical form.
    /// </summary>
    public byte[] Fingerprint { get; set; } = [];

    /// <summary>
    /// The <c>fingerprint_version</c> column: the version of the fingerprint key the
    /// fingerprint was computed under.
    /// </summary>
    public int FingerprintVersion { get; set; }

    /// <summary>
    /// The <c>enc_entered</c> column: the form the person entered.
    /// </summary>
    public byte[] Entered { get; set; } = [];

    /// <summary>
    /// The <c>enc_canonical</c> column: the form it is compared under.
    /// </summary>
    public byte[] Canonical { get; set; } = [];

    /// <summary>
    /// The <c>is_locked</c> column.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// The <c>added_at</c> column.
    /// </summary>
    public DateTimeOffset AddedAt { get; set; }

    /// <summary>
    /// The <c>verified_at</c> column.
    /// </summary>
    public DateTimeOffset VerifiedAt { get; set; }

    /// <summary>
    /// The <c>removed_at</c> column.
    /// </summary>
    public DateTimeOffset RemovedAt { get; set; }

    /// <summary>
    /// The <c>expires_at</c> column: when the undo stops working and the value is
    /// released.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// The <c>undo_fingerprint</c> column: what the token in the undo link answers to.
    /// </summary>
    public byte[] Undo { get; set; } = [];
}
