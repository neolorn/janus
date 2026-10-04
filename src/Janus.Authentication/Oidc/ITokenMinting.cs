using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Oidc;

/// <summary>
/// Where a token about to be minted is asked what its session record allows.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-007, AUTH-OIDC-003 and AUTH-OIDC-004. Every token stands on a
/// session record, and no public contract declares the question of what one may be
/// minted from, so a type outside this project that mints one asks through this
/// contract and never names what answers it.
/// </remarks>
internal interface ITokenMinting
{
    /// <summary>
    /// What a token minted from a session record may carry and how long it may last.
    /// </summary>
    /// <param name="session">The record the token stands on.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// What to mint from, or <c>auth.session.expired</c> where the record has been
    /// revoked or has reached either of its expiries: no token is minted from a record
    /// that no longer answers.
    /// </returns>
    ValueTask<Result<MintedSession>> MintAsync(SessionId session, CancellationToken cancellationToken);
}
