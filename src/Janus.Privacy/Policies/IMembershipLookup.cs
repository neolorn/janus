using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Policies;

/// <summary>
/// Which organizations a principal belongs to, which is what an operation of the
/// deployment rather than of one record is authorized within.
/// </summary>
/// <remarks>
/// Implements AUTHZ-SCOPE-001 and CONV-DESIGN-003. The access context names no
/// organization, so an administrative operation asks the gate in each organization
/// the caller holds a membership in.
/// </remarks>
internal interface IMembershipLookup
{
    /// <summary>
    /// The organizations a principal holds a live membership in.
    /// </summary>
    /// <param name="subject">The principal.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The organizations, empty for an individual.</returns>
    ValueTask<IReadOnlyList<OrganizationId>> OfAsync(
        SubjectId subject,
        CancellationToken cancellationToken);
}
