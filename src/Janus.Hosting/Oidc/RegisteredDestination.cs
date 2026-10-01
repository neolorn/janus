using System;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Microsoft.AspNetCore;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// Where the code is returned to, which is the one destination the registry holds.
/// </summary>
/// <param name="clients">The registry the client is read from.</param>
/// <param name="log">Where a refused destination is recorded.</param>
/// <remarks>
/// Implements AUTH-OIDC-006 AC1 and API-REDIR-001 AC4. The destination is not matched by
/// prefix or pattern: either the request named the client's one registered destination
/// or it did not, and a pushed request is an authorization request validated as one
/// (RFC 9126 section 2.1), so one that named another is refused with
/// <c>invalid_request</c>, no description and no reference (OAuth 2.1 section 2.3.5). A
/// request naming none takes the registered one. It is judged where the request is
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

        if (context.RedirectUri is null)
        {
            context.Request.RedirectUri = client.Redirect;
            context.SetRedirectUri(client.Redirect);

            return;
        }

        if (string.Equals(context.RedirectUri, client.Redirect, StringComparison.Ordinal))
        {
            return;
        }

        OidcLog.DestinationRefused(
            log,
            context.Transaction.GetHttpRequest()?.HttpContext.TraceIdentifier ?? string.Empty,
            client.ClientId,
            context.RedirectUri);

        context.Reject(error: OpenIddictConstants.Errors.InvalidRequest);
    }
}
