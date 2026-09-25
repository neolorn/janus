using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Organizations;

/// <summary>
/// The domains organizations lock their members to, over the
/// <c>organization_domains</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>Implements REG-DOM-001, IDN-ORG-006 and CONV-DESIGN-003.</remarks>
internal sealed class DomainStore(StoreContext context) : IDomainStore
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<LockedDomain>> OfAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        [.. (await context.OrganizationDomains
                .Where(domain => domain.Organization == organization)
                .OrderBy(domain => domain.AddedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(Read)];

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<LockedDomain>> DueAsync(
        DateTimeOffset checkedBefore,
        CancellationToken cancellationToken) =>
        [.. (await context.OrganizationDomains
                .Where(domain => domain.RemovedAt == null
                    && domain.VerifiedAt != null
                    && domain.CheckedAt < checkedBefore)
                .OrderBy(domain => domain.CheckedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(Read)];

    /// <inheritdoc/>
    public async ValueTask AddAsync(LockedDomain domain, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domain);

        await context.OrganizationDomains
            .AddAsync(
                new LockedDomainRecord
                {
                    Token = domain.Token,
                    Organization = domain.Organization,
                    Domain = domain.Domain,
                    AddedAt = domain.AddedAt,
                    VerifiedAt = domain.VerifiedAt,
                    CheckedAt = domain.CheckedAt,
                    LastCheckPassed = domain.LastCheckPassed,
                    RemovedAt = domain.RemovedAt,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(LockedDomain domain, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domain);

        LockedDomainRecord record = await context.OrganizationDomains
            .FindAsync([domain.Token], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The domain has no row to carry the change.");

        record.VerifiedAt = domain.VerifiedAt;
        record.CheckedAt = domain.CheckedAt;
        record.LastCheckPassed = domain.LastCheckPassed;
        record.RemovedAt = domain.RemovedAt;
    }

    private static LockedDomain Read(LockedDomainRecord record) =>
        LockedDomain.Existing(
            record.Organization,
            record.Domain,
            record.Token,
            record.AddedAt,
            record.VerifiedAt,
            record.CheckedAt,
            record.LastCheckPassed,
            record.RemovedAt);
}
