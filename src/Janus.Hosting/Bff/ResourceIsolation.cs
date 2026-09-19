using System;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Bff;

/// <summary>
/// The resource isolation policy: what a browser says about where a request came
/// from, which a page cannot forge.
/// </summary>
/// <param name="log">Where a refusal is recorded.</param>
/// <remarks>
/// Implements BFF-CSRF-002. The fetch metadata headers are forbidden request
/// headers, so no script sets them; their absence means a legacy browser or a
/// non-browser client and is not permission, so a request without them goes on to
/// the layers that judge it on what it carries.
/// </remarks>
internal sealed class ResourceIsolation(ILogger<ResourceIsolation> log) : IMiddleware
{
    private const string Site = "Sec-Fetch-Site";
    private const string CrossSite = "cross-site";

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
}
