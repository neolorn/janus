using System;
using System.Threading;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Janus.Hosting.Credentials;

/// <summary>
/// The social providers' security events, <c>POST /callbacks/providers/{provider}</c>,
/// on the machine profile.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-012a AC3, BFF-MACH-001 and INT-GEN-003. One route a provider the
/// factor catalogue names as social, so a path naming any other is not the library's.
/// </remarks>
internal static class ProviderEventEndpoints
{
    private const string Prefix = "/callbacks/providers/";

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapProviderEvents(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        foreach ((string route, Factor provider) in ProviderRoutes.Named)
        {
            RouteHandlerBuilder taken = endpoints.MapPost(
                    Prefix + route,
                    (ProviderEventIntake intake, HttpContext context, CancellationToken cancellationToken) =>
                        intake.TakeAsync(context, provider, cancellationToken))
                .Declares(EndpointDeclaration.Answering(
                    ErrorCodes.CallbackInProgress,
                    ErrorCodes.CallbackRejected,
                    ErrorCodes.SystemFault))
                .Produces(ProviderEventIntake.Taken(provider));

            // RFC 8935 section 2.4: a provider that pushes its events is refused in the
            // standard's own shape.
            if (ProviderEventIntake.Pushes(provider))
            {
                _ = taken.Produces<SecurityEventError>(StatusCodes.Status400BadRequest);
            }
        }

        return endpoints;
    }
}
