using System;
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
/// <param name="admissions">What this instance admitted from each source and each /48.</param>
/// <param name="configuration">Where the limits come from.</param>
/// <param name="log">Where a source going over its limit is recorded.</param>
/// <remarks>
/// Implements BFF-ORDER-001 stage 4, AC3, AC6 and AC7. The source is the one every other
/// count per source is kept by (AUTH-ABUSE-001), so a deployment behind a proxy names
/// the proxies it trusts to the framework and a deployment that does not is one source.
/// An IPv6 source is also counted by the /48 that encloses it. A source or a /48 already
/// over its limit is refused from memory, without either limit being read and without a
/// line for each refusal, so a flood past the limit reaches no store and fills no log;
/// the line is written once, when the hold begins. A limit that cannot be read is
/// answered as the failure it is, and the request goes no further. The refusal carries
/// the instant the source is admitted again.
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
        string? site = RequestOrigin.Site(context.Request);

        if (((site is null ? null : admissions.HeldUntil(site)) ?? admissions.HeldUntil(source))
            is DateTimeOffset held)
        {
            await ThrottledAsync(context, held).ConfigureAwait(false);

            return;
        }

        Error? unread = null;

        int limit = (await configuration
                .ReadAsync(Settings.AbuseSourceRateLimit, context.RequestAborted)
                .ConfigureAwait(false))
            .Match(within => within, error => Withheld(error, ref unread));

        int siteLimit = unread is null && site is not null
            ? (await configuration
                    .ReadAsync(Settings.AbuseSourceSiteLimit, context.RequestAborted)
                    .ConfigureAwait(false))
                .Match(within => within, error => Withheld(error, ref unread))
            : 0;

        if (unread is not null)
        {
            await Refusal.WriteAsync(context, unread, context.RequestAborted).ConfigureAwait(false);

            return;
        }

        if (admissions.Admit(source, limit, site, siteLimit) is DateTimeOffset lifts)
        {
            BrowserProfileLog.SourceOverLimit(log, context.TraceIdentifier, lifts);
            await ThrottledAsync(context, lifts).ConfigureAwait(false);

            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static Task ThrottledAsync(HttpContext context, DateTimeOffset lifts) =>
        Refusal.WriteAsync(context, Error.Throttled(lifts), context.RequestAborted);

    private static int Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return 0;
    }
}
