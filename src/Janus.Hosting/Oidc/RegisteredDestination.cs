using System;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Microsoft.Extensions.Logging;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// Where the code is returned to, which is the one destination the registry holds.
/// </summary>
/// <param name="clients">The registry the client is read from.</param>
/// <param name="log">Where a replaced destination is recorded.</param>
/// <remarks>
/// Implements API-REDIR-001. The destination is not matched by prefix or pattern:
/// either the request named the client's one registered destination or it did not, and
/// one that did not is replaced by the registered one before anything else reads it,
/// so the answer reveals nothing about the check (API-REDIR-001 AC1) and the code goes
/// where the deployment registered whatever was asked for.
/// </remarks>
internal sealed class RegisteredDestination(
    IOidcClientStore clients,
    ILogger<RegisteredDestination> log)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateAuthorizationRequestContext>
{
    /// <summary>
    /// Where the handler sits: after the server has read the client the request named
    /// and before anything judges the destination it named.
    /// </summary>
    public const int Order = int.MinValue + 102_500;

    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        OpenIddictServerEvents.ValidateAuthorizationRequestContext context)
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

        if (string.Equals(context.RedirectUri, client.Redirect, StringComparison.Ordinal))
        {
            return;
        }

        OidcLog.DestinationReplaced(log, client.ClientId, context.RedirectUri ?? string.Empty);

        context.Request.RedirectUri = client.Redirect;
        context.SetRedirectUri(client.Redirect);
    }
}
