using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Accounts;

/// <summary>
/// Whether a person is tied to an organization, read from their memberships, the records
/// registered in the organization and the person records it holds.
/// </summary>
/// <param name="context">The store.</param>
/// <remarks>
/// Implements IDN-READ-004, AUTHZ-INHERIT-002 and REC-ADM-001. A membership that ended
/// ties nothing; a record ties its data subject for as long as it stays registered in
/// the organization, and a person record its subject to the organization holding it.
/// </remarks>
internal sealed class AccountTies(StoreContext context) : IAccountTies
{
    /// <inheritdoc/>
    public async ValueTask<bool> TiedAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        await context.Memberships
            .AnyAsync(
                membership => membership.Subject == subject
                    && membership.Organization == organization
                    && membership.EndedAt == null,
                cancellationToken)
            .ConfigureAwait(false)
        || await context.Resources
            .AnyAsync(
                resource => resource.Subject == subject && resource.Organization == organization,
                cancellationToken)
            .ConfigureAwait(false)
        || await context.PersonRecords
            .AnyAsync(
                record => record.Subject == subject && record.Organization == organization,
                cancellationToken)
            .ConfigureAwait(false);
}
