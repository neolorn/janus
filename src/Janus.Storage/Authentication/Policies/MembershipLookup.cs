using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Policies;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Policies;

/// <summary>
/// Which organizations a principal belongs to, over the <c>memberships</c> table.
/// </summary>
/// <param name="context">The context the read runs on.</param>
/// <remarks>
/// Implements AUTH-PRIN-002 and CONV-DESIGN-003. A membership that has ended resolves
/// no policy: the person is no longer under the organization's rules, and the record
/// of it stays for IDN-MEM-002 to read.
/// </remarks>
internal sealed class MembershipLookup(JanusDbContext context) : IMembershipLookup
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
