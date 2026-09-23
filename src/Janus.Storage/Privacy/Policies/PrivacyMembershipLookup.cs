using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Policies;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.Policies;

/// <summary>
/// Which organizations a principal belongs to, over the <c>memberships</c> table.
/// </summary>
/// <param name="context">The context the read runs on.</param>
/// <remarks>
/// Implements AUTHZ-SCOPE-001 and CONV-DESIGN-003. A membership that has ended
/// carries no permission: the person is no longer under the organization's rules, and
/// the record of it stays for IDN-MEM-002 to read.
/// </remarks>
internal sealed class PrivacyMembershipLookup(StoreContext context) : IMembershipLookup
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<OrganizationId>> OfAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await context.Memberships
            .Where(membership => membership.Subject == subject && membership.EndedAt == null)
            .OrderBy(membership => membership.CreatedAt)
            .Select(membership => membership.Organization)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
