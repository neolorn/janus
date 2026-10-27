using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Core;
using Janus.Identity.Organizations;
using Janus.Storage.Identity.Organizations;
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
        Standing(await organizations.FindAsync(organization, cancellationToken).ConfigureAwait(false));

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<OrganizationStanding?> HoldAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        Standing(await organizations.FindForUpdateAsync(organization, cancellationToken).ConfigureAwait(false));

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

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<OrganizationStanding>> PageAsync(
        IReadOnlyCollection<OrganizationId>? within,
        OrganizationId? after,
        int count,
        CancellationToken cancellationToken)
    {
        Guid? from = after?.Value;
        Guid[]? among = within is null ? null : [.. within.Select(organization => organization.Value)];

        // IDN-READ-001: ordered by identifier, as the database orders it, and paged from
        // the identifier the last page ended at.
        List<OrganizationRecord> found = await context.Organizations
            .FromSql(
                $"""
                SELECT * FROM identity.organizations
                WHERE erased_at IS NULL
                  AND ({from}::uuid IS NULL OR id > {from}::uuid)
                  AND ({among}::uuid[] IS NULL OR id = ANY({among}::uuid[]))
                ORDER BY id
                LIMIT {count}
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. found.Select(record => new OrganizationStanding(
            record.Id,
            record.Name,
            record.IsAdministrative,
            record.DeletionRequestedAt,
            record.ErasedAt))];
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<MemberSummary>> MemberPageAsync(
        OrganizationId organization,
        SubjectId? after,
        int count,
        CancellationToken cancellationToken)
    {
        Guid? from = after?.Value;

        // IDN-READ-002: ordered by subject, as the database orders it, and paged from the
        // subject the last page ended at.
        List<MembershipRecord> found = await context.Memberships
            .FromSql(
                $"""
                SELECT * FROM identity.memberships
                WHERE organization = {organization.Value} AND ended_at IS NULL
                  AND ({from}::uuid IS NULL OR subject > {from}::uuid)
                ORDER BY subject
                LIMIT {count}
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. found.Select(Member)];
    }

    /// <inheritdoc/>
    public async ValueTask<MemberSummary?> MemberAsync(
        OrganizationId organization,
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await context.Memberships
            .AsNoTracking()
            .Where(membership => membership.Organization == organization
                && membership.Subject == subject
                && membership.EndedAt == null)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) is MembershipRecord found
            ? Member(found)
            : null;

    private static MemberSummary Member(MembershipRecord membership) =>
        new(membership.Subject, membership.Id, membership.CreatedAt, membership.EndsAt, membership.Invitation);

    private static OrganizationStanding? Standing(Organization? found) =>
        found is null
            ? null
            : new OrganizationStanding(found.Id, found.Name, found.IsAdministrative, found.DeletionRequestedAt, found.ErasedAt);

    private async ValueTask<Organization> ExistingAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        await organizations.FindAsync(organization, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The organization has no row to carry the change.");
}
