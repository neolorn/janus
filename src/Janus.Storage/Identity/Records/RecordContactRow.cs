using System;
using Janus.Core;

namespace Janus.Storage.Identity.Records;

/// <summary>
/// The <c>person_record_contacts</c> row.
/// </summary>
/// <remarks>Implements CONV-DESIGN-003.</remarks>
internal sealed class RecordContactRow
{
    /// <summary>
    /// The <c>subject</c> column: the record the contact belongs to.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>kind</c> column.
    /// </summary>
    public IdentifierKind Kind { get; set; }

    /// <summary>
    /// The <c>fingerprint</c> column.
    /// </summary>
    public byte[] Fingerprint { get; set; } = [];

    /// <summary>
    /// The <c>fingerprint_version</c> column.
    /// </summary>
    public int FingerprintVersion { get; set; }

    /// <summary>
    /// The <c>canonicalisation_version</c> column.
    /// </summary>
    public string CanonicalisationVersion { get; set; } = string.Empty;

    /// <summary>
    /// The <c>enc_value</c> column, under the record's key.
    /// </summary>
    public byte[] Value { get; set; } = [];

    /// <summary>
    /// The <c>verified_at</c> column.
    /// </summary>
    public DateTimeOffset? VerifiedAt { get; set; }
}
