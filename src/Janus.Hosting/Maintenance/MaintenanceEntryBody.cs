using System;

namespace Janus.Hosting.Maintenance;

/// <summary>
/// The body of <c>POST /admin/compliance/maintenance</c>: one task performed or review
/// made. Who performed it is the person asking, and never a member of the body.
/// </summary>
/// <param name="Task">Which task or review, as chapter 06 section 9 names it.</param>
/// <param name="PerformedAt">When it was performed, never later than now.</param>
/// <param name="Note">What the person notes, where they note anything.</param>
/// <remarks>Implements OPS-MAINT-001 (D-153).</remarks>
internal sealed record MaintenanceEntryBody(string? Task, DateTimeOffset? PerformedAt, string? Note);
