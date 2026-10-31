using System;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// What a request may ask for: the scopes the provider is built with, and each scope its
/// client is registered with.
/// </summary>
/// <typeparam name="TContext">The validation of one endpoint that reads a scope.</typeparam>
/// <param name="clients">The registry the client is read from.</param>
/// <remarks>
/// Implements AUTH-OIDC-001 AC5 (D-226). It stands where the server's own check of the
/// requested scopes stood, which knows only the scopes the provider is built with and
/// the deployment's scope rows: a scope beyond the provider's own is admitted where the
/// client the request names is registered with it, and refused with
/// <c>invalid_scope</c> otherwise, a row of the deployment's included. The provider's own
/// scopes go on to the server's check of what the client is permitted, as before.
/// </remarks>
internal sealed class RegisteredScopes<TContext>(IOidcClientStore clients) : IOpenIddictServerHandler<TContext>
    where TContext : OpenIddictServerEvents.BaseValidatingContext
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(TContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Transaction.Request is not OpenIddictRequest request)
        {
            return;
        }

        string[] beyond = [.. request.GetScopes().Where(scope => !context.Options.Scopes.Contains(scope))];

        if (beyond.Length is 0)
        {
            return;
        }

        OidcClient? client = request.ClientId is string named
            ? await clients.FindAsync(named, context.CancellationToken).ConfigureAwait(false)
            : null;

        if (client is null || beyond.Any(scope => !client.Scopes.Contains(scope, StringComparer.Ordinal)))
        {
            context.Reject(error: OpenIddictConstants.Errors.InvalidScope);
        }
    }
}
