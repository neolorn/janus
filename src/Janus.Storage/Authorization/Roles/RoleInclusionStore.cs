using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Roles;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// Roles' inclusions of one another, over the <c>role_inclusions</c> table, and the
/// host's actions declared not inherited, over the <c>uninherited_permissions</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-006, AUTHZ-CACHE-001 and CONV-DESIGN-003 (D-278). Nothing is
/// copied into a role: the view a grant is read through follows the rows here at each
/// request, so a change reaches every holder at the next one.
/// </remarks>
internal sealed class RoleInclusionStore(StoreContext context) : IRoleInclusionStore
{
    // The one name every change to an inclusion is held under, since a loop is a matter
    // of the whole graph rather than of one row.
    private const string Held = "identity.role_inclusions";

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask HoldAsync(CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("The inclusions are held only inside the operation's transaction.");
        }

        _ = await context.Database
            .ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({Held}, 0))", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<(RoleName Role, RoleInclusion Inclusion)>> AllAsync(
        CancellationToken cancellationToken)
    {
        // Read untracked, so the inclusions read again under the lock are the rows as
        // committed and never ones an earlier read left in the context.
        List<RoleInclusionRecord> rows = await context.RoleInclusions
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows
                .OrderBy(row => row.Role.ToString(), StringComparer.Ordinal)
                .ThenBy(row => row.Included.ToString(), StringComparer.Ordinal)
                .Select(row => (row.Role, Read(row))),
        ];
    }

    /// <inheritdoc/>
    public async ValueTask CreateAsync(RoleName role, RoleInclusion inclusion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inclusion);

        var row = new RoleInclusionRecord { Role = role, Included = inclusion.Included };

        Write(row, inclusion);

        await context.RoleInclusions.AddAsync(row, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No such row exists.</exception>
    public async ValueTask RecordAsync(RoleName role, RoleInclusion inclusion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inclusion);

        RoleInclusionRecord row = await context.RoleInclusions
                .FirstOrDefaultAsync(
                    row => row.Role == role && row.Included == inclusion.Included,
                    cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The inclusion has no row to carry it onto.");

        Write(row, inclusion);
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(RoleName role, RoleName included, CancellationToken cancellationToken)
    {
        RoleInclusionRecord? row = await context.RoleInclusions
            .FirstOrDefaultAsync(row => row.Role == role && row.Included == included, cancellationToken)
            .ConfigureAwait(false);

        if (row is not null)
        {
            context.RoleInclusions.Remove(row);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DeclareUninheritedAsync(
        IReadOnlySet<Permission> permissions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        string[] declared = [.. permissions.Select(permission => permission.ToString()).Order(StringComparer.Ordinal)];

        // Two starts at once each write the same declaration, so a row the other wrote
        // first is left as it stands.
        _ = await context.Database
            .ExecuteSqlAsync(
                $"""
                DELETE FROM identity.uninherited_permissions
                WHERE permission <> ALL({declared});
                INSERT INTO identity.uninherited_permissions (permission)
                SELECT unnest({declared})
                ON CONFLICT DO NOTHING;
                """,
                cancellationToken)
            .ConfigureAwait(false);
    }

    // A name the row holds was a permission or a role name when it was written, so it
    // reads as one.
    private static RoleInclusion Read(RoleInclusionRecord row) =>
        new(
            row.Included,
            row.Enabled,
            [.. row.LeftOutPermissions.Select(Permission.Parse)],
            [.. row.LeftOutHandsOut.Select(RoleName.Parse)]);

    // The names are kept in one order, so a list written twice reads as one.
    private static void Write(RoleInclusionRecord row, RoleInclusion inclusion)
    {
        row.Enabled = inclusion.Enabled;
        row.LeftOutPermissions =
            [.. inclusion.LeftOutPermissions.Select(permission => permission.ToString()).Distinct().Order(StringComparer.Ordinal)];
        row.LeftOutHandsOut =
            [.. inclusion.LeftOutHandsOut.Select(handed => handed.ToString()).Distinct().Order(StringComparer.Ordinal)];
    }
}
