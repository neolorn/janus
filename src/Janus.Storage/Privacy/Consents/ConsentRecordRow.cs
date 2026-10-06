using System;
using Janus.Core;

namespace Janus.Storage.Privacy.Consents;

/// <summary>
/// The <c>consents</c> row.
/// </summary>
/// <remarks>
/// Implements PRIV-CONS-001, PRIV-CONS-002, PRIV-CONS-004, PRIV-CONS-007 and
/// PRIV-CONS-008. One row a grant, naming one purpose: a later grant is a row of its
/// own, and what ends a consent is written onto the row that was live.
/// </remarks>
internal sealed class ConsentRecordRow
{
    /// <summary>
    /// The <c>id</c> column: the row's own identifier, which no contract carries.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The <c>subject</c> column.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>purpose</c> column, as the host declared the purpose.
    /// </summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// The <c>document</c> column: the legal document it was given against.
    /// </summary>
    public string Document { get; set; } = string.Empty;

    /// <summary>
    /// The <c>notice_version</c> column: the version displayed when it was given.
    /// </summary>
    public string NoticeVersion { get; set; } = string.Empty;

    /// <summary>
    /// The <c>mechanism</c> column.
    /// </summary>
    public ConsentMechanism Mechanism { get; set; }

    /// <summary>
    /// The <c>kind</c> column: the ordinary or the written capture path.
    /// </summary>
    public ConsentKind Kind { get; set; }

    /// <summary>
    /// The <c>granted_at</c> column.
    /// </summary>
    public DateTimeOffset GrantedAt { get; set; }

    /// <summary>
    /// The <c>withdrawn_at</c> column, set rather than the row deleted.
    /// </summary>
    public DateTimeOffset? WithdrawnAt { get; set; }

    /// <summary>
    /// The <c>superseded_at</c> column, set by a material revision of the notice.
    /// </summary>
    public DateTimeOffset? SupersededAt { get; set; }
}
