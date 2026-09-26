using System;
using Janus.Authentication;
using Janus.Authentication.Maintenance;

namespace Janus.Hosting.Maintenance;

/// <summary>
/// One entry of the maintenance log as the response carries it.
/// </summary>
/// <param name="Task">Which task or review.</param>
/// <param name="PerformedAt">When it was performed.</param>
/// <param name="Actor">The subject who performed it.</param>
/// <param name="Note">What they noted, where they noted anything.</param>
/// <remarks>Implements OPS-MAINT-001 (D-153).</remarks>
internal sealed record MaintenanceEntryView(
    string Task,
    DateTimeOffset PerformedAt,
    Guid Actor,
    string? Note)
{
    /// <summary>
    /// The view of one entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The entry is absent.</exception>
    public static MaintenanceEntryView Of(MaintenanceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new(
            WrittenName.Of(entry.Task),
            entry.PerformedAt,
            entry.Actor.Value,
            entry.Note);
    }
}
