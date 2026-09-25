using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// Which organization administers the deployment, over the <c>organizations</c> table.
/// </summary>
/// <param name="context">The context the read runs on.</param>
/// <remarks>
/// Implements IDN-ORG-001, AUTHZ-SCOPE-001 and CONV-DESIGN-003. The unique partial
/// index on the mark admits one such row, so a second is never read.
/// </remarks>
internal sealed class GateAdministrativeOrganization(StoreContext context) : IAdministrativeOrganization
{
    /// <inheritdoc/>
    public async ValueTask<OrganizationId?> FindAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<OrganizationId> marked = await context.Organizations
            .Where(organization => organization.IsAdministrative)
            .Select(organization => organization.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return marked is [OrganizationId administrative] ? administrative : null;
    }
}
