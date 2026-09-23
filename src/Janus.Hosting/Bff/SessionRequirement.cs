using System;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// The stage that holds an endpoint answering only a signed-in person to having one.
/// </summary>
/// <param name="resolved">What the session resolution stage established.</param>
/// <remarks>
/// Implements BFF-STEP-001 and API-CONV-003. The requirement is asserted here, once,
/// for every endpoint that carries it: an endpoint that had to answer it for itself
/// would be an endpoint that could forget to, and forty of them would be forty chances
/// to answer it differently. A browser whose cookie named a session that has ended is
/// answered with what the resolution stage produced, which carries what has to be done
/// again (BFF-STEP-001 AC3); a browser that carried nothing is answered with the code
/// alone, because it has nothing to do again.
/// </remarks>
internal sealed class SessionRequirement(RequestSession resolved) : IMiddleware
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

        if (resolved.Live is null && SessionRequired.Asks(context.GetEndpoint()))
        {
            await Refusal
                .WriteAsync(
                    context,
                    resolved.Expiry ?? Error.From(ErrorCodes.SessionExpired),
                    context.RequestAborted)
                .ConfigureAwait(false);

            return;
        }

        await next(context).ConfigureAwait(false);
    }
}
