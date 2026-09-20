using System;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// Whether a token request names a registered client.
/// </summary>
/// <param name="oidc">The registry the client is read from.</param>
/// <param name="log">Where an unregistered client is recorded.</param>
/// <remarks>
/// Implements AUTH-OIDC-001. The secret itself is judged where the exchange is
/// judged, so that one refusal covers a wrong secret, a wrong code and a wrong
/// verifier and none of them is told apart from the others.
/// </remarks>
internal sealed class ClientAuthentication(IOidc oidc, ILogger<ClientAuthentication> log)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenRequestContext>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.ClientId is string named
            && (await oidc.FindClientAsync(named, context.CancellationToken).ConfigureAwait(false))
                .Match(_ => true, _ => false))
        {
            return;
        }

        OidcLog.ClientUnknown(log, context.Request.ClientId ?? string.Empty);
        context.Reject(OpenIddictConstants.Errors.InvalidClient, description: null, uri: null);
    }
}
