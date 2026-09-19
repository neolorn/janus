using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Identity.Organizations;

/// <summary>
/// Where memberships are read and written.
/// </summary>
/// <remarks>
/// Implements IDN-MEM-001, IDN-MEM-002 and CONV-DESIGN-003. The schema permits an
/// account any number of memberships; whether a second one may be created is
/// <c>organization.multiplememberships</c> and not a shape of the table.
/// </remarks>
internal interface IMembershipStore
{
    /// <summary>
    /// Reads every membership an account holds, ended ones included.
    /// </summary>
    /// <param name="subject">Whose memberships to read.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The memberships, oldest first.</returns>
    ValueTask<IReadOnlyList<Membership>> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads every membership of an organization, ended ones included.
    /// </summary>
    /// <param name="organization">Which organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The memberships, oldest first.</returns>
    ValueTask<IReadOnlyList<Membership>> FindByOrganizationAsync(
        OrganizationId organization,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes a new membership.
    /// </summary>
    /// <param name="membership">The membership to create.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask CreateAsync(Membership membership, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the membership as it now stands onto its row.
    /// </summary>
    /// <param name="membership">The membership as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="System.InvalidOperationException">No such row exists.</exception>
    ValueTask RecordAsync(Membership membership, CancellationToken cancellationToken);
}
