using System;
using Janus.Core;

namespace Janus.Storage.Identity.Identifiers;

/// <summary>
/// The <c>identifiers</c> row.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-004, PRIV-RIGHT-005a, PRIV-RIGHT-005c and CONV-DESIGN-003. Both
/// forms of the identifier are ciphertext: the one the person entered, which is shown
/// back to them, and the canonical one, which the fingerprint is derived from. What is
/// looked up is the fingerprint, so no plaintext identifier is in the database and no
/// column takes a collation (D-155).
/// </remarks>
internal sealed class IdentifierRecord
{
    /// <summary>
    /// The <c>identifier_id</c> column, which is this table's key.
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
    /// The <c>fingerprint</c> column: the keyed fingerprint of the canonical form, or
    /// the neutralised value where erasure has overwritten it.
    /// </summary>
    public byte[] Fingerprint { get; set; } = [];

    /// <summary>
    /// The <c>fingerprint_version</c> column: the version of the fingerprint key the
    /// fingerprint was computed under.
    /// </summary>
    public int FingerprintVersion { get; set; }

    /// <summary>
    /// The <c>canonicalisation_version</c> column: the Unicode version the canonical
    /// form the fingerprint was derived from was computed under.
    /// </summary>
    public string CanonicalisationVersion { get; set; } = string.Empty;

    /// <summary>
    /// The <c>enc_entered</c> column: the form the person entered.
    /// </summary>
    public byte[] Entered { get; set; } = [];

    /// <summary>
    /// The <c>enc_canonical</c> column: the form it is compared under.
    /// </summary>
    public byte[] Canonical { get; set; } = [];

    /// <summary>
    /// The <c>added_at</c> column.
    /// </summary>
    public DateTimeOffset AddedAt { get; set; }

    /// <summary>
    /// The <c>verified_at</c> column.
    /// </summary>
    public DateTimeOffset? VerifiedAt { get; set; }

    /// <summary>
    /// The <c>is_primary</c> column.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// The <c>is_locked</c> column.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// The <c>is_personal</c> column.
    /// </summary>
    public bool IsPersonal { get; set; }
}
