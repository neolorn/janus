using System;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Janus.Hosting.Bff;

/// <summary>
/// The flood limit every request from one source passes before anything reads a
/// session.
/// </summary>
/// <param name="admissions">What this instance admitted from each source.</param>
/// <param name="configuration">Where the limit comes from.</param>
/// <param name="log">Where a source going over its limit is recorded.</param>
/// <remarks>
/// Implements BFF-ORDER-001 stage 4 and AC3. The source is the address the connection
/// arrived on, the same one every other count per source is kept by, so a deployment
/// behind a proxy names the proxies it trusts to the framework and a deployment that
/// does not is one source. A source already over its limit is refused from memory,
/// without the limit being read, so a flood past the limit reaches no store; a limit
/// that cannot be read is answered as the failure it is, and the request goes no
/// further. The refusal carries the instant the source is admitted again.
/// </remarks>
internal sealed class SourceRateLimiting(
    SourceAdmissions admissions,
    IConfigurationStore configuration,
    ILogger<SourceRateLimiting> log) : IMiddleware
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

        string source = RequestOrigin.Source(context.Request);

        if (admissions.HeldUntil(source) is DateTimeOffset held)
        {
            await ThrottledAsync(context, held).ConfigureAwait(false);

            return;
        }

        Result<int> limit = await configuration
            .ReadAsync(Settings.AbuseSourceRateLimit, context.RequestAborted)
            .ConfigureAwait(false);

        if (limit.Match(_ => (Error?)null, error => error) is Error unread)
        {
            await Refusal.WriteAsync(context, unread, context.RequestAborted).ConfigureAwait(false);

            return;
        }

        if (admissions.Admit(source, limit.Match(within => within, _ => 0)) is DateTimeOffset lifts)
        {
            BrowserProfileLog.SourceOverLimit(log, context.TraceIdentifier, lifts);
            await ThrottledAsync(context, lifts).ConfigureAwait(false);

            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static Task ThrottledAsync(HttpContext context, DateTimeOffset lifts) =>
        Refusal.WriteAsync(
            context,
            Error.From(ErrorCodes.Throttled, "retryAt", JsonSerializer.SerializeToElement(lifts)),
            context.RequestAborted);
}
