using System;
using Janus.Core;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// The <c>bulk_exports</c> row: when one actor's export operation was admitted.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-006. An actor's rows older than the hour the limit counts are
/// forgotten when the actor's next export is recorded.
/// </remarks>
internal sealed class BulkExportRecord
{
    /// <summary>The <c>id</c> column.</summary>
    public Guid Id { get; set; }

    /// <summary>The <c>actor</c> column: the person, where a person exported.</summary>
    public SubjectId? Actor { get; set; }

    /// <summary>The <c>principal</c> column: the system principal's name, where one exported.</summary>
    public string? Principal { get; set; }

    /// <summary>The <c>admitted_at</c> column.</summary>
    public DateTimeOffset AdmittedAt { get; set; }
}
