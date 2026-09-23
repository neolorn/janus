using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace Janus.Hosting.Oidc;

/// <summary>
/// The answer to an authorization request: a code against the session the browser
/// holds, and otherwise the authentication application or the refusal a silent request
/// asked for.
/// </summary>
/// <param name="oidc">Where the session record is read.</param>
/// <param name="browser">What the browser carried.</param>
/// <param name="addresses">Where a browser holding no session is sent.</param>
/// <param name="configuration">Where the code's lifetime comes from.</param>
/// <remarks>
/// Implements AUTH-SESS-012, AUTH-OIDC-002, AUTH-OIDC-004 and API-LAND-001. The
/// endpoint forwards a browser and never renders a page, so a person who holds no
/// session is sent to the authentication application's own route and the library
/// writes no sentence. The code stands on the session record, which the token endpoint
/// reads again before it mints anything.
/// </remarks>
internal sealed class AuthorizationIssue(
    OidcService oidc,
    RequestSession browser,
    AuthenticationAddresses addresses,
    IConfigurationStore configuration)
    : IOpenIddictServerHandler<OpenIddictServerEvents.HandleAuthorizationRequestContext>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        OpenIddictServerEvents.HandleAuthorizationRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        bool silent = context.Request.HasPromptValue(OpenIddictConstants.PromptValues.None);

        // AUTH-OIDC-004 AC1: the record the code will stand on has to answer now, not
        // only when the cookie was resolved.
        if (browser.Live is not Session live
            || (await oidc.MintAsync(live.Spine, context.CancellationToken).ConfigureAwait(false))
                .Match(_ => false, _ => true))
        {
            Refuse(context, silent);

            return;
        }

        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.OidcCodeLifetime, context.CancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, error => Withheld(error, ref failure));

        if (failure is not null)
        {
            throw new InvalidOperationException("The code lifetime could not be read.");
        }

        context.SignIn(Principal(context, live, lifetime));
    }

    private static TimeSpan Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default;
    }

    private static IEnumerable<string> Destinations(Claim claim) =>
        claim.Type switch
        {
            // BFF-SESS-006: the confidential client reads the session record from the
            // identity token, and the access token carries who the person is.
            OpenIddictConstants.Claims.Subject =>
                [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OidcClaimNames.Session => [OpenIddictConstants.Destinations.IdentityToken],
            _ => [],
        };

    private static ClaimsPrincipal Principal(
        OpenIddictServerEvents.HandleAuthorizationRequestContext context,
        Session live,
        TimeSpan lifetime)
    {
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, live.Subject.ToString()));
        identity.AddClaim(new Claim(OidcClaimNames.Session, live.Spine.ToString()));

        var principal = new ClaimsPrincipal(identity);

        principal.SetScopes(context.Request.GetScopes());
        principal.SetAuthorizationCodeLifetime(lifetime);
        principal.SetDestinations(Destinations);

        return principal;
    }

    private void Refuse(
        OpenIddictServerEvents.HandleAuthorizationRequestContext context,
        bool silent)
    {
        // AUTH-SESS-012 AC3: a request that is not silent is sent where a person can
        // sign in, which the deployment declared or it did not start.
        if (!silent && context.Transaction.GetHttpRequest() is HttpRequest request)
        {
            request.HttpContext.Response.Redirect(addresses.SignIn);
            context.HandleRequest();

            return;
        }

        // `login_required` answers a silent request and nothing else: it is what
        // `prompt=none` asks to be told, and a request that did not ask to be told it
        // was forwarded instead.
        context.Reject(
            silent
                ? OpenIddictConstants.Errors.LoginRequired
                : OpenIddictConstants.Errors.AccessDenied,
            description: null,
            uri: null);
    }
}
