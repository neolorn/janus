using System;
using Janus.Core;

namespace Janus.Storage.Identity.Records;

/// <summary>
/// The <c>person_record_documents</c> row.
/// </summary>
/// <remarks>Implements CONV-DESIGN-003.</remarks>
internal sealed class RecordDocumentsRow
{
    /// <summary>
    /// The <c>documents_id</c> column.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The <c>subject</c> column: the record the collection wrote.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>notice_version</c> column.
    /// </summary>
    public string NoticeVersion { get; set; } = string.Empty;

    /// <summary>
    /// The <c>terms_version</c> column.
    /// </summary>
    public string? TermsVersion { get; set; }

    /// <summary>
    /// The <c>recorded_at</c> column.
    /// </summary>
    public DateTimeOffset RecordedAt { get; set; }
}
