using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Configuration;
using Janus.Core.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Settings;

/// <summary>
/// The value of a protected key, over the <c>settings</c> table.
/// </summary>
/// <param name="context">The context the writes are tracked on.</param>
/// <remarks>
/// Implements OPS-CFG-004 and D-071. The configuration store refuses a protected key,
/// so this is the one other writer of the table, reached by bootstrap and by the
/// change of a protected key from the server and by nothing the application serves.
/// </remarks>
internal sealed class ProtectedSettings(StoreContext context) : IProtectedSettings
{
    /// <inheritdoc/>
    public async ValueTask<string?> WriteAsync(ConfigurationKey key, string written, CancellationToken cancellationToken)
    {
        SettingRecord? record = await context.Settings.FindAsync([key], cancellationToken).ConfigureAwait(false);
        string? before = record?.Value;

        if (record is null)
        {
            record = new SettingRecord { Key = key };
            context.Settings.Add(record);
        }

        record.Value = written;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return before;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlySet<ConfigurationKey>> HeldAsync(CancellationToken cancellationToken) =>
        (await context.Settings
            .Select(record => record.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
        .ToHashSet();
}
