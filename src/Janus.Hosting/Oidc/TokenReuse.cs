using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// What happens when a refresh token is presented after it has already been used.
/// </summary>
/// <param name="oidc">Where the session family is ended and the reuse recorded.</param>
/// <param name="tokens">Where the row behind the presented token is read.</param>
/// <remarks>
/// Implements AUTH-OIDC-003 AC1, AC2 and AC4. A token presented twice means a copy is in
/// someone's hands and there is no telling whose, so everything that stands on the
/// session record goes: the grant, every token issued under it, and the sessions
/// derived from the record, together in one unit of work (D-207). Whatever the token
/// carries, it takes that one path: a token naming no session record or no account, or
/// issued under no grant, loses what of its family it names (D-208). The refusal is
/// issued here rather than left to the stage after it, so nothing can be issued on the
/// strength of a token already spent.
/// </remarks>
internal sealed class TokenReuse(OidcService oidc, IOpenIddictTokenManager tokens)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>
{
    /// <summary>
    /// Where the handler sits: after the server has read the token into a principal,
    /// and before it judges the row the token was issued as.
    /// </summary>
    public const int Order = int.MinValue + 111_500;

    /// <inheritdoc/>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.ValidTokenTypes.Contains(OpenIddictConstants.TokenTypeIdentifiers.RefreshToken)
            || context.Principal is not ClaimsPrincipal presented
            || presented.GetTokenId() is not string identifier)
        {
            return;
        }

        if (await tokens.FindByIdAsync(identifier, context.CancellationToken).ConfigureAwait(false)
                is not object held
            || !await tokens
                .HasStatusAsync(held, OpenIddictConstants.Statuses.Redeemed, context.CancellationToken)
                .ConfigureAwait(false))
        {
            return;
        }

        SubjectId? subject = presented.GetClaim(OpenIddictConstants.Claims.Subject) is string account
            && Guid.TryParse(account, out Guid named)
                ? new SubjectId(named)
                : null;

        SessionId? session = presented.GetClaim(OidcClaimNames.Session) is string record
            && Guid.TryParse(record, out Guid spine)
                ? new SessionId(spine)
                : null;

        await oidc
            .ReuseAsync(
                subject,
                session,
                context.Request?.ClientId ?? string.Empty,
                presented.GetAuthorizationId(),
                context.CancellationToken)
            .ConfigureAwait(false);

        context.Reject(OpenIddictConstants.Errors.InvalidGrant, description: null, uri: null);
    }
}
