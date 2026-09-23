using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Core;
using Janus.Identity.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Organizations;

/// <summary>
/// Organizations and their current members, over the <c>organizations</c> and
/// <c>memberships</c> tables.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="organizations">Where the organization is read and its change carried.</param>
/// <remarks>
/// Implements IDN-ORG-002, IDN-ORG-003, IDN-ORG-004 and CONV-DESIGN-003. Every change
/// goes through the organization itself, so the rules of the window and the refusal
/// for the administrative organization are the domain's.
/// </remarks>
internal sealed class OrganizationDirectory(StoreContext context, IOrganizationStore organizations)
    : IOrganizationDirectory
{
    /// <inheritdoc/>
    public async ValueTask<OrganizationStanding?> FindAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        await organizations.FindAsync(organization, cancellationToken).ConfigureAwait(false) is Organization found
            ? new OrganizationStanding(found.Id, found.Name, found.IsAdministrative, found.DeletionRequestedAt, found.ErasedAt)
            : null;

    /// <inheritdoc/>
    public async ValueTask CreateAsync(
        OrganizationId organization,
        string name,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await organizations
            .CreateAsync(Organization.Create(organization, name, at), cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<Result> RequestDeletionAsync(
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Organization found = await ExistingAsync(organization, cancellationToken).ConfigureAwait(false);

        if (found.RequestDeletion(at).Match<Error?>(() => null, error => error) is Error refused)
        {
            return Result.Failure(refused);
        }

        await organizations.RecordAsync(found, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask CancelDeletionAsync(OrganizationId organization, CancellationToken cancellationToken)
    {
        Organization found = await ExistingAsync(organization, cancellationToken).ConfigureAwait(false);

        found.CancelDeletion();

        await organizations.RecordAsync(found, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SubjectId>> MembersAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        await context.Memberships
            .Where(membership => membership.Organization == organization && membership.EndedAt == null)
            .Select(membership => membership.Subject)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask<Organization> ExistingAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        await organizations.FindAsync(organization, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The organization has no row to carry the change.");
}
