using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Where the gate reads whether an organization is suspended, its deletion requested.
/// </summary>
/// <remarks>
/// Implements IDN-ORG-003 (entry 266). The state belongs to the organization, which is
/// another area's aggregate and therefore out of this one's reach (LIB-PKG-001), so the
/// gate reads the one fact it evaluates through a port of its own (CONV-DESIGN-003).
/// A stored grant is left out by the effective grants view; a derivation has no row
/// there, so this is where it is stopped.
/// </remarks>
internal interface IOrganizationSuspensions
{
    /// <summary>
    /// Whether the organization is suspended.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether its deletion is requested, which an organization the library holds no
    /// row for is not: none of its records is registered either, so nothing reaches one.
    /// </returns>
    ValueTask<bool> IsSuspendedAsync(OrganizationId organization, CancellationToken cancellationToken);
}
