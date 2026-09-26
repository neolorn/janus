using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Maintenance;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Maintenance;

/// <summary>
/// The licences and permits and the maintenance log, over the <c>licences</c> and
/// <c>maintenance_log</c> tables.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>Implements OPS-MAINT-001 and CONV-DESIGN-003.</remarks>
internal sealed class MaintenanceStore(StoreContext context) : IMaintenanceStore
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Licence>> LicencesAsync(CancellationToken cancellationToken)
    {
        List<LicenceRecord> rows = await context.Licences
            .AsNoTracking()
            .OrderBy(licence => licence.ExpiresAt)
            .ThenBy(licence => licence.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(row => new Licence(row.Id, row.Kind, row.Name, row.ExpiresAt, row.RenewedAt))];
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The list is absent.</exception>
    public async ValueTask ReplaceLicencesAsync(
        IReadOnlyList<Licence> licences,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(licences);

        Dictionary<LicenceId, LicenceRecord> held = await context.Licences
            .ToDictionaryAsync(licence => licence.Id, cancellationToken)
            .ConfigureAwait(false);

        foreach (LicenceRecord gone in held.Values.Where(row => !licences.Any(licence => licence.Id == row.Id)))
        {
            context.Licences.Remove(gone);
        }

        foreach (Licence licence in licences)
        {
            if (!held.TryGetValue(licence.Id, out LicenceRecord? row))
            {
                row = new LicenceRecord { Id = licence.Id };
                await context.Licences.AddAsync(row, cancellationToken).ConfigureAwait(false);
            }

            row.Kind = licence.Kind;
            row.Name = licence.Name;
            row.ExpiresAt = licence.ExpiresAt;
            row.RenewedAt = licence.RenewedAt;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<MaintenanceEntry>> LogAsync(CancellationToken cancellationToken)
    {
        List<MaintenanceEntryRecord> rows = await context.MaintenanceLog
            .AsNoTracking()
            .OrderByDescending(entry => entry.PerformedAt)
            .ThenByDescending(entry => entry.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(row => new MaintenanceEntry(row.Id, row.Task, row.PerformedAt, row.Actor, row.Note))];
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The entry is absent.</exception>
    public async ValueTask RecordAsync(MaintenanceEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await context.MaintenanceLog
            .AddAsync(
                new MaintenanceEntryRecord
                {
                    Id = entry.Id,
                    Task = entry.Task,
                    PerformedAt = entry.PerformedAt,
                    Actor = entry.Actor,
                    Note = entry.Note,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }
}
