using System;
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
/// The continuation of a browser's pending authorization: the request kept for its
/// sign-in, read back off the session record the browser now carries and answered as
/// the authorization endpoint answers it.
/// </summary>
/// <param name="browser">What the browser carried.</param>
/// <param name="pending">Where the request waits.</param>
/// <param name="configuration">Where the client a browser falls back to is named.</param>
/// <param name="clients">The registry that client is read from.</param>
/// <remarks>
/// Implements AUTH-SESS-012 AC10 and API-REDIR-001 (D-224, D-226, D-228). The request
/// is spent as it is read, so a second follow finds nothing. A browser with nothing
/// waiting, or with no live session to answer it on, is sent to the origin of the
/// registered address of <c>redirect.defaultclient</c>, or to <c>/</c> while that key
/// is empty; the route never renders a page.
/// </remarks>
internal sealed class AuthorizationContinued(
    RequestSession browser,
    PendingAuthorizations pending,
    IConfigurationStore configuration,
    IOidcClientStore clients)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ExtractAuthorizationRequestContext>
{
    /// <summary>
    /// Where the handler runs: once the request has been read from the address, so what
    /// it reads is replaced before anything validates it.
    /// </summary>
    public static int Order { get; } =
        OpenIddictServerAspNetCoreHandlers.ExtractGetOrPostRequest<OpenIddictServerEvents.ExtractAuthorizationRequestContext>
            .Descriptor.Order + 500;

    /// <summary>
    /// Whether a request is the continuation's, which the requirement that every request
    /// be pushed admits without a reference (AUTH-OIDC-006).
    /// </summary>
    /// <param name="transaction">The request being answered.</param>
    /// <returns>Whether it is.</returns>
    /// <exception cref="ArgumentNullException">The transaction is absent.</exception>
    public static bool Continues(OpenIddictServerTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return transaction.GetProperty<object>(typeof(AuthorizationContinued).FullName!) is not null;
    }

    /// <inheritdoc/>
    public async ValueTask HandleAsync(OpenIddictServerEvents.ExtractAuthorizationRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Transaction.GetHttpRequest() is not HttpRequest request
            || !request.Path.Equals("/" + OidcRegistration.Continuation, StringComparison.Ordinal))
        {
            return;
        }

        string? kept = browser.Live is Session live
            ? await pending.TakeAsync(live.Spine, context.CancellationToken).ConfigureAwait(false)
            : null;

        if (kept is null)
        {
            request.HttpContext.Response.Redirect(await FallbackAsync(context.CancellationToken).ConfigureAwait(false));
            context.HandleRequest();

            return;
        }

        using var read = JsonDocument.Parse(kept);

        context.Request = new OpenIddictRequest(read.RootElement.Clone());
        context.Transaction.SetProperty(typeof(AuthorizationContinued).FullName!, this);
    }

    // D-228: the origin of the registered address of the client a browser falls back to,
    // or this application's root while the deployment names none (API-REDIR-001).
    private async ValueTask<string> FallbackAsync(CancellationToken cancellationToken)
    {
        string named = (await configuration
                .ReadAsync(Settings.RedirectDefaultClient, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        return named.Length > 0
            && await clients.FindAsync(named, cancellationToken).ConfigureAwait(false) is OidcClient client
                ? RedirectValidation.Landing(client)
                : "/";
    }
}
