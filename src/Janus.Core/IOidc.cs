using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What the library owns of the OpenID Connect provider: the registry of manually
/// registered clients, the one-time codes a live session is issued, the tokens those
/// codes are exchanged for, the claims a token covers, and the keys the tokens are
/// validated against.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, AUTH-OIDC-001, AUTH-OIDC-002, AUTH-OIDC-003, AUTH-OIDC-004,
/// AUTH-SESS-012, AUTH-KEY-001 and API-REDIR-002. The protocol itself is not here: the
/// request shapes, the token formats and the signatures are the server's, and what is
/// here is what the deployment holds about its own clients, sessions and keys.
/// </remarks>
public interface IOidc
{
    /// <summary>
    /// The client a request named, where the registry holds one.
    /// </summary>
    /// <param name="clientId">What the request called it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The client, or <c>authz.denied</c> where the registry holds none: an
    /// unregistered client obtains neither a code nor a token.
    /// </returns>
    ValueTask<Result<OidcClient>> FindClientAsync(string clientId, CancellationToken cancellationToken);

    /// <summary>
    /// Issues a one-time code against a live session.
    /// </summary>
    /// <param name="intent">What the client asked for.</param>
    /// <param name="session">The session the browser holds at the authentication application.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The code, or the refusal: an unregistered client, a destination that is not the
    /// client's own, or a silent request where no session answers.
    /// </returns>
    ValueTask<Result<IssuedCode>> IssueCodeAsync(
        AuthorizationIntent intent,
        SessionId? session,
        CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges a code for the session it was issued against.
    /// </summary>
    /// <param name="redemption">What the client presented.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What the token is minted from, or the refusal.</returns>
    ValueTask<Result<IssuedToken>> RedeemCodeAsync(
        CodeRedemption redemption,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rotates a refresh token, revoking everything derived from the session where the
    /// token has already been used.
    /// </summary>
    /// <param name="redemption">What the client presented.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What the token is minted from, or the refusal.</returns>
    ValueTask<Result<IssuedToken>> RefreshAsync(
        RefreshRedemption redemption,
        CancellationToken cancellationToken);

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
