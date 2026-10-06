using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Roles;

/// <summary>
/// Whether anything the deployment keeps names a role, which is what keeps the role in
/// place.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-004, AUTHZ-GRANT-003 AC3, REG-INV-001 and CONV-DESIGN-003. A
/// grant's history names its role, and a standing invitation becomes grants of its
/// roles at the acknowledgement; neither is left naming nothing.
/// </remarks>
internal interface IRoleReferences
{
    /// <summary>
    /// Whether any grant, live, expired or revoked, or any standing invitation, neither
    /// acknowledged nor revoked, expired or not, names the role.
    /// </summary>
    /// <param name="role">Which role.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether one does.</returns>
    ValueTask<bool> NamedAsync(RoleName role, CancellationToken cancellationToken);
}
