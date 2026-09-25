using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Invitations;

/// <summary>
/// The roles an invitation names, read from the <c>roles</c> and
/// <c>role_permissions</c> tables.
/// </summary>
/// <param name="context">The context the operation reads through.</param>
/// <remarks>
/// Implements REG-INV-001, AUTHZ-GRANT-004 and CONV-LAYOUT-001. The rows are the
/// authorization area's; this reads them and writes nothing.
/// </remarks>
internal sealed class RoleCatalogue(StoreContext context) : IRoleCatalogue
{
    /// <inheritdoc/>
    public async ValueTask<DefinedRole?> FindAsync(RoleName name, CancellationToken cancellationToken)
    {
        if (!await context.Roles
                .AnyAsync(row => row.Name == name, cancellationToken)
                .ConfigureAwait(false))
        {
            return null;
        }

        List<Permission> permissions = await context.RolePermissions
            .Where(row => row.Role == name)
            .Select(row => row.Permission)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DefinedRole(name, permissions);
    }
}
