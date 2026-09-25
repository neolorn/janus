using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// The answer to a token request: what the session record a code or a refresh token
/// stands on is worth now.
/// </summary>
/// <param name="oidc">Where the session record a token stands on is read.</param>
/// <remarks>
/// Implements AUTH-OIDC-002, AUTH-OIDC-003, AUTH-OIDC-004, AUTH-OIDC-006 and
/// AUTH-SESS-012. The grant itself has already been judged by the time this runs: what
/// is left is the record every token is minted from, which decides whether anything is
/// issued at all, how long the access token lasts, and how long a handle on the record
/// may be held.
/// </remarks>
internal sealed class TokenIssue(OidcService oidc)
    : IOpenIddictServerHandler<OpenIddictServerEvents.HandleTokenRequestContext>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ClaimsPrincipal principal = context.Principal
            ?? throw new InvalidOperationException("The grant carried no principal.");

        if (principal.GetClaim(OidcClaimNames.Session) is not string held
            || !Guid.TryParse(held, out Guid value))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, description: null, uri: null);

            return;
        }

        Error? failure = null;

        MintedSession minted = (await oidc
                .MintAsync(new SessionId(value), context.CancellationToken)
                .ConfigureAwait(false))
            .Match(session => session, error => Withheld(error, ref failure));

        // AUTH-OIDC-004 AC1, AUTH-OIDC-003 AC3: a record that has been revoked or has
        // run out mints nothing, and what the client is told about is the grant.
        if (failure is not null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, description: null, uri: null);

            return;
        }

        // The principal the grant carried is the one signed in again, so the grant it
        // belongs to and the row it was issued under stay the same ones.
        principal.SetAccessTokenLifetime(minted.AccessTokenLifetime);

        // AUTH-OIDC-003: the handle stops no later than the record it is a handle on,
        // and has no lifetime of its own to outlive it with.
        principal.SetRefreshTokenLifetime(minted.Remaining);

        // AUTH-OIDC-006 AC3: the access token names the client it was issued to as its
        // audience, so a party configured as another client refuses it.
        principal.SetAudiences(
            context.ClientId ?? throw new InvalidOperationException("The grant named no client."));
        principal.SetDestinations(Destinations);

        context.SignIn(principal);
    }

    private static MintedSession Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static IEnumerable<string> Destinations(Claim claim) =>
        claim.Type switch
        {
            OpenIddictConstants.Claims.Subject =>
                [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OidcClaimNames.Session =>
                [OpenIddictConstants.Destinations.IdentityToken],
            _ => [],
        };
}
