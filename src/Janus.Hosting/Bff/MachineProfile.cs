using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Callbacks;
using Janus.Core;
using Janus.Hosting.Callbacks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Bff;

/// <summary>
/// The layer a callback passes: no session is read, no synchronizer token is asked
/// for, and a request carrying a session cookie is refused as a rejected callback is.
/// </summary>
/// <param name="log">Where a refusal is recorded.</param>
/// <remarks>
/// Implements BFF-MACH-001 and INT-GEN-003. A caller on this profile authenticates with
/// its own credential and never with a cookie, so a cookie arriving here is either a
/// browser reaching an endpoint that is not for it or an attempt to have one ride
/// along. The request is counted against its source first, as every callback is, so a
/// flood is answered by the rate limit; within it the refusal is
/// <c>integration.callback.rejected</c>, counted towards
/// <c>alerting.callback.threshold</c> and committed with the admission's count. A
/// protocol endpoint of the provider is refused by the provider, in its protocol's
/// shape, and does not pass this layer.
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

            CancellationToken cancellationToken = context.RequestAborted;
            CallbackAdmission admission = context.RequestServices.GetRequiredService<CallbackAdmission>();
            IUnitOfWork work = context.RequestServices.GetRequiredService<IUnitOfWork>();

            (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
                .Switch(_ => { }, error => throw new InvalidOperationException(error.Code.ToString()));

            await CallbackIntake
                .CookieRefusedAsync(context, admission, work, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await next(context).ConfigureAwait(false);
    }
}
