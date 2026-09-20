using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Sessions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Bff;

/// <summary>
/// The pre-authentication session a browser is given the first time it arrives.
/// </summary>
/// <param name="contacts">What issues one.</param>
/// <param name="cookies">Where the two values are written.</param>
/// <param name="log">Where an issue is recorded.</param>
/// <remarks>
/// Implements BFF-CSRF-005a and BFF-ORDER-001. It exists so that the endpoints
/// reached before a session exists have something for a synchronizer token to bind
/// to, which is what lets them be protected in exactly the way every other endpoint
/// is rather than exempted. It carries no identity and grants no access, and it is
/// never issued to a browser that already holds a session.
/// </remarks>
internal sealed class FirstContact(
    PreAuthenticationService contacts,
    BrowserSessionCookies cookies,
    ILogger<FirstContact> log) : IMiddleware
{
    /// <summary>
    /// Runs the layer.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>The work of carrying it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (await NeedsOneAsync(context, context.RequestAborted).ConfigureAwait(false))
        {
            (await contacts.IssueAsync(context.RequestAborted).ConfigureAwait(false))
                .Switch(
                    issued => cookies.Write(context.Response, issued),
                    error => BrowserProfileLog.FirstContactRefused(
                        log,
                        context.TraceIdentifier,
                        error.Code.ToString()));
        }

        await next(context).ConfigureAwait(false);
    }

    private async ValueTask<bool> NeedsOneAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (context.Request.Cookies[BrowserCookies.Session] is { Length: > 0 })
        {
            return false;
        }

        string carried = context.Request.Cookies[BrowserCookies.PreAuthentication] ?? string.Empty;

        return carried.Length is 0
            || await contacts.FindAsync(OpaqueToken.Of(carried), cancellationToken)
                .ConfigureAwait(false) is null;
    }
}
