using System;
using Janus.Core;

namespace Janus.Storage.Identity.Records;

/// <summary>
/// The <c>person_record_references</c> row.
/// </summary>
/// <remarks>Implements CONV-DESIGN-003.</remarks>
internal sealed class RecordReferenceRow
{
    /// <summary>
    /// The <c>reference_id</c> column.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The <c>subject</c> column: the record the reference belongs to.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>fingerprint</c> column.
    /// </summary>
    public byte[] Fingerprint { get; set; } = [];

    /// <summary>
    /// The <c>fingerprint_version</c> column.
    /// </summary>
    public int FingerprintVersion { get; set; }

    /// <summary>
    /// The <c>added_at</c> column.
    /// </summary>
    public DateTimeOffset AddedAt { get; set; }
}
