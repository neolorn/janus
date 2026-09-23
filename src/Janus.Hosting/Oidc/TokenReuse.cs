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
/// <param name="tokens">Where the row behind the presented token is read and revoked.</param>
/// <param name="authorizations">Where the grant it was issued under is revoked.</param>
/// <remarks>
/// Implements AUTH-OIDC-003 AC1 and AC2. A token presented twice means a copy is in
/// someone's hands and there is no telling whose, so everything that stands on the
/// session record goes: the grant, every token issued under it, and the sessions
/// derived from the record. The refusal is issued here rather than left to the stage
/// after it, so nothing can be issued on the strength of a token already spent.
/// </remarks>
internal sealed class TokenReuse(
    OidcService oidc,
    IOpenIddictTokenManager tokens,
    IOpenIddictAuthorizationManager authorizations)
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

        if (presented.GetAuthorizationId() is string grant)
        {
            _ = await tokens.RevokeByAuthorizationIdAsync(grant, context.CancellationToken)
                .ConfigureAwait(false);

            if (await authorizations.FindByIdAsync(grant, context.CancellationToken)
                .ConfigureAwait(false) is object issued)
            {
                _ = await authorizations.TryRevokeAsync(issued, context.CancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (presented.GetClaim(OidcClaimNames.Session) is string named
            && Guid.TryParse(named, out Guid session)
            && presented.GetClaim(OpenIddictConstants.Claims.Subject) is string account
            && Guid.TryParse(account, out Guid subject))
        {
            await oidc
                .ReuseAsync(
                    new SubjectId(subject),
                    new SessionId(session),
                    context.Request?.ClientId ?? string.Empty,
                    context.CancellationToken)
                .ConfigureAwait(false);
        }

        context.Reject(OpenIddictConstants.Errors.InvalidGrant, description: null, uri: null);
    }
}
