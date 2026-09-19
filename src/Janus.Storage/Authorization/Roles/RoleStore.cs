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
/// Roles, over the <c>roles</c> and <c>role_permissions</c> tables.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-004, AUTHZ-CACHE-001 and CONV-DESIGN-003. Editing a role is
/// rows added and removed under its name; no counter is raised, because what a role
/// allows is read wherever a grant naming it is evaluated.
/// </remarks>
internal sealed class RoleStore(JanusDbContext context) : IRoleStore
{
    /// <inheritdoc/>
    public async ValueTask<Role?> FindAsync(RoleName name, CancellationToken cancellationToken)
    {
        RoleRecord? role = await context.Roles
            .FirstOrDefaultAsync(row => row.Name == name, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            return null;
        }

        List<Permission> permissions = await context.RolePermissions
            .Where(row => row.Role == name)
            .Select(row => row.Permission)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Role.Of(name, permissions);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Role>> AllAsync(CancellationToken cancellationToken)
    {
        List<RoleRecord> roles = await context.Roles
            .OrderBy(row => row.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<RolePermissionRecord> permissions = await context.RolePermissions
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. roles.Select(role => Role.Of(
                role.Name,
                permissions.Where(row => row.Role == role.Name).Select(row => row.Permission))),
        ];
    }

    /// <inheritdoc/>
    public async ValueTask CreateAsync(Role role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);

        await context.Roles
            .AddAsync(new RoleRecord { Name = role.Name }, cancellationToken)
            .ConfigureAwait(false);

        await context.RolePermissions
            .AddRangeAsync(Rows(role), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(Role role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);

        bool exists = await context.Roles
            .AnyAsync(row => row.Name == role.Name, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            throw new InvalidOperationException("The role has no row to carry it onto.");
        }

        List<RolePermissionRecord> stored = await context.RolePermissions
            .Where(row => row.Role == role.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        context.RolePermissions.RemoveRange(
            stored.Where(row => !role.Allows(row.Permission)));

        await context.RolePermissions
            .AddRangeAsync(
                Rows(role).Where(row => !stored.Exists(held => held.Permission == row.Permission)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(RoleName name, CancellationToken cancellationToken)
    {
        RoleRecord? role = await context.Roles
            .FirstOrDefaultAsync(row => row.Name == name, cancellationToken)
            .ConfigureAwait(false);

        if (role is not null)
        {
            context.Roles.Remove(role);
        }
    }

    private static IEnumerable<RolePermissionRecord> Rows(Role role) =>
        role.Permissions.Select(permission => new RolePermissionRecord
        {
            Role = role.Name,
            Permission = permission,
        });
}
