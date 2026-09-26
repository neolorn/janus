using System;
using Janus.Core;

namespace Janus.Authentication.Maintenance;

/// <summary>
/// One dated entry of the maintenance log: a task performed or a review made, and who
/// did it.
/// </summary>
/// <param name="Id">What it is held under.</param>
/// <param name="Task">Which task or review.</param>
/// <param name="PerformedAt">When it was performed.</param>
/// <param name="Actor">Who performed it.</param>
/// <param name="Note">What they noted, where they noted anything.</param>
/// <remarks>Implements OPS-MAINT-001 (D-153).</remarks>
internal sealed record MaintenanceEntry(
    MaintenanceEntryId Id,
    MaintenanceTask Task,
    DateTimeOffset PerformedAt,
    SubjectId Actor,
    string? Note);
