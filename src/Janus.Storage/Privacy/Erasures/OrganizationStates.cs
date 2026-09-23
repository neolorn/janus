using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Organizations;
using Janus.Privacy.Erasures;
using Janus.Storage.Identity.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.Erasures;

/// <summary>
/// The organization deletion windows, and the erasure at the end of one.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="organizations">Where the organization is read and written.</param>
/// <param name="memberships">Where the memberships of it are read and written.</param>
/// <remarks>
/// Implements IDN-ORG-003, IDN-ORG-005 and IDN-PRIN-003. Every write here is made on
/// one context and committed by the caller's unit of work, so the memberships and the
/// organization reach the database together or not at all. No row is removed: what the
/// organization was called becomes its own identifier and the trail that references it
/// goes on resolving.
/// </remarks>
internal sealed class OrganizationStates(
    StoreContext context,
    IOrganizationStore organizations,
    IMembershipStore memberships) : IOrganizationStates
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<PendingOrganizationDeletion>> DeletingSinceAsync(
        DateTimeOffset before,
        CancellationToken cancellationToken)
    {
        List<OrganizationRecord> pending = await context.Organizations
            .AsNoTracking()
            .Where(row =>
                row.DeletionRequestedAt != null
                && row.DeletionRequestedAt <= before
                && row.ErasedAt == null)
            .OrderBy(row => row.DeletionRequestedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var deleting = new List<PendingOrganizationDeletion>(pending.Count);

        foreach (OrganizationRecord row in pending)
        {
            deleting.Add(new PendingOrganizationDeletion(
                row.Id,
                row.DeletionRequestedAt ?? throw new InvalidOperationException(
                    "An organization with no deletion request was read as deleting.")));
        }

        return deleting;
    }

    /// <inheritdoc/>
    public async ValueTask<int> EraseAsync(
        OrganizationId organization,
        DateTimeOffset at,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        Organization erasing = await organizations
                .FindAsync(organization, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException("No such organization.");

        int ended = await EndedAsync(organization, at, cancellationToken).ConfigureAwait(false);

        erasing.RecordErasure(at, window);

        await organizations.RecordAsync(erasing, cancellationToken).ConfigureAwait(false);

        return ended;
    }

    private async ValueTask<int> EndedAsync(
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Membership> held = await memberships
            .FindByOrganizationAsync(organization, cancellationToken)
            .ConfigureAwait(false);

        int ended = 0;

        foreach (Membership membership in held)
        {
            if (!membership.IsCurrent)
            {
                continue;
            }

            membership.End(at);

            await memberships.RecordAsync(membership, cancellationToken).ConfigureAwait(false);

            ended++;
        }

        return ended;
    }
}
