using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Organizations;

/// <summary>
/// Where organizations are created and carried through their deletion window, and
/// where their current members are read.
/// </summary>
/// <remarks>
/// Implements IDN-ORG-002, IDN-ORG-003, IDN-ORG-004 and CONV-DESIGN-003. The rules of
/// the window are the organization's own; this carries them to its row.
/// </remarks>
internal interface IOrganizationDirectory
{
    /// <summary>
    /// Where one organization stands.
    /// </summary>
    /// <param name="organization">Which organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Its standing, or nothing where no such row exists.</returns>
    ValueTask<OrganizationStanding?> FindAsync(OrganizationId organization, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a new organization.
    /// </summary>
    /// <param name="organization">The identifier issued for it.</param>
    /// <param name="name">What it is called.</param>
    /// <param name="at">When it was created.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask CreateAsync(
        OrganizationId organization,
        string name,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Requests an organization's deletion, which suspends it.
    /// </summary>
    /// <param name="organization">Which organization, one not already being deleted.</param>
    /// <param name="at">The instant of the request.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or <c>identity.organization.protected</c> for the administrative one.</returns>
    ValueTask<Result> RequestDeletionAsync(
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancels an organization's deletion, which lifts the suspension.
    /// </summary>
    /// <param name="organization">Which organization, one being deleted and not yet erased.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask CancelDeletionAsync(OrganizationId organization, CancellationToken cancellationToken);

    /// <summary>
    /// The accounts holding a current membership of an organization.
    /// </summary>
    /// <param name="organization">Which organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Its members.</returns>
    ValueTask<IReadOnlyList<SubjectId>> MembersAsync(OrganizationId organization, CancellationToken cancellationToken);
}
