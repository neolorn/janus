using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Identity.Organizations;

/// <summary>
/// Where organizations are read and written.
/// </summary>
/// <remarks>
/// Implements IDN-ORG-001, IDN-ORG-003 and CONV-DESIGN-003. No read filters by
/// organization for isolation: an organization is an entity in the one pool, and
/// scoping is the authorization seam's business.
/// </remarks>
internal interface IOrganizationStore
{
    /// <summary>
    /// Reads one organization.
    /// </summary>
    /// <param name="id">Which organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The organization, or nothing where no such row exists.</returns>
    ValueTask<Organization?> FindAsync(OrganizationId id, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a new organization.
    /// </summary>
    /// <param name="organization">The organization to create.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask CreateAsync(Organization organization, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the organization as it now stands onto its row.
    /// </summary>
    /// <param name="organization">The organization as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    /// <exception cref="System.InvalidOperationException">No such row exists.</exception>
    ValueTask RecordAsync(Organization organization, CancellationToken cancellationToken);
}
