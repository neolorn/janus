using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authorization.Grants;

/// <summary>
/// Where a revocation no person made is written down.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-003, AUTHZ-INHERIT-002 and IDN-AUD-001 (D-203). The record is
/// written in the transaction that revokes the grant, so the trail and the grant never
/// disagree.
/// </remarks>
internal interface IGrantAudit
{
    /// <summary>
    /// Records a grant the removal of the record it names revoked.
    /// </summary>
    /// <param name="revoked">The grant, revoked, carrying its revoker, instant and reason.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask RevokedAsync(Grant revoked, CancellationToken cancellationToken);
}
