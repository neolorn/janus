using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Core;
using Janus.Privacy.Policies;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.Policies;

/// <summary>
/// Which organizations a read of another person may be asked in, and whom each reaches,
/// read from the organizations and from the ties the account reads already answer.
/// </summary>
/// <param name="context">The store.</param>
/// <param name="ties">Which accounts an organization reaches.</param>
/// <remarks>
/// Implements IDN-READ-004. A person is reached by the same ties for the standing as for
/// the account and the contact details, so the two areas read one answer.
/// </remarks>
internal sealed class ReachedSubjects(StoreContext context, IAccountTies ties) : IReachedSubjects
{
    /// <inheritdoc/>
    public async ValueTask<bool> HeldAsync(OrganizationId organization, CancellationToken cancellationToken) =>
        await context.Organizations
            .AnyAsync(held => held.Id == organization && held.ErasedAt == null, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public ValueTask<bool> TiedAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        ties.TiedAsync(subject, organization, cancellationToken);
}
