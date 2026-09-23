using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The two operations of the OpenID Connect provider a host calls in process and the
/// library answers over HTTP: the claims a token covers, and the keys the tokens are
/// validated against.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, AUTH-OIDC-001 and AUTH-KEY-001. The protocol itself is not
/// here: the request shapes, the codes, the tokens, the signatures and the refusals
/// are the server's, and what the library holds of a session while a token is minted
/// is not an operation a host calls, so it is not on this contract either.
/// </remarks>
public interface IOidc
{
    /// <summary>
    /// What a token covering these scopes says about the person.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="scope">What the token covers, space separated.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The claims, or the refusal where the account no longer answers.</returns>
    ValueTask<Result<OidcClaims>> ClaimsAsync(
        SubjectId subject,
        string scope,
        CancellationToken cancellationToken);

    /// <summary>
    /// The keys a relying party validates against: the one signing now and, through
    /// the overlap, the one before it.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The published set, or the refusal where the deployment's own signing settings
    /// cannot be read: a set that is empty because something failed would validate
    /// nothing while saying nothing.
    /// </returns>
    ValueTask<Result<IReadOnlyList<PublishedSigningKey>>> KeysAsync(
        CancellationToken cancellationToken);
}
