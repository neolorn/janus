using System.Collections.Generic;

namespace Janus.Hosting.Maintenance;

/// <summary>
/// The licences and permits as <c>GET /admin/compliance/licences</c> answers them,
/// soonest to lapse first.
/// </summary>
/// <param name="Licences">The licences and permits.</param>
/// <remarks>Implements OPS-MAINT-001 AC1.</remarks>
internal sealed record LicencesView(IReadOnlyList<LicenceView> Licences);
