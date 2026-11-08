using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Grants;

/// <summary>
/// Where the removals of access of one organization are held one after the other.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-005 (D-281). The hold lasts until the transaction it is taken in
/// ends, so the host's check of one removal reads what a removal before it committed, and
/// two removals of the last two holders of a role admit exactly one.
/// </remarks>
internal interface IRemovalLocks
{
    /// <summary>
    /// Holds the removals of access of one organization for the rest of the transaction.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of holding them.</returns>
    /// <exception cref="System.InvalidOperationException">No transaction is open.</exception>
    ValueTask HoldAsync(OrganizationId organization, CancellationToken cancellationToken);
}
