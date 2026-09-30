using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Maintenance;
using Janus.Core;

namespace Janus.Authentication.Tests.Maintenance;

/// <summary>
/// The licences and permits and the maintenance log, held in memory, with the
/// completed rotations and the bootstrap the audit trail would record.
/// </summary>
internal sealed class MaintenanceStoreInMemory : IMaintenanceStore
{
    private readonly List<Licence> _licences = [];

    /// <summary>
    /// Every entry appended, in the order it was.
    /// </summary>
    public List<MaintenanceEntry> Log { get; } = [];

    /// <summary>
    /// Every completed rotation, of either key, as its <c>ops.keyrotation.completed</c>
    /// record states it: the kind as chapter 10 section 5.41 spells it.
    /// </summary>
    public List<(string Kind, int Version, DateTimeOffset CompletedAt)> Rotations { get; } = [];

    /// <summary>
    /// When bootstrap created the first organization, where it has.
    /// </summary>
    public DateTimeOffset? Bootstrapped { get; set; }

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

    /// <inheritdoc/>
    public ValueTask<(int Version, DateTimeOffset CompletedAt)?> KeyEncryptionKeyRotatedAsync(
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            Rotations
                .Where(rotation => rotation.Kind == "key-encryption-key")
                .OrderByDescending(rotation => rotation.CompletedAt)
                .Select(rotation => ((int Version, DateTimeOffset CompletedAt)?)(rotation.Version, rotation.CompletedAt))
                .FirstOrDefault());

    /// <inheritdoc/>
    public ValueTask<DateTimeOffset?> BootstrappedAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Bootstrapped);
}
