using System;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// Whether an authorization request names a registered client, and which destination
/// the code will be returned to.
/// </summary>
/// <param name="oidc">The registry the client is read from.</param>
/// <param name="log">Where a replaced destination is recorded.</param>
/// <remarks>
/// Implements AUTH-OIDC-001 and API-REDIR-001. The destination is not matched by
/// prefix or pattern: either the request named the client's one registered
/// destination or it did not, and a destination that did not is replaced by the
/// registered one rather than refused, so nothing about the check is answered back.
/// </remarks>
internal sealed class AuthorizationValidation(IOidc oidc, ILogger<AuthorizationValidation> log)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateAuthorizationRequestContext>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        OpenIddictServerEvents.ValidateAuthorizationRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        OidcClient? client = context.ClientId is string named
            ? await oidc.FindClientAsync(named, context.CancellationToken).ConfigureAwait(false)
            : null;

        if (client is null)
        {
            OidcLog.ClientUnknown(log, context.ClientId ?? string.Empty);
            context.Reject(
                OpenIddictConstants.Errors.InvalidClient,
                description: null,
                uri: null);

            return;
        }

        if (!string.Equals(context.RedirectUri, client.Redirect, StringComparison.Ordinal))
        {
            OidcLog.DestinationReplaced(log, client.ClientId, context.RedirectUri ?? string.Empty);

            // What the request asked for is gone before anything else reads it, so
            // the rest of the exchange knows only the registered destination and the
            // answer says nothing about the one that was replaced.
            context.Request.RedirectUri = client.Redirect;
        }

        context.SetRedirectUri(client.Redirect);
    }
}
