using System;
using System.Globalization;

namespace Janus.Authentication.Maintenance;

/// <summary>
/// What one entry of the maintenance log is held under.
/// </summary>
/// <param name="Value">The identifier.</param>
/// <remarks>Implements OPS-MAINT-001.</remarks>
internal readonly record struct MaintenanceEntryId(Guid Value)
{
    /// <summary>
    /// A new identifier, ordered by when the entry was recorded.
    /// </summary>
    /// <param name="recordedAt">When the entry was recorded.</param>
    /// <returns>The identifier.</returns>
    public static MaintenanceEntryId Of(DateTimeOffset recordedAt) =>
        new(Guid.CreateVersion7(recordedAt));

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
