using System;
using Janus.Authentication.Maintenance;
using Janus.Core;

namespace Janus.Storage.Authentication.Maintenance;

/// <summary>
/// The <c>maintenance_log</c> row: one task performed or review made.
/// </summary>
/// <remarks>
/// Implements OPS-MAINT-001 (D-153). The application appends and reads these rows and
/// holds no right to change or remove one.
/// </remarks>
internal sealed class MaintenanceEntryRecord
{
    /// <summary>The <c>id</c> column, which is this table's key.</summary>
    public MaintenanceEntryId Id { get; set; }

    /// <summary>The <c>task</c> column.</summary>
    public MaintenanceTask Task { get; set; }

    /// <summary>The <c>performed_at</c> column.</summary>
    public DateTimeOffset PerformedAt { get; set; }

    /// <summary>The <c>actor</c> column: the subject who performed it.</summary>
    public SubjectId Actor { get; set; }

    /// <summary>The <c>note</c> column, absent where nothing was noted.</summary>
    public string? Note { get; set; }
}
