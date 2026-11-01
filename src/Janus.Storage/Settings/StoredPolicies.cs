using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Settings;

/// <summary>
/// The system policy and the organization overrides as the settings table holds them,
/// before any read has passed over what they name.
/// </summary>
/// <param name="context">The context the rows are read through.</param>
/// <remarks>
/// Implements chapter 10 section 5a (D-235, D-238). Only the start reads these, to tell
/// the operator which stored policies still name a retired step-up action; every other
/// read goes through the configuration store.
/// </remarks>
internal sealed class StoredPolicies(StoreContext context)
{
    /// <summary>
    /// Every stored policy, by its key, in the order of the keys.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Each policy's key and the text stored under it.</returns>
    public async ValueTask<IReadOnlyList<(string Key, string Stored)>> AllAsync(CancellationToken cancellationToken)
    {
        string system = Core.Configuration.Settings.PolicyDefault.Key.ToString();
        string overrides = Core.Configuration.Settings.OrganizationPolicy.Prefix + ".";

        List<SettingRecord> rows = await context.Settings
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows
                .Select(row => (Key: row.Key.ToString(), Stored: row.Value))
                .Where(row => string.Equals(row.Key, system, StringComparison.Ordinal)
                    || row.Key.StartsWith(overrides, StringComparison.Ordinal))
                .OrderBy(row => row.Key, StringComparer.Ordinal),
        ];
    }
}
