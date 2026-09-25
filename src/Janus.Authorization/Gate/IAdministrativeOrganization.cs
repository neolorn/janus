using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Which organization administers the deployment.
/// </summary>
/// <remarks>
/// Implements IDN-ORG-001, AUTHZ-SCOPE-001 and CONV-DESIGN-003. The organization is the
/// one bootstrap marks administrative, and nothing in the application marks another.
/// </remarks>
internal interface IAdministrativeOrganization
{
    /// <summary>
    /// The administrative organization.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The organization, or nothing before bootstrap has created it.</returns>
    ValueTask<OrganizationId?> FindAsync(CancellationToken cancellationToken);
}
