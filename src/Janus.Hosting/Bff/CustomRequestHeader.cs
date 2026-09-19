using System;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Bff;

/// <summary>
/// The custom request header a state-changing request carries, whose presence is
/// checked and whose value is ignored.
/// </summary>
/// <param name="log">Where a refusal is recorded.</param>
/// <remarks>
/// Implements BFF-CSRF-003. A cross-origin page cannot add a header without a
/// preflight it cannot satisfy, so the header's presence is a fact about the caller
/// rather than a claim by it.
/// </remarks>
internal sealed class CustomRequestHeader(ILogger<CustomRequestHeader> log) : IMiddleware
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

        if (StateChange.Changes(context.Request)
            && !context.Request.Headers.ContainsKey(BrowserCookies.RequestHeader))
        {
            BrowserProfileLog.HeaderAbsent(log, context.TraceIdentifier, context.Request.Method);
            await Refusal
                .WriteAsync(context, ErrorCodes.SessionCsrfInvalid, context.RequestAborted)
                .ConfigureAwait(false);

            return;
        }

        await next(context).ConfigureAwait(false);
    }
}
