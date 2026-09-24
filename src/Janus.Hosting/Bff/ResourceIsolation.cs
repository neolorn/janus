using System;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Bff;

/// <summary>
/// The resource isolation policy: what a browser says about where a request came
/// from, which a page cannot forge.
/// </summary>
/// <param name="log">Where a refusal is recorded.</param>
/// <remarks>
/// Implements BFF-CSRF-002, BFF-CSRF-005 and entry 275. The fetch metadata headers are
/// forbidden request headers, so no script sets them; their absence means a legacy
/// browser or a non-browser client and is not permission, so a request without them
/// goes on to the layers that judge it on what it carries. A cross-site post that
/// navigates the whole page and carries no session, which is how a host's processor
/// returns a browser by form, is not carried either: it is answered 303 with its own
/// address, so the browser reads that address instead and, the cookie being lax,
/// carries the session to the host's GET route. Nothing of the post reaches a stage
/// or an endpoint.
/// </remarks>
internal sealed class ResourceIsolation(ILogger<ResourceIsolation> log) : IMiddleware
{
    private const string Site = "Sec-Fetch-Site";
    private const string CrossSite = "cross-site";
    private const string Mode = "Sec-Fetch-Mode";
    private const string Destination = "Sec-Fetch-Dest";

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

        if (StateChange.Changes(context.Request) && CameFromElsewhere(context.Request))
        {
            if (ReturnsByPost(context.Request))
            {
                BrowserProfileLog.CrossSiteReturn(log, context.TraceIdentifier);
                context.Response.StatusCode = StatusCodes.Status303SeeOther;
                context.Response.Headers.Location = context.Request.GetEncodedPathAndQuery();

                return;
            }

            BrowserProfileLog.CrossSite(log, context.TraceIdentifier, context.Request.Method);
            await Refusal
                .WriteAsync(context, ErrorCodes.SessionCsrfInvalid, context.RequestAborted)
                .ConfigureAwait(false);

            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool CameFromElsewhere(HttpRequest request) => string.Equals(
        request.Headers[Site].ToString(),
        CrossSite,
        StringComparison.OrdinalIgnoreCase);

    // A form another site posted as the whole page, on a browser that brought no
    // session with it: a lax cookie is withheld from exactly this, and a browser that
    // did carry one is refused, since nothing the post says can be told from a forgery.
    private static bool ReturnsByPost(HttpRequest request) =>
        HttpMethods.IsPost(request.Method)
        && string.Equals(request.Headers[Mode].ToString(), "navigate", StringComparison.OrdinalIgnoreCase)
        && string.Equals(request.Headers[Destination].ToString(), "document", StringComparison.OrdinalIgnoreCase)
        && !request.Cookies.ContainsKey(BrowserCookies.Session);
}
