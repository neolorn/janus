using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Invitations;

/// <summary>
/// Where the roles an invitation names are looked up.
/// </summary>
/// <remarks>
/// Implements REG-INV-001, AUTHZ-GRANT-004 and CONV-LAYOUT-001. The roles are the
/// authorization area's; this is what an invitation asks of them, and nothing here
/// writes one.
/// </remarks>
internal interface IRoleCatalogue
{
    /// <summary>
    /// Reads one role with what it permits.
    /// </summary>
    /// <param name="name">Which role.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The role, or nothing where the deployment defines no such role.</returns>
    ValueTask<DefinedRole?> FindAsync(RoleName name, CancellationToken cancellationToken);
}
