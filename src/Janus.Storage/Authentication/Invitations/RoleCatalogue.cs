using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Authorization.Roles;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Invitations;

/// <summary>
/// The roles an invitation names, read as a grant of each admits: through the
/// authorization area's store, with what each carries and hands out through its
/// inclusions.
/// </summary>
/// <param name="context">The context the operation reads through.</param>
/// <param name="roles">Where the roles are read.</param>
/// <remarks>
/// Implements REG-INV-001, AUTHZ-GRANT-004, AUTHZ-GRANT-006 and CONV-LAYOUT-001. The rows
/// are the authorization area's; this reads them and writes nothing.
/// </remarks>
internal sealed class RoleCatalogue(StoreContext context, IRoleStore roles) : IRoleCatalogue
{
    /// <inheritdoc/>
    public async ValueTask<DefinedRole?> FindAsync(RoleName name, CancellationToken cancellationToken) =>
        await roles.EffectiveAsync(name, cancellationToken).ConfigureAwait(false) is Role role
            ? new DefinedRole(name, [.. role.Permissions], [.. role.HandsOut], role.GrantsMustEnd)
            : null;

    /// <inheritdoc/>
    /// <exception cref="System.InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<DefinedRole?> FindForUpdateAsync(RoleName name, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new System.InvalidOperationException("A role's row is held only inside the operation's transaction.");
        }

        return (await context.Roles
                .FromSql($"SELECT * FROM identity.roles WHERE name = {name.ToString()} FOR UPDATE")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Count is 0
            ? null
            : await FindAsync(name, cancellationToken).ConfigureAwait(false);
    }
}
