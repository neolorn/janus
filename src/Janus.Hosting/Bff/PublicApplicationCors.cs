using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Janus.Hosting.Bff;

/// <summary>
/// The cross-origin answer the token endpoint, discovery and the key set give a public
/// application's browser.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-006 (D-252). A public application may run its code exchange in
/// the browser, so a request to these routes from the origin of a return address a
/// <c>public-application</c> client registered is answered for that origin exactly,
/// with no credentials allowed, and its preflight is answered with the one method the
/// route takes and the one header the token request sends. A request from any other
/// origin is carried as before, with no cross-origin header, and the profile mounts this
/// on no other route. The origins are read from the registry on each request that names
/// one, so a client registered or changed is answered at once.
/// </remarks>
internal static class PublicApplicationCors
{
    private const string AllowOrigin = "Access-Control-Allow-Origin";

    private const string AllowMethods = "Access-Control-Allow-Methods";

    private const string AllowHeaders = "Access-Control-Allow-Headers";

    private const string RequestMethod = "Access-Control-Request-Method";

    /// <summary>
    /// Runs the stage for one route.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <param name="token">Whether the route is the token endpoint, or else a document.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The work of answering or carrying it.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async Task InvokeAsync(HttpContext context, RequestDelegate next, bool token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        string origin = context.Request.Headers.Origin.ToString();

        if (origin.Length is 0)
        {
            await next(context).ConfigureAwait(false);

            return;
        }

        // A cached answer for one origin is never served to another.
        context.Response.Headers.Append(HeaderNames.Vary, HeaderNames.Origin);

        IOidcClientStore clients = context.RequestServices.GetRequiredService<IOidcClientStore>();

        if (!Registered(await clients.AllAsync(cancellationToken).ConfigureAwait(false), origin))
        {
            await next(context).ConfigureAwait(false);

            return;
        }

        context.Response.Headers[AllowOrigin] = origin;

        if (HttpMethods.IsOptions(context.Request.Method) && context.Request.Headers.ContainsKey(RequestMethod))
        {
            context.Response.Headers[AllowMethods] = token ? HttpMethods.Post : HttpMethods.Get;

            if (token)
            {
                context.Response.Headers[AllowHeaders] = HeaderNames.ContentType;
            }

            context.Response.StatusCode = StatusCodes.Status204NoContent;

            return;
        }

        await next(context).ConfigureAwait(false);
    }

    // Whether the origin is exactly that of a public application's registered return
    // address, compared as the browser serialises an origin (RFC 6454 section 6.1).
    private static bool Registered(IReadOnlyList<OidcClient> clients, string origin) =>
        clients
            .Where(client => client.Kind is OidcClientKind.PublicApplication)
            .SelectMany(RedirectValidation.Registered)
            .Any(address => Uri.TryCreate(address, UriKind.Absolute, out Uri? parsed)
                && string.Equals(parsed.GetLeftPart(UriPartial.Authority), origin, StringComparison.Ordinal));
}
