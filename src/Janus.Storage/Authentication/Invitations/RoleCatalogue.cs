using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Authorization.Roles;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Invitations;

/// <summary>
/// The roles an invitation names, read as a grant of each is judged: through the
/// authorization area's roles, with what each carries and hands out through its
/// inclusions as though they were on.
/// </summary>
/// <param name="context">The context the operation reads through.</param>
/// <param name="graphs">Where the roles and their inclusions are read.</param>
/// <remarks>
/// Implements REG-INV-001, AUTHZ-GRANT-004, AUTHZ-GRANT-006 and CONV-LAYOUT-001. The rows
/// are the authorization area's; this reads them and writes nothing. An invitation hands
/// out the roles it names, so each is judged whatever <c>authz.roleinclusion.enabled</c>
/// reads, as a grant of it is (D-278).
/// </remarks>
internal sealed class RoleCatalogue(StoreContext context, RoleGraphs graphs) : IRoleCatalogue
{
    /// <inheritdoc/>
    public async ValueTask<DefinedRole?> FindAsync(RoleName name, CancellationToken cancellationToken)
    {
        RoleGraph graph = await graphs.ReadFollowingAsync(cancellationToken).ConfigureAwait(false);

        return graph.Find(name) is Role role ? graph.Judged(role) : null;
    }

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
