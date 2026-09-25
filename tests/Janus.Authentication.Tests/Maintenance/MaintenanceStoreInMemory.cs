using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Maintenance;
using Janus.Core;

namespace Janus.Authentication.Tests.Maintenance;

/// <summary>
/// The licences and permits and the maintenance log, held in memory.
/// </summary>
internal sealed class MaintenanceStoreInMemory : IMaintenanceStore
{
    private readonly List<Licence> _licences = [];

    /// <summary>
    /// Every entry appended, in the order it was.
    /// </summary>
    public List<MaintenanceEntry> Log { get; } = [];

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<Licence>> LicencesAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<Licence>>(
            [.. _licences.OrderBy(licence => licence.ExpiresAt).ThenBy(licence => licence.Id.Value)]);

    /// <inheritdoc/>
    public ValueTask ReplaceLicencesAsync(IReadOnlyList<Licence> licences, CancellationToken cancellationToken)
    {
        _licences.Clear();
        _licences.AddRange(licences!);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<MaintenanceEntry>> LogAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<MaintenanceEntry>>(
            [.. Log.OrderByDescending(entry => entry.PerformedAt).ThenByDescending(entry => entry.Id.Value)]);

    /// <inheritdoc/>
    public ValueTask RecordAsync(MaintenanceEntry entry, CancellationToken cancellationToken)
    {
        Log.Add(entry);

        return ValueTask.CompletedTask;
    }
}
