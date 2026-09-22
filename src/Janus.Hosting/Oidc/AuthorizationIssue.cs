using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace Janus.Hosting.Oidc;

/// <summary>
/// The answer to an authorization request: a one-time code where the browser holds a
/// session, and otherwise the authentication application or the refusal a silent
/// request asked for.
/// </summary>
/// <param name="oidc">Where the code is issued.</param>
/// <param name="browser">What the browser carried.</param>
/// <param name="addresses">Where a browser holding no session is sent.</param>
/// <remarks>
/// Implements AUTH-SESS-012, AUTH-OIDC-002 and API-LAND-001. The endpoint forwards a
/// browser and never renders a page, so a person who holds no session is sent to the
/// authentication application's own route and the library writes no sentence.
/// </remarks>
internal sealed class AuthorizationIssue(
    IOidc oidc,
    RequestSession browser,
    AuthenticationAddresses addresses)
    : IOpenIddictServerHandler<OpenIddictServerEvents.HandleAuthorizationRequestContext>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        OpenIddictServerEvents.HandleAuthorizationRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var intent = new AuthorizationIntent(
            context.Request.ClientId ?? string.Empty,
            context.Request.RedirectUri ?? string.Empty,
            context.Request.Scope ?? string.Empty,
            context.Request.CodeChallenge ?? string.Empty,
            context.Request.CodeChallengeMethod ?? string.Empty,
            context.Request.Nonce,
            context.Request.HasPromptValue(OpenIddictConstants.PromptValues.None));

        Error? failure = null;

        IssuedCode issued = (await oidc
                .IssueCodeAsync(intent, browser.Live?.Id, context.CancellationToken)
                .ConfigureAwait(false))
            .Match(code => code, error => Withheld(error, ref failure));

        if (failure is Error refusal)
        {
            Refuse(context, refusal, intent.Silent);

            return;
        }

        context.SignIn(Principal(intent, issued));
    }

    private static IssuedCode Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
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

    private ClaimsPrincipal Principal(AuthorizationIntent intent, IssuedCode issued)
    {
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        Session live = browser.Live!;

        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, live.Subject.ToString()));
        identity.AddClaim(new Claim(OidcClaimNames.Session, live.Spine.ToString()));
        identity.AddClaim(new Claim(OidcClaimNames.Code, issued.Code));

        var principal = new ClaimsPrincipal(identity);

        principal.SetScopes(intent.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        principal.SetDestinations(Destinations);

        return principal;
    }

    private void Refuse(
        OpenIddictServerEvents.HandleAuthorizationRequestContext context,
        Error refusal,
        bool silent)
    {
        // AUTH-SESS-012 AC3: a request that is not silent is sent where a person can
        // sign in, which the deployment declared or it did not start.
        if (!silent
            && refusal.Code == ErrorCodes.SessionExpired
            && context.Transaction.GetHttpRequest() is HttpRequest request)
        {
            request.HttpContext.Response.Redirect(addresses.SignIn);
            context.HandleRequest();

            return;
        }

        // `login_required` answers a silent request and nothing else: it is what
        // `prompt=none` asks to be told, and a request that did not ask to be told it
        // was forwarded instead.
        context.Reject(
            silent && refusal.Code == ErrorCodes.SessionExpired
                ? OpenIddictConstants.Errors.LoginRequired
                : OpenIddictConstants.Errors.AccessDenied,
            description: null,
            uri: null);
    }
}
