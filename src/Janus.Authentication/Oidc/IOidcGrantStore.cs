using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Oidc;

/// <summary>
/// Where the provider's grants, and the tokens issued under each, are revoked by the
/// library's own statements, inside the unit of work in progress, and where the account
/// a grant records is read.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-003 and CONV-DESIGN-003 (D-207). The provider's own revocation
/// answers any failure as a refusal to revoke, which would leave a transaction aborted
/// unseen, so the family a reuse ends is revoked here and commits or rolls back with
/// the rest of it.
/// </remarks>
internal interface IOidcGrantStore
{
    /// <summary>
    /// Reads the account a grant records.
    /// </summary>
    /// <param name="grant">The provider's identifier of the grant, as a token carries it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The account, or nothing where no such grant is held.</returns>
    ValueTask<SubjectId?> SubjectAsync(string grant, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes a grant and every token issued under it.
    /// </summary>
    /// <param name="grant">The provider's identifier of the grant, as a token carries it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of revoking them.</returns>
    ValueTask RevokeAsync(string grant, CancellationToken cancellationToken);
}
