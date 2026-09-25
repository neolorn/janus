using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Core;

namespace Janus.Authentication.Tests.Organizations;

/// <summary>
/// The domains organizations lock their members to, held in memory. One row stands
/// listed per organization and domain, as the partial unique index holds it.
/// </summary>
internal sealed class DomainStoreInMemory : IDomainStore
{
    private readonly List<LockedDomain> _domains = [];

    /// <summary>
    /// Every row, removed ones included, in the order they were added.
    /// </summary>
    public IReadOnlyList<LockedDomain> Held => _domains;

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<LockedDomain>> OfAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<LockedDomain>>(
            [.. _domains.Where(domain => domain.Organization == organization).OrderBy(domain => domain.AddedAt)]);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<LockedDomain>> DueAsync(
        DateTimeOffset checkedBefore,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<LockedDomain>>(
            [.. _domains
                .Where(domain => domain.IsListed
                    && domain.VerifiedAt is not null
                    && domain.CheckedAt < checkedBefore)
                .OrderBy(domain => domain.CheckedAt)]);

    /// <inheritdoc/>
    public ValueTask AddAsync(LockedDomain domain, CancellationToken cancellationToken)
    {
        if (_domains.Any(held => held.IsListed
            && held.Organization == domain.Organization
            && string.Equals(held.Domain, domain.Domain, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("The domain is listed already.");
        }

        _domains.Add(domain);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(LockedDomain domain, CancellationToken cancellationToken)
    {
        int at = _domains.FindIndex(held => string.Equals(held.Token, domain.Token, StringComparison.Ordinal));

        if (at < 0)
        {
            throw new InvalidOperationException("The domain has no row to carry the change.");
        }

        _domains[at] = domain;

        return ValueTask.CompletedTask;
    }
}
