using System;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Bff;

/// <summary>
/// The layer a non-browser caller passes: no session is read, no synchronizer token is
/// asked for, and a request carrying a session cookie is refused.
/// </summary>
/// <param name="log">Where a refusal is recorded.</param>
/// <remarks>
/// Implements BFF-MACH-001. A caller on this profile authenticates with its own
/// credential and never with a cookie, so a cookie arriving here is either a browser
/// reaching an endpoint that is not for it or an attempt to have one ride along.
/// </remarks>
internal sealed class MachineProfile(ILogger<MachineProfile> log) : IMiddleware
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

        if (context.Request.Cookies.ContainsKey(BrowserCookies.Session))
        {
            BrowserProfileLog.CookieOnMachineProfile(log, context.TraceIdentifier, context.Request.Method);
            await Refusal
                .WriteAsync(context, ErrorCodes.Denied, context.RequestAborted)
                .ConfigureAwait(false);

            return;
        }

        await next(context).ConfigureAwait(false);
    }
}
