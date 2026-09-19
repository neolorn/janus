using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Policies;

/// <summary>
/// Which organizations a principal belongs to, which is the only thing a policy is
/// resolved from.
/// </summary>
/// <remarks>
/// Implements AUTH-PRIN-002 and CONV-DESIGN-003. Never a user type, an application
/// identity or the resource acted on.
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
