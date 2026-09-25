using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Organizations;

/// <summary>
/// Where the domains organizations lock their members to are read and written.
/// </summary>
/// <remarks>
/// Implements REG-DOM-001, IDN-ORG-006 and CONV-DESIGN-003. A removed domain's row
/// stays, so no token is drawn twice and a removal goes on refusing.
/// </remarks>
internal interface IDomainStore
{
    /// <summary>
    /// Reads every domain an organization has listed, removed ones included.
    /// </summary>
    /// <param name="organization">Which organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The domains, oldest first.</returns>
    ValueTask<IReadOnlyList<LockedDomain>> OfAsync(
        OrganizationId organization,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads every listed, verified domain last checked before an instant, which is
    /// what the sweep re-verifies.
    /// </summary>
    /// <param name="checkedBefore">The instant a check is due before.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The domains, the longest unchecked first.</returns>
    ValueTask<IReadOnlyList<LockedDomain>> DueAsync(
        DateTimeOffset checkedBefore,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes a newly listed domain.
    /// </summary>
    /// <param name="domain">The domain.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AddAsync(LockedDomain domain, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the domain as it now stands onto its row.
    /// </summary>
    /// <param name="domain">The domain as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="InvalidOperationException">No such row exists.</exception>
    ValueTask RecordAsync(LockedDomain domain, CancellationToken cancellationToken);
}
