using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Authentication.Sessions;
using Janus.Core;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// The stage that establishes who is asking.
/// </summary>
/// <param name="sessions">What resolves a session from what the cookie carried.</param>
/// <param name="contacts">What resolves a browser that holds no session yet.</param>
/// <param name="resolved">Where the answer is left for the stages after this one.</param>
/// <param name="cookies">What the browser carries.</param>
/// <remarks>
/// Implements BFF-ORDER-001 stage 5, BFF-SESS-001, BFF-CSRF-005a and BFF-STEP-001.
/// A cookie that no longer resolves is cleared, so the browser stops presenting it,
/// and the request goes on as the request of a browser that carried nothing: a
/// person whose session ended has to reach the endpoints that sign them in again.
/// What resolving it answered is kept for the stage that requires a session, which
/// is the one place that refuses on account of there being none.
/// </remarks>
internal sealed class SessionResolution(
    SessionService sessions,
    PreAuthenticationService contacts,
    RequestSession resolved,
    BrowserSessionCookies cookies) : IMiddleware
{
    /// <summary>
    /// Runs the layer.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>The work of carrying or refusing it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        string secret = context.Request.Cookies[BrowserCookies.Session] ?? string.Empty;

        if (secret.Length is not 0)
        {
            Error? failure = null;

            (await sessions
                    .ResolveAsync(
                        OpaqueToken.Of(secret),
                        RequestOrigin.Of(context.Request),
                        context.RequestAborted)
                    .ConfigureAwait(false))
                .Switch(session => resolved.Resolved(session), error => failure = error);

            if (failure is Error ended)
            {
                cookies.Clear(context.Response);
                resolved.Ended(ended);
            }
        }

        if (resolved.Live is null)
        {
            await CarriedAsync(context, context.RequestAborted).ConfigureAwait(false);
        }

        await next(context).ConfigureAwait(false);
    }

    private async ValueTask CarriedAsync(HttpContext context, CancellationToken cancellationToken)
    {
        string carried = context.Request.Cookies[BrowserCookies.PreAuthentication] ?? string.Empty;

        if (carried.Length is 0)
        {
            return;
        }

        PreAuthentication? contact = await contacts
            .FindAsync(OpaqueToken.Of(carried), cancellationToken)
            .ConfigureAwait(false);

        if (contact is not null)
        {
            resolved.Resolved(contact);
        }
    }
}
