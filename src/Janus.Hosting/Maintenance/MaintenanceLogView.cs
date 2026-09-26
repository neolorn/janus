using System.Collections.Generic;

namespace Janus.Hosting.Maintenance;

/// <summary>
/// The maintenance log as <c>GET /admin/compliance/maintenance</c> answers it, most
/// recently performed first.
/// </summary>
/// <param name="Entries">The entries.</param>
/// <remarks>Implements OPS-MAINT-001 AC3.</remarks>
internal sealed record MaintenanceLogView(IReadOnlyList<MaintenanceEntryView> Entries);
