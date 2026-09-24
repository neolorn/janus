using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// Whether an organization is suspended, over the <c>organizations</c> table.
/// </summary>
/// <param name="context">The context the row is read on.</param>
/// <remarks>
/// Implements IDN-ORG-003 and CONV-DESIGN-003 (entry 266). It reads the one column the
/// gate evaluates and nothing else of the organization.
/// </remarks>
internal sealed class OrganizationSuspensions(StoreContext context) : IOrganizationSuspensions
{
    /// <inheritdoc/>
    public async ValueTask<bool> IsSuspendedAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        await context.Organizations
            .AsNoTracking()
            .AnyAsync(
                row => row.Id == organization && row.DeletionRequestedAt != null,
                cancellationToken)
            .ConfigureAwait(false);
}
