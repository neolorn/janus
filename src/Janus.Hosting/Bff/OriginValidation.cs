using System;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Bff;

/// <summary>
/// The origin a request claims, against the one it arrived at.
/// </summary>
/// <param name="log">Where a refusal is recorded.</param>
/// <remarks>
/// Implements BFF-CSRF-004. Each application has its own origin, which the
/// <c>__Host-</c> prefix already scopes its cookies to, so the expected target is the
/// origin the request arrived at and nothing has to be configured to say so. An
/// opaque origin arrives as the word <c>null</c> and matches nothing.
/// </remarks>
internal sealed class OriginValidation(ILogger<OriginValidation> log) : IMiddleware
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

        string claimed = context.Request.Headers.Origin.ToString();

        if (claimed.Length is not 0
            && !string.Equals(claimed, Target(context.Request), StringComparison.OrdinalIgnoreCase))
        {
            BrowserProfileLog.OriginMismatch(log, context.TraceIdentifier, Target(context.Request));
            await Refusal
                .WriteAsync(context, ErrorCodes.SessionCsrfInvalid, context.RequestAborted)
                .ConfigureAwait(false);

            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static string Target(HttpRequest request) =>
        request.Scheme + Uri.SchemeDelimiter + request.Host.Value;
}
