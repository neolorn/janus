using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Maintenance;

/// <summary>
/// Where the licences and permits and the maintenance log are kept.
/// </summary>
/// <remarks>
/// Implements OPS-MAINT-001 and CONV-DESIGN-003. The log is appended to and read,
/// and nothing removes or changes an entry.
/// </remarks>
internal interface IMaintenanceStore
{
    /// <summary>
    /// The licences and permits, soonest to lapse first.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The licences and permits.</returns>
    ValueTask<IReadOnlyList<Licence>> LicencesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the licences and permits with the list given, on the transaction in
    /// progress.
    /// </summary>
    /// <param name="licences">What now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of replacing them.</returns>
    ValueTask ReplaceLicencesAsync(IReadOnlyList<Licence> licences, CancellationToken cancellationToken);

    /// <summary>
    /// The maintenance log, most recently performed first.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The entries.</returns>
    ValueTask<IReadOnlyList<MaintenanceEntry>> LogAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Appends one entry to the log, on the transaction in progress.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of appending it.</returns>
    ValueTask RecordAsync(MaintenanceEntry entry, CancellationToken cancellationToken);
}
