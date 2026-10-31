using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
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
/// <param name="pushed">What spends the pushed request a forwarded browser carried.</param>
/// <param name="pending">Where a forwarded browser's request waits for its sign-in.</param>
/// <remarks>
/// Implements AUTH-SESS-012, AUTH-OIDC-002, AUTH-OIDC-004, AUTH-OIDC-006 and
/// API-LAND-001. The endpoint forwards a browser and never renders a page, so a person
/// who holds no session is sent to the authentication application's own route and the
/// library writes no sentence. The code stands on the session record, which the token endpoint
/// reads again before it mints anything.
/// </remarks>
internal sealed class AuthorizationIssue(
    OidcService oidc,
    RequestSession browser,
    AuthenticationAddresses addresses,
    IConfigurationStore configuration,
    PushedRequestSpent pushed,
    PendingAuthorizations pending)
    : IOpenIddictServerHandler<OpenIddictServerEvents.HandleAuthorizationRequestContext>
{
    // The parameters a kept request leaves behind: the reference it was read from and
    // whatever authenticated the client at the push.
    private static readonly string[] Unkept =
    [
        OpenIddictConstants.Parameters.RequestUri,
        OpenIddictConstants.Parameters.Request,
        OpenIddictConstants.Parameters.ClientSecret,
        OpenIddictConstants.Parameters.ClientAssertion,
        OpenIddictConstants.Parameters.ClientAssertionType,
    ];

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
            await RefuseAsync(context, silent, context.CancellationToken).ConfigureAwait(false);

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

    // What the continuation reads back: the parameters the request was validated with,
    // without the reference it was read from or anything that authenticated a client.
    private static string Kept(OpenIddictRequest request)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            new OpenIddictRequest(request
                    .GetParameters()
                    .Where(parameter => !Unkept.Contains(parameter.Key, StringComparer.Ordinal)))
                .WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
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

    private async ValueTask RefuseAsync(
        OpenIddictServerEvents.HandleAuthorizationRequestContext context,
        bool silent,
        CancellationToken cancellationToken)
    {
        // AUTH-SESS-012 AC3: a request that is not silent is sent where a person can
        // sign in, which the deployment declared or it did not start. No response of
        // the server's own follows, so the pushed request is spent here (AUTH-OIDC-006
        // AC2).
        if (!silent && context.Transaction.GetHttpRequest() is HttpRequest request)
        {
            await pushed.SpendAsync(context.Transaction, cancellationToken).ConfigureAwait(false);

            // AUTH-SESS-012 AC10 (D-224): the request, validated, waits for the sign-in
            // on the browser's pre-authentication session, a later one replacing it.
            if (browser.FirstContact is PreAuthentication contact)
            {
                (await pending.KeepAsync(contact, Kept(context.Request), cancellationToken).ConfigureAwait(false))
                    .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
            }

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
