using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace Janus.Hosting.Oidc;

/// <summary>
/// The answer to a token request: what a code or a refresh token was exchanged for.
/// </summary>
/// <param name="oidc">Where the exchange is judged and recorded.</param>
/// <remarks>
/// Implements AUTH-OIDC-002, AUTH-OIDC-003, AUTH-OIDC-004 and AUTH-SESS-012. A wrong
/// secret, a spent code and a wrong verifier are one refusal, so a client learns which
/// of them it got wrong only by holding all of them right.
/// </remarks>
internal sealed class TokenIssue(IOidc oidc)
    : IOpenIddictServerHandler<OpenIddictServerEvents.HandleTokenRequestContext>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Error? failure = null;

        IssuedToken issued = (await ExchangedAsync(context, context.CancellationToken).ConfigureAwait(false))
            .Match(token => token, error => Withheld(error, ref failure));

        if (failure is Error refusal)
        {
            context.Reject(
                refusal.Code == ErrorCodes.SessionExpired
                    ? OpenIddictConstants.Errors.InvalidGrant
                    : Refused(refusal),
                description: null,
                uri: null);

            return;
        }

        context.SignIn(Principal(issued));
    }

    private static IssuedToken Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // The provider's refusals are the standard ones: a client that did not
    // authenticate is `invalid_client` and everything about the grant itself is
    // `invalid_grant`.
    private static string Refused(Error refusal) =>
        refusal.Code == ErrorCodes.Denied
            ? OpenIddictConstants.Errors.InvalidClient
            : OpenIddictConstants.Errors.InvalidGrant;

    private static IEnumerable<string> Destinations(Claim claim) =>
        claim.Type switch
        {
            OpenIddictConstants.Claims.Subject =>
                [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OidcClaimNames.Session or OpenIddictConstants.Claims.Nonce =>
                [OpenIddictConstants.Destinations.IdentityToken],
            _ => [],
        };

    private static ClaimsPrincipal Principal(IssuedToken issued)
    {
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, issued.Subject.ToString()));
        identity.AddClaim(new Claim(OidcClaimNames.Session, issued.Session.ToString()));

        if (issued.Nonce is string nonce)
        {
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Nonce, nonce));
        }

        if (issued.RefreshToken is string refresh)
        {
            identity.AddClaim(new Claim(OidcClaimNames.RefreshToken, refresh));
        }

        var principal = new ClaimsPrincipal(identity);

        // AUTH-OIDC-002: a browser application's own layer establishes one session and
        // holds nothing afterwards, so the scope that would carry a refresh token is
        // not granted to it however it asked.
        principal.SetScopes(Granted(issued));
        principal.SetAccessTokenLifetime(issued.Lifetime);
        principal.SetDestinations(Destinations);

        return principal;
    }

    private static IEnumerable<string> Granted(IssuedToken issued)
    {
        IEnumerable<string> asked = issued.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return issued.RefreshToken is null
            ? asked.Where(scope => !string.Equals(
                scope,
                OpenIddictConstants.Scopes.OfflineAccess,
                StringComparison.Ordinal))
            : asked;
    }

    private async ValueTask<Result<IssuedToken>> ExchangedAsync(
        OpenIddictServerEvents.HandleTokenRequestContext context,
        CancellationToken cancellationToken) =>
        context.Request.IsRefreshTokenGrantType()
            ? await oidc
                .RefreshAsync(
                    new RefreshRedemption(
                        context.Request.RefreshToken ?? string.Empty,
                        context.Request.ClientId ?? string.Empty,
                        context.Request.ClientSecret ?? string.Empty),
                    cancellationToken)
                .ConfigureAwait(false)
            : await oidc
                .RedeemCodeAsync(
                    new CodeRedemption(
                        context.Request.Code ?? string.Empty,
                        context.Request.ClientId ?? string.Empty,
                        context.Request.ClientSecret ?? string.Empty,
                        context.Request.RedirectUri ?? string.Empty,
                        context.Request.CodeVerifier ?? string.Empty),
                    cancellationToken)
                .ConfigureAwait(false);
}
