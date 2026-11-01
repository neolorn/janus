using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Microsoft.AspNetCore;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// Where the code is returned to, which is a destination the registry holds.
/// </summary>
/// <param name="clients">The registry the client is read from.</param>
/// <param name="log">Where a refused destination is recorded.</param>
/// <remarks>
/// Implements AUTH-OIDC-006 AC1 and API-REDIR-001 AC4 (D-252). The destination is not
/// matched by prefix or pattern: either the request named one of the client's registered
/// destinations or it did not, and a pushed request is an authorization request validated
/// as one (RFC 9126 section 2.1), so one that named another is refused with
/// <c>invalid_request</c>, no description and no reference (OAuth 2.1 section 2.3.5). A
/// request naming none takes the registered one where the client holds one, and is
/// refused where a public application holds several, since none is then the registered
/// one (OAuth 2.1 section 2.3.2). It is judged where the request is
/// pushed, because every authorization request is (AUTH-OIDC-006 AC2) and the one the
/// browser then carries holds only what was kept here.
/// </remarks>
internal sealed class RegisteredDestination(
    IOidcClientStore clients,
    ILogger<RegisteredDestination> log)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidatePushedAuthorizationRequestContext>
{
    /// <summary>
    /// Where the handler sits: after the server has read the client the request named
    /// and before anything judges the destination it named.
    /// </summary>
    public const int Order = int.MinValue + 102_500;

    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        OpenIddictServerEvents.ValidatePushedAuthorizationRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A request naming no client, or one the registry does not hold, is the
        // server's refusal to make and not this handler's.
        if (context.ClientId is not string named
            || await clients.FindAsync(named, context.CancellationToken).ConfigureAwait(false)
                is not OidcClient client)
        {
            return;
        }

        IReadOnlyList<string> registered = RedirectValidation.Registered(client);

        if (context.RedirectUri is null && registered.Count is 1)
        {
            context.Request.RedirectUri = client.Redirect;
            context.SetRedirectUri(client.Redirect);

            return;
        }

        if (context.RedirectUri is string asked
            && registered.Contains(asked, StringComparer.Ordinal))
        {
            return;
        }

        OidcLog.DestinationRefused(
            log,
            context.Transaction.GetHttpRequest()?.HttpContext.TraceIdentifier ?? string.Empty,
            client.ClientId,
            context.RedirectUri ?? string.Empty);

        context.Reject(error: OpenIddictConstants.Errors.InvalidRequest);
    }
}
