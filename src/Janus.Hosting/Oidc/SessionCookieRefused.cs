using System;
using System.Threading.Tasks;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Janus.Hosting.Oidc;

/// <summary>
/// A request to one of the provider's machine routes that carries the browser's
/// session cookie, which the route refuses as its protocol refuses a bad request.
/// </summary>
/// <typeparam name="TContext">The validation of one endpoint the provider serves.</typeparam>
/// <param name="log">Where the refusal is recorded.</param>
/// <remarks>
/// Implements BFF-MACH-001 criterion 2. A client of these routes authenticates with its
/// own secret or presents a token and never carries a cookie, so one arriving here is a
/// browser reaching a route that is not for it. The refusal is the server's own
/// <c>invalid_request</c>, so its status and body are the ones the route gives that
/// error, and it is made before anything the request presents is read.
/// </remarks>
internal sealed class SessionCookieRefused<TContext>(ILogger<SessionCookieRefused<TContext>> log)
    : IOpenIddictServerHandler<TContext>
    where TContext : OpenIddictServerEvents.BaseValidatingContext
{
    /// <summary>
    /// Where the handler sits: before the first step of the server that judges the
    /// request.
    /// </summary>
    public const int Order = int.MinValue + 50_000;

    /// <inheritdoc/>
    public ValueTask HandleAsync(TContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Transaction.GetHttpRequest() is HttpRequest request
            && request.Cookies.ContainsKey(BrowserCookies.Session))
        {
            BrowserProfileLog.CookieOnMachineProfile(log, request.HttpContext.TraceIdentifier, request.Method);

            context.Reject(error: OpenIddictConstants.Errors.InvalidRequest);
        }

        return ValueTask.CompletedTask;
    }
}
