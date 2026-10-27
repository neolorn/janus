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
/// Implements BFF-STEP-001, BFF-HOST-002 and API-CONV-003. The requirement is asserted
/// here, once, for every endpoint that carries it: an endpoint that had to answer it for
/// itself would be an endpoint that could forget to, and forty of them would be forty
/// chances to answer it differently. A browser whose cookie named a session that has
/// ended is answered with what the resolution stage produced, which carries what has to
/// be done again (BFF-STEP-001 AC3); a browser that carried nothing is answered with the
/// code alone, because it has nothing to do again. Where no endpoint is matched yet, the
/// host's routing comes after the profile, and the same judgement is made of the
/// endpoint it selects (<see cref="SessionRequirementPolicy"/>).
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

        Endpoint? endpoint = context.GetEndpoint();

        if (endpoint is null)
        {
            // BFF-HOST-002 (D-209): the host's routing comes after the profile, and the
            // endpoint it selects is judged then, before it runs.
            context.Features.Set(SessionRequirementDeferred.Mark);
        }
        else if (SessionRequired.Refuses(resolved, endpoint) is Error refusal)
        {
            await Refusal.WriteAsync(context, refusal, context.RequestAborted).ConfigureAwait(false);

            return;
        }

        await next(context).ConfigureAwait(false);
    }
}
