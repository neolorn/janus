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
/// A cookie that no longer resolves is session death and is answered as such here,
/// once, by the pipeline: an endpoint that had to notice it for itself would be an
/// endpoint that could forget to. The dead pair is cleared in the same answer, so the
/// browser starts again rather than presenting it on every request afterwards. A
/// browser carrying nothing is nobody, which is not a refusal: what each endpoint
/// requires of a caller is the endpoint's own business.
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

        if (secret.Length is 0)
        {
            await CarriedAsync(context, context.RequestAborted).ConfigureAwait(false);
            await next(context).ConfigureAwait(false);

            return;
        }

        Error? failure = null;

        (await sessions
                .ResolveAsync(
                    OpaqueToken.Of(secret),
                    RequestOrigin.Of(context.Request),
                    context.RequestAborted)
                .ConfigureAwait(false))
            .Switch(session => resolved.Resolved(session), error => failure = error);

        if (failure is not null)
        {
            cookies.Clear(context.Response);
            await Refusal.WriteAsync(context, failure, context.RequestAborted).ConfigureAwait(false);

            return;
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
