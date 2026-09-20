using System;
using Janus.Core;

namespace Janus.Storage.Privacy.Consents;

/// <summary>
/// The <c>objections</c> row.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-001a. An objection is recorded as a consent is, so the row
/// carries the same columns but the one that does not apply: nothing here is a
/// capture path, because the subject was never asked.
/// </remarks>
internal sealed class ObjectionRecordRow
{
    /// <summary>
    /// The <c>subject</c> column.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>purpose</c> column, as the host declared the purpose.
    /// </summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// The <c>notice_version</c> column: the version displayed when it was recorded.
    /// </summary>
    public string NoticeVersion { get; set; } = string.Empty;

    /// <summary>
    /// The <c>mechanism</c> column.
    /// </summary>
    public ConsentMechanism Mechanism { get; set; }

    /// <summary>
    /// The <c>recorded_at</c> column.
    /// </summary>
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>
    /// The <c>withdrawn_at</c> column, set rather than the row deleted.
    /// </summary>
    public DateTimeOffset? WithdrawnAt { get; set; }
}
