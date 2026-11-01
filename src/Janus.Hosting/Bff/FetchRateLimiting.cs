using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Janus.Hosting.Bff;

/// <summary>
/// The limit each fetch of the provider's discovery document or key set passes on the
/// machine profile, before the provider answers it.
/// </summary>
/// <remarks>
/// Implements BFF-MACH-001 AC4 (D-241). Discovery is held to
/// <c>oidc.discovery.ratelimit</c> fetches per source a minute and the key set to
/// <c>oidc.jwks.ratelimit</c>, the profile saying which route it is; past it the route answers 429
/// with <c>Retry-After</c> and no body, as the fetchers are servers that read the header.
/// It writes no cookie. A limit that cannot be read is answered as the failure it is, and
/// the fetch goes no further.
/// </remarks>
internal static class FetchRateLimiting
{
    /// <summary>
    /// Runs the layer for one route.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <param name="keySet">Whether the route is the key set, or else discovery.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The work of carrying or refusing it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task InvokeAsync(HttpContext context, RequestDelegate next, bool keySet, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        IntegerSetting limit = keySet ? Settings.OidcJwksRateLimit : Settings.OidcDiscoveryRateLimit;
        FetchAdmissions admissions = context.RequestServices.GetRequiredService<FetchAdmissions>();
        IConfigurationStore configuration = context.RequestServices.GetRequiredService<IConfigurationStore>();
        Error? unread = null;

        int admitted = (await configuration
                .ReadAsync(limit, cancellationToken)
                .ConfigureAwait(false))
            .Match(within => within, error => Withheld(error, ref unread));

        if (unread is not null)
        {
            await Refusal.WriteAsync(context, unread, cancellationToken).ConfigureAwait(false);

            return;
        }

        if (admissions.Admit(limit.Key.ToString(), RequestOrigin.Source(context.Request), admitted) is TimeSpan wait)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers[HeaderNames.RetryAfter] =
                Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static int Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return 0;
    }
}
