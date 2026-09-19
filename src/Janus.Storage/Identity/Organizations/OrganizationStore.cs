using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Organizations;

namespace Janus.Storage.Identity.Organizations;

/// <summary>
/// Organizations, over the <c>organizations</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>
/// Implements IDN-ORG-001, IDN-ORG-003, IDN-ORG-004 and CONV-DESIGN-003. The translation between the
/// organization and its row lives here and nowhere else.
/// </remarks>
internal sealed class OrganizationStore(JanusDbContext context) : IOrganizationStore
{
    /// <inheritdoc/>
    public async ValueTask<Organization?> FindAsync(
        OrganizationId id,
        CancellationToken cancellationToken)
    {
        OrganizationRecord? record = await StoredAsync(id, cancellationToken).ConfigureAwait(false);

        return record is null ? null : Organization.Existing(
            record.Id,
            record.Name,
            record.CreatedAt,
            record.IsAdministrative,
            record.DeletionRequestedAt,
            record.ErasedAt);
    }

    /// <inheritdoc/>
    public async ValueTask CreateAsync(Organization organization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(organization);

        await context.Organizations
            .AddAsync(
                new OrganizationRecord
                {
                    Id = organization.Id,
                    Name = organization.Name,
                    CreatedAt = organization.CreatedAt,
                    IsAdministrative = organization.IsAdministrative,
                    DeletionRequestedAt = organization.DeletionRequestedAt,
                    ErasedAt = organization.ErasedAt,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(Organization organization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(organization);

        OrganizationRecord record = await StoredAsync(organization.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The organization has no row to carry the change.");

        record.DeletionRequestedAt = organization.DeletionRequestedAt;
        record.ErasedAt = organization.ErasedAt;
    }

    private async ValueTask<OrganizationRecord?> StoredAsync(
        OrganizationId id,
        CancellationToken cancellationToken) =>
        await context.Organizations.FindAsync([id], cancellationToken).ConfigureAwait(false);
}
